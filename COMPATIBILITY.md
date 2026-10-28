# MEDALS UI 0.1.2 compatibility checks

Validated on 28 October 2026 against Nuclear Option 0.34.5 client build 26272685.
The game's Assembly-CSharp SHA-256 was
`E9E6AC0F6C071359320B67A9A67AFB3B07316DFE78DBAA4760DB7AEEE47340A8`.

## Automated coverage

- Native client build against the installed game, BepInEx and Unity assemblies.
- 413 client logic checks: complete/partial/out-of-order snapshots, stale data,
  replay rejection, disconnect reset, filters, revoked awards, MFD slot ownership,
  viewport layout, cached rows, keep-alive behaviour and bounded reply scheduling.
- Five native chat method signatures and three MFD field contracts.
- The direct SendChatMessage path still calls CanSend and CmdSendChatMessage;
  the compiled client uses that path rather than the typed command parser.
- All 167 referenced game/Unity/Mirage members resolve; client GUID/version checked.
- Four in-memory fault injections: missing receiver, changed signature, changed
  request route and changed MFD field. All are rejected without editing game files.
- Companion MEDALS server: 2,320 rule, ledger, commander, UI and idempotency checks
  plus assembly/AWACS contract validation pass against the updated game.

Native checks require a legally installed game and BepInEx; CI does not distribute
game assemblies and runs only the game-free logic suite. Static checks cannot prove
runtime Harmony ordering, rendering, all game behaviours or network delivery.

## Multiplayer acceptance checklist

The previous 0.1.1 client's local log shows loading, MED slot registration and panel
opening. The rebuilt 0.1.2 client still needs the following in-game acceptance:

1. Join Aryx Shipyard, open MED and confirm your own medal records arrive privately.
2. Exercise Next Awards, Earned, All, category/track filters and scrolling.
3. Check MED beside AIR/TAL, different UI scales and occupied extra MFD slots.
4. Earn an award and confirm progress/earned status refresh; verify stale-data recovery.
5. Change maps, reconnect and switch player context: old records must not persist.
6. Exercise normal chat and typed commands alongside MED; subscription packets must
   not appear publicly. Test busy chat and simultaneous viewers for responsive UI.

Database impact: none. Configuration impact: none; retain the existing Enabled
setting. No change to the server protocol, 296-medal catalogue or 52-entry reward
allow-list. No server installation or restart is required for this client release.
