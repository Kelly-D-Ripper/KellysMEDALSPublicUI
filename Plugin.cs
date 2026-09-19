using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using KellysMedalsUi;
using Mirage;
using NuclearOption.Chat;
using NuclearOption.Networking;
using UnityEngine;

namespace KellysMEDALSPublicUI;

[BepInPlugin(PluginGuid, "Kelly's MEDALS Public UI", "0.1.1")]
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
            Logger.LogInfo("MEDALS panel ready. Open MED on the map; requires MEDALS server 1.13.1+.");
        }
        catch (Exception error) { harmony?.UnpatchSelf(); Logger.LogError(error); enabled = false; }
    }
    private bool Context()
    {
        GameManager.GetLocalPlayer(out Player current);
        var currentChat = ChatManager.i;
        var currentMap = SceneSingleton<DynamicMap>.i;
        if (current != player || currentChat != chat || currentMap != map)
        {
            Session.Reset(); nextRequest = 0;
            player = current; chat = currentChat; map = currentMap;
        }
        return player != null && player.IsLocalPlayer && chat != null && map != null;
    }
    private void Update()
    {
        bool connected = Context();
        mfd?.Tick(show?.Value == true);
        if (!connected || show?.Value != true || mfd?.Visible != true || Time.unscaledTime < nextRequest) return;
        string id = Session.Request == null || !Session.Fresh(Time.unscaledTime) ? Guid.NewGuid().ToString("N") : Session.Request;
        string command = MedalsUiProtocol.CompactCommand + id;
        // Shares the native chat budget with AIRLIFT. A renewable subscription avoids polling.
        if (!ChatManager.CanSend(command, false, false)) { nextRequest = Time.unscaledTime + 3; return; }
        Session.Query(id);
        nextRequest = Time.unscaledTime + 30;
        ChatManager.SendChatMessage(command, false);
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
