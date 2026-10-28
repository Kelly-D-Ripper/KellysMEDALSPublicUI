#nullable disable
// Compile the real Plugin.cs against deliberately minimal game doubles. No game files in CI.
using System;
using System.Reflection;
using KellysMedalsUi;
using KellysMEDALSPublicUI;
using NuclearOption.Chat;
using NuclearOption.ModdingApi;

internal static class PrivacyHarness
{
    private static int checks;
    private static void Check(bool ok, string name) { checks++; if (!ok) throw new Exception("Privacy: " + name); }
    private static void Call(Plugin plugin, string name) => typeof(Plugin).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(plugin, null);
    private static ChatCommandDescriptor Supported() => new ChatCommandDescriptor {
        Name = MedalsUiProtocol.ServerCommand, Description = MedalsUiProtocol.Capability,
        Arguments = new[] { new ChatCommandArgument("request", ChatArgumentType.Text, false) }
    };
    internal static void Run()
    {
        var plugin = new Plugin();
        Call(plugin, "Awake");
        GameManager.Local = new Player { IsLocalPlayer = true };
        ChatManager.i = new ChatManager();
        SceneSingleton<DynamicMap>.i = new DynamicMap();
        // Cross product: host, client, capability, Enabled and panel visibility.
        for (int bits = 0; bits < 32; bits++)
        {
            var manager = new ChatCommandManager { IsServer = (bits & 1) != 0, IsClient = (bits & 2) != 0,
                HasDescriptor = (bits & 4) != 0, Descriptor = Supported() };
            ChatCommandManager.i = manager;
            plugin.Config.Enabled.Value = (bits & 8) != 0;
            MedalsMfdUi.Open = (bits & 16) != 0;
            UnityEngine.Time.unscaledTime += 100;
            Call(plugin, "Update");
            bool allowed = bits == 30;
            Check(manager.Sends == (allowed ? 1 : 0), "request gates " + bits);
            if (allowed) Check(manager.LastName == MedalsUiProtocol.ServerCommand && MedalsUiProtocol.ValidId(manager.LastArgs), "native-only nonce");
        }
        plugin.Config.Enabled.Value = true; MedalsMfdUi.Open = true;
        var supported = new ChatCommandManager { IsClient = true, HasDescriptor = true, Descriptor = Supported() };
        ChatCommandManager.i = supported;
        Call(plugin, "Update");
        string first = plugin.Session.Request;
        Check(first != null && supported.Sends == 1, "supported server sends");
        Call(plugin, "Update"); Check(supported.Sends == 1, "renewal throttled");
        UnityEngine.Time.unscaledTime += 31;
        Call(plugin, "Update"); Check(supported.Sends == 2, "renewal allowed");
        supported.HasDescriptor = false;
        Call(plugin, "Update");
        Check(plugin.Session.Request == null && supported.Sends == 2 && plugin.TransportStatus.Contains("1.13.2"), "capability removed clears session without sending");
        supported.HasDescriptor = true;
        Call(plugin, "Update"); Check(supported.Sends == 3, "capability restored");
        supported.IsClient = false;
        Call(plugin, "Update"); Check(plugin.Session.Request == null && supported.Sends == 3, "disconnect clears request");
        supported.IsClient = true;
        Call(plugin, "Update");
        supported.IsServer = true;
        Call(plugin, "Update"); Check(plugin.Session.Request == null && plugin.TransportStatus.Contains("single-player"), "remote to local host blocked");
        for (int fault = 0; fault < 10; fault++)
        {
            var descriptor = Supported();
            switch (fault)
            {
                case 0: descriptor.Name = "wrong"; break;
                case 1: descriptor.Description = "wrong"; break;
                case 2: descriptor.RequiredPermissionLevel = (PermissionLevel)1; break;
                case 3: descriptor.Arguments = null; break;
                case 4: descriptor.Arguments = new ChatCommandArgument[0]; break;
                case 5: descriptor.Arguments[0] = new ChatCommandArgument("other", ChatArgumentType.Text, false); break;
                case 6: descriptor.Arguments[0] = new ChatCommandArgument("request", (ChatArgumentType)99, false); break;
                case 7: descriptor.Arguments[0] = new ChatCommandArgument("request", ChatArgumentType.Text, true); break;
                case 8: descriptor.Aliases = new[] { "alias" }; break;
                case 9: descriptor.Arguments = new[] { descriptor.Arguments[0], descriptor.Arguments[0] }; break;
            }
            var manager = new ChatCommandManager { IsClient = true, HasDescriptor = true, Descriptor = descriptor };
            ChatCommandManager.i = manager; Call(plugin, "Update");
            Check(manager.Sends == 0 && plugin.Session.Request == null, "invalid descriptor " + fault);
        }
        ChatCommandManager.i = supported; supported.IsServer = false;
        foreach (bool builtIn in new[] { false, true })
        {
            ChatCommandCollections.ClientCollision = !builtIn; ChatCommandCollections.BuiltInCollision = builtIn;
            Call(plugin, "Update"); Check(plugin.Session.Request == null, "command collision rejected");
        }
        ChatCommandCollections.ClientCollision = ChatCommandCollections.BuiltInCollision = false;
        Call(plugin, "Update"); string oldRequest = plugin.Session.Request;
        GameManager.Local = new Player { IsLocalPlayer = true };
        Call(plugin, "Update"); Check(plugin.Session.Request != oldRequest, "player change resets nonce");
        oldRequest = plugin.Session.Request;
        SceneSingleton<DynamicMap>.i = new DynamicMap();
        Call(plugin, "Update"); Check(plugin.Session.Request != oldRequest, "map change resets nonce");
        oldRequest = plugin.Session.Request; ChatManager.i = new ChatManager();
        Call(plugin, "Update"); Check(plugin.Session.Request != oldRequest, "chat change resets nonce");
        GameManager.Local = null; Call(plugin, "Update"); Check(plugin.Session.Request == null, "missing player");
        GameManager.Local = new Player { IsLocalPlayer = false }; Call(plugin, "Update"); Check(plugin.Session.Request == null, "non-local player");
        GameManager.Local.IsLocalPlayer = true; ChatCommandManager.i = null;
        Call(plugin, "Update"); Check(plugin.Session.Request == null, "missing command manager");
        ChatCommandManager.i = supported; SceneSingleton<DynamicMap>.i = null;
        Call(plugin, "Update"); Check(plugin.Session.Request == null, "missing map");
        SceneSingleton<DynamicMap>.i = new DynamicMap(); ChatManager.i = null;
        Call(plugin, "Update"); Check(plugin.Session.Request == null, "missing chat receiver");
        ChatManager.i = new ChatManager(); Call(plugin, "Update");
        Call(plugin, "OnDisable"); Check(plugin.Session.Request == null, "disable reset");
        Call(plugin, "OnDestroy");
        Check(ChatManager.PublicSends == 0, "zero ordinary chat sends across all scenarios");
        Console.WriteLine("PASS: " + checks + " real-plugin privacy checks (stubbed game context).");
    }
}

namespace UnityEngine { public static class Application { public static bool isBatchMode; } public static class Time { public static float unscaledTime; } }
namespace BepInEx.Configuration { public class ConfigEntry<T> { public T Value; } public class ConfigFile {
    public ConfigEntry<bool> Enabled = new ConfigEntry<bool>();
    public ConfigEntry<bool> Bind(string section, string key, bool value, string description) { Enabled.Value = value; return Enabled; }
} }
namespace BepInEx {
    public sealed class BepInPlugin : Attribute { public BepInPlugin(string id, string name, string version) {} }
    public class Log { public void LogInfo(object value) {} public void LogError(object value) {} public void LogWarning(object value) {} }
    public class BaseUnityPlugin { public bool enabled = true; public bool isActiveAndEnabled => enabled; public Log Logger = new Log(); public Configuration.ConfigFile Config = new Configuration.ConfigFile(); }
}
namespace HarmonyLib {
    public class Harmony { public Harmony(string id) {} public void Patch(object method, HarmonyMethod prefix) {} public void UnpatchSelf() {} }
    public class HarmonyMethod { public HarmonyMethod(Type type, string name) {} }
    public static class AccessTools { public static object Method(Type type, string name, Type[] args) => new object(); }
}
namespace Mirage { public interface INetworkPlayer {} }
namespace NuclearOption.Networking { }
public class Player { public bool IsLocalPlayer; }
public class DynamicMap { }
public static class SceneSingleton<T> { public static T i; }
public static class GameManager { public static Player Local; public static void GetLocalPlayer(out Player player) { player = Local; } }
namespace NuclearOption.ModdingApi {
    public enum PermissionLevel { All }
    public enum ChatArgumentType { Text }
    public struct ChatCommandArgument { public string Name; public ChatArgumentType Type; public bool Optional;
        public ChatCommandArgument(string name, ChatArgumentType type, bool optional) { Name = name; Type = type; Optional = optional; } }
}
namespace NuclearOption.Chat {
    public class ChatManager { public static ChatManager i; public static int PublicSends;
        public static bool CanSend(string command, bool a, bool b) => true;
        public static void SendChatMessage(string message, bool team) { PublicSends++; } }
    public struct ChatCommandDescriptor { public string Name, Description; public ChatCommandArgument[] Arguments; public PermissionLevel RequiredPermissionLevel; public string[] Aliases; }
    public class ChatCommand { }
    public static class ChatCommandCollections {
        public static bool ClientCollision, BuiltInCollision;
        public static bool TryGetClientCommand(string name, out ChatCommand command) { command = null; return ClientCollision; }
        public static bool TryGetBuiltInServerCommand(string name, out ChatCommand command) { command = null; return BuiltInCollision; }
    }
    public class ChatCommandManager {
        public static ChatCommandManager i; public bool IsClient, IsServer, HasDescriptor; public ChatCommandDescriptor Descriptor;
        public int Sends; public string LastName, LastArgs;
        public bool TryGetModServerCommandDescriptor(string name, out ChatCommandDescriptor descriptor) { descriptor = Descriptor; return HasDescriptor; }
        public void ExecuteCommandLocal(string name, string args) { Sends++; LastName = name; LastArgs = args; }
    }
}
namespace KellysMEDALSPublicUI { internal class MedalsMfdUi {
    internal static bool Open; internal bool Visible => Open;
    internal MedalsMfdUi(Plugin plugin, Action<string> logger) {} internal void Tick(bool show) {} internal void Close() { Open = false; } internal void Dispose() {}
} }
