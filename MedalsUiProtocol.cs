#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace KellysMedalsUi
{
    // Source-shared by the server and optional client. No client-supplied player identity.
    internal sealed class MedalView
    {
        internal string Key, Name, Description, Mode, Category, Tier, AwardedUtc;
        internal long Progress, Threshold;
        internal bool Revoked;
        internal bool Earned => !Revoked && !string.IsNullOrEmpty(AwardedUtc);
        internal double Fraction => Math.Min(1d, (double)Progress / Math.Max(1, Threshold));
    }

    internal static class MedalsUiProtocol
    {
        internal const string Command = "/medals ui1 ";
        internal const string Prefix = "[KMED1]|";
        internal const int MaxRows = 512, MaxChunks = 128, ChunkSize = 768, MaxBytes = 262144;
        internal static bool ValidId(string id) => id != null && id.Length == 32 && id.All(c =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));

        internal static IEnumerable<string> Encode(string request, string revision, IList<MedalView> rows)
        {
            if (!ValidId(request) || !ValidId(revision) || rows.Count > MaxRows) throw new InvalidDataException();
            byte[] raw;
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(rows.Count);
                    foreach (var row in rows)
                    {
                        writer.Write(row.Key); writer.Write(row.Name); writer.Write(row.Description);
                        writer.Write(row.Mode); writer.Write(row.Category); writer.Write(row.Tier);
                        writer.Write(row.Progress); writer.Write(row.Threshold);
                        writer.Write(row.AwardedUtc ?? ""); writer.Write(row.Revoked);
                    }
                }
                raw = stream.ToArray();
            }
            if (raw.Length > MaxBytes) throw new InvalidDataException();
            string payload;
            using (var stream = new MemoryStream())
            {
                using (var zip = new DeflateStream(stream, CompressionMode.Compress, true)) zip.Write(raw, 0, raw.Length);
                payload = Convert.ToBase64String(stream.ToArray());
            }
            int count = (payload.Length + ChunkSize - 1) / ChunkSize;
            if (count > MaxChunks) throw new InvalidDataException();
            for (int i = 0; i < count; i++)
                yield return Prefix + request + "|" + revision + "|" + i.ToString(CultureInfo.InvariantCulture) + "|" +
                    count.ToString(CultureInfo.InvariantCulture) + "|" + payload.Substring(i * ChunkSize, Math.Min(ChunkSize, payload.Length - i * ChunkSize));
        }

        internal static List<MedalView> Decode(string payload)
        {
            using (var input = new MemoryStream(Convert.FromBase64String(payload)))
            using (var zip = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[4096];
                int read;
                while ((read = zip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + read > MaxBytes) throw new InvalidDataException();
                    output.Write(buffer, 0, read);
                }
                output.Position = 0;
                using (var reader = new BinaryReader(output))
                {
                    int count = reader.ReadInt32();
                    if (count < 0 || count > MaxRows) throw new InvalidDataException();
                    var rows = new List<MedalView>();
                    var keys = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < count; i++)
                    {
                        var row = new MedalView {
                            Key = reader.ReadString(), Name = reader.ReadString(), Description = reader.ReadString(),
                            Mode = reader.ReadString(), Category = reader.ReadString(), Tier = reader.ReadString(),
                            Progress = reader.ReadInt64(), Threshold = reader.ReadInt64(),
                            AwardedUtc = reader.ReadString(), Revoked = reader.ReadBoolean()
                        };
                        if (row.Key.Length == 0 || row.Key.Length > 128 || !keys.Add(row.Key) || row.Name.Length > 160 ||
                            row.Description.Length > 2048 || row.Mode.Length > 32 || row.Category.Length > 64 ||
                            row.Tier.Length > 32 || row.AwardedUtc.Length > 40 || row.Progress < 0 || row.Threshold <= 0)
                            throw new InvalidDataException();
                        rows.Add(row);
                    }
                    if (output.Position != output.Length) throw new InvalidDataException();
                    return rows;
                }
            }
        }
    }

    internal sealed class MedalsUiSession
    {
        internal List<MedalView> Rows { get; private set; }
        internal double ReceivedAt { get; private set; }
        internal string Request { get; private set; }
        private string revision, completed;
        private string[] chunks;
        internal void Query(string id) { Request = id; revision = completed = null; chunks = null; }
        internal void Reset() { Query(null); Rows = null; ReceivedAt = 0; }
        internal bool Fresh(double now) => Rows != null && now - ReceivedAt < 20;
        internal bool Receive(string message, double now)
        {
            if (message == null || message.Length > 900 || !message.StartsWith(MedalsUiProtocol.Prefix, StringComparison.Ordinal)) return false;
            var parts = message.Split('|');
            int index, count;
            if (parts.Length != 6 || Request == null || parts[1] != Request || !MedalsUiProtocol.ValidId(parts[2]) || parts[2] == completed ||
                !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out index) ||
                !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 1 || count > MedalsUiProtocol.MaxChunks ||
                index < 0 || index >= count || parts[5].Length > MedalsUiProtocol.ChunkSize) return false;
            if (revision != parts[2]) { revision = parts[2]; chunks = new string[count]; }
            if (chunks.Length != count || (chunks[index] != null && chunks[index] != parts[5])) return false;
            chunks[index] = parts[5];
            if (chunks.Any(c => c == null)) return false;
            try { Rows = MedalsUiProtocol.Decode(string.Concat(chunks)); }
            catch (Exception e) when (e is IOException || e is FormatException || e is ArgumentException) { chunks = null; revision = null; return false; }
            ReceivedAt = now; completed = revision; chunks = null; revision = null;
            return true;
        }
    }

    internal static class MedalsViewFilter
    {
        internal static List<MedalView> Select(IEnumerable<MedalView> rows, string category, string mode, int tab)
        {
            var query = rows.Where(r => (category == "ALL" || r.Category == category) && (mode == "ALL" || r.Mode == mode));
            if (tab == 0) return query.Where(r => !r.Earned && !r.Revoked).OrderByDescending(r => r.Fraction).ThenBy(r => r.Name, StringComparer.Ordinal).ToList();
            if (tab == 1) return query.Where(r => r.Earned).OrderByDescending(r => r.AwardedUtc, StringComparer.Ordinal).ThenBy(r => r.Name, StringComparer.Ordinal).ToList();
            return query.OrderBy(r => r.Category, StringComparer.Ordinal).ThenBy(r => r.Name, StringComparer.Ordinal).ToList();
        }
    }
}
