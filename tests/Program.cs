using System;
using System.Collections.Generic;
using System.Linq;
using KellysMedalsUi;
using KellysMEDALSPublicUI;

internal static class Program
{
    private static int checks;
    private static void Check(bool pass, string name) { checks++; if (!pass) throw new Exception(name); }
    private static void Main()
    {
        var rows = Enumerable.Range(0, 296).Select(i => new MedalView {
            Key = "medal_" + i, Name = "Medal " + i, Description = "Requirement " + i,
            Category = i % 2 == 0 ? "Combat" : "Support", Mode = "Universal", Tier = "Bronze",
            Progress = i, Threshold = 500, AwardedUtc = i == 0 ? "2026-09-19T00:00:00Z" : ""
        }).ToList();
        string id = new string('a', 32), revision = new string('b', 32);
        var frames = MedalsUiProtocol.Encode(id, revision, rows).ToArray();
        var session = new MedalsUiSession(); session.Query(id);
        Check(frames.Length > 1 && frames.All(f => f.Length <= 900), "bounded frames");
        Check(!session.Receive(frames[0], 1) && session.Rows == null, "no partial display");
        foreach (var frame in frames.Reverse()) session.Receive(frame, 1);
        Check(session.Rows.Count == rows.Count, "complete roundtrip");
        for (int i = 0; i < rows.Count; i++) Check(session.Rows[i].Key == rows[i].Key && session.Rows[i].Progress == rows[i].Progress, "row roundtrip");
        Check(session.Fresh(20) && !session.Fresh(21), "stale status");
        Check(!session.Receive(frames[0], 30) && !session.Fresh(30), "replay cannot refresh");
        session.Query(new string('c',32)); Check(!session.Receive(frames[0], 30), "old request rejected");
        session.Reset(); Check(session.Rows == null, "disconnect clears data");
        Check(MedalsViewFilter.Select(rows, "ALL", "ALL", 1).Count == 1, "earned filter");
        Check(MedalsViewFilter.Select(rows, "ALL", "ALL", 0).Count == 295, "next filter");
        Check(MedalsViewFilter.Select(rows, "Support", "ALL", 2).Count == 148, "category filter");
        rows[1].Revoked = true;
        Check(MedalsViewFilter.Select(rows, "ALL", "ALL", 0).Count == 294, "revoked excluded");
        var slots = new List<object> { new object(), new object(), new object() };
        var med = new object(); var air = new object(); var tal = new object();
        var lease = MfdSlotLease<object>.TryClaim(slots, 6, med, i => true)!;
        Check(lease.Index == 5, "last vacant slot");
        slots[3] = air; slots[4] = tal; lease.Dispose();
        Check(slots.Count == 5 && slots[3] == air && slots[4] == tal, "other panels preserved");
        Check(MfdSlotLease<object>.TryClaim(slots, 5, med, i => true) == null, "full slots unchanged");
        var layout = MfdPanelLayout.Calculate(1280, 720, 420, 460, 760);
        Check(layout.Scale > 0 && layout.Y + layout.Scale * 760 <= 720, "viewport fit");
        Console.WriteLine("PASS: " + checks + " client logic checks.");
    }
}
