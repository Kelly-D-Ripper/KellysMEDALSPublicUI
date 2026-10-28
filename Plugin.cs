using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using KellysMedalsUi;
using Mirage;
using NuclearOption.Chat;
using NuclearOption.Networking;
using NuclearOption.ModdingApi;
using UnityEngine;

namespace KellysMEDALSPublicUI;

[BepInPlugin(PluginGuid, "Kelly's MEDALS Public UI", "0.1.3")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "kelly.nuclearoption.medals.publicui";
    private static Plugin? instance;
    private Harmony? harmony;
    private ConfigEntry<bool>? show;
    private MedalsMfdUi? mfd;
    private Player? player;
    private ChatManager? chat;
    private DynamicMap? map;
    private ChatCommandManager? commands;
    internal string TransportStatus { get; private set; } = "Connect to a supported dedicated server";
    private float nextRequest;
    internal readonly MedalsUiSession Session = new MedalsUiSession();

    private void Awake()
    {
        if (Application.isBatchMode) { enabled = false; return; }
        show = Config.Bind("General", "Enabled", true, "Show the MEDALS stats panel on the last vacant left MFD button.");
        instance = this;
        try
        {
            var receive = AccessTools.Method(typeof(ChatManager), "UserCode_RpcTargetServerMessage_-537879009",
                new[] { typeof(INetworkPlayer), typeof(string), typeof(bool) });
            if (receive == null) throw new MissingMethodException("Native private server-message receiver changed.");
            harmony = new Harmony(PluginGuid);
            harmony.Patch(receive, prefix: new HarmonyMethod(typeof(Plugin), nameof(Receive)));
            mfd = new MedalsMfdUi(this, message => Logger.LogInfo(message));
            Logger.LogInfo("MEDALS panel ready. Private command transport requires MEDALS server 1.13.2+. No chat fallback.");
        }
        catch (Exception error) { harmony?.UnpatchSelf(); Logger.LogError(error); enabled = false; }
    }
    private bool Context()
    {
        GameManager.GetLocalPlayer(out Player current);
        var currentChat = ChatManager.i;
        var currentMap = SceneSingleton<DynamicMap>.i;
        var currentCommands = ChatCommandManager.i;
        if (current != player || currentChat != chat || currentMap != map || currentCommands != commands)
        {
            Session.Reset(); nextRequest = 0;
            player = current; chat = currentChat; map = currentMap; commands = currentCommands;
        }
        string status;
        if (commands != null && commands.IsServer)
            status = "Unavailable in single-player / local host";
        else if (player == null || !player.IsLocalPlayer || chat == null || map == null || commands == null || !commands.IsClient)
            status = "Connect to a supported dedicated server";
        else if (!SupportsPrivateUi(commands))
            status = "Requires MEDALS server 1.13.2+";
        else status = "";
        if (status.Length != 0 && (Session.Request != null || Session.Rows != null)) { Session.Reset(); nextRequest = 0; }
        TransportStatus = status;
        return status.Length == 0;
    }
    private static bool SupportsPrivateUi(ChatCommandManager manager)
    {
        // Replicated by this server, never inferred from local DLLs or a chat probe.
        // ExecuteCommandLocal checks client/built-in commands first: reject collisions.
        if (ChatCommandCollections.TryGetClientCommand(MedalsUiProtocol.ServerCommand, out _) ||
            ChatCommandCollections.TryGetBuiltInServerCommand(MedalsUiProtocol.ServerCommand, out _) ||
            !manager.TryGetModServerCommandDescriptor(MedalsUiProtocol.ServerCommand, out var descriptor)) return false;
        var args = descriptor.Arguments;
        return descriptor.Name == MedalsUiProtocol.ServerCommand && descriptor.Description == MedalsUiProtocol.Capability &&
            (int)descriptor.RequiredPermissionLevel == 0 && (descriptor.Aliases == null || descriptor.Aliases.Length == 0) &&
            args != null && args.Length == 1 && args[0].Name == "request" &&
            args[0].Type == ChatArgumentType.Text && !args[0].Optional;
    }
    private void Update()
    {
        bool connected = Context();
        mfd?.Tick(show?.Value == true);
        if (!connected || show?.Value != true || mfd?.Visible != true || Time.unscaledTime < nextRequest) return;
        string id = Session.Request == null || !Session.Fresh(Time.unscaledTime) ? Guid.NewGuid().ToString("N") : Session.Request;
        Session.Query(id);
        nextRequest = Time.unscaledTime + 30;
        // Native command RPC, NOT SendChatMessage: even capability loss has no chat fallback.
        commands!.ExecuteCommandLocal(MedalsUiProtocol.ServerCommand, id);
    }
    internal void MfdOpened() { if (!Session.Fresh(Time.unscaledTime)) nextRequest = Math.Min(nextRequest, Time.unscaledTime); }
    private static bool Receive(string message)
    {
        if (message == null || !message.StartsWith(MedalsUiProtocol.Prefix, StringComparison.Ordinal)) return true;
        if (instance != null && instance.isActiveAndEnabled && instance.Context())
        {
            try { instance.Session.Receive(message, Time.unscaledTime); }
            catch (Exception error) { instance.Logger.LogWarning("Ignored invalid MEDALS reply: " + error.GetType().Name); }
        }
        return false;
    }
    private void OnDisable() { mfd?.Close(); Session.Reset(); nextRequest = 0; }
    private void OnDestroy() { mfd?.Dispose(); harmony?.UnpatchSelf(); if (instance == this) instance = null; }
}
