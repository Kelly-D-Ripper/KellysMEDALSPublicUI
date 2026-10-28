# MEDALS UI 0.1.3 compatibility and privacy checks

Validated on 28 October 2026 against Nuclear Option 0.34.5 client build 26272685.
Assembly-CSharp SHA-256:
`E9E6AC0F6C071359320B67A9A67AFB3B07316DFE78DBAA4760DB7AEEE47340A8`.

## Automated coverage

- Native client/server builds against installed game/BepInEx/Unity assemblies.
- 62 tests compile the actual client Plugin.cs against game doubles: all 32
  host/client/capability/Enabled/visibility combinations, valid native request,
  nonce/renewal, capability removal/restoration, disconnect, remote-to-host switch,
  ten invalid descriptors, command collisions, player/map/chat context changes,
  missing contexts and disable cleanup. Zero ordinary chat sends.
- 413 existing protocol/UI tests: snapshots, replay/stale rejection, session reset,
  filters, MFD ownership/layout, keep-alives, cached views and bounded scheduling.
- Native private receiver, replicated capability lookup, command RPC route and MFD
  contracts. No ChatManager calls in the inspected native command manager or its
  async state machines, and no ordinary-chat send references in the client DLL.
- All 180 game/Unity/Mirage member references resolve; client GUID/version checked.
- Five in-memory negative API tests reject a missing receiver, changed signature,
  changed route, chat fallback and changed MFD field. No game files are edited.
- Private server: 23 tests of the production adapter with game/ledger doubles,
  authenticated sender/nonce checks, targeted replies, throttle and ownership;
  2,320 existing MEDALS tests. No server code is included in this repository.

The capability is compatibility metadata from the connected server, not server
identity certification or a promise about a malicious server. The safeguard removes
ordinary-chat transport rather than hiding local output. An unsupported server is
not probed. Even losing capability between check and execution cannot invoke a
chat fallback through the audited native route.

CI runs game-free tests only and distributes no game files. Native release checks
require a legally installed game and BepInEx. Static/stubbed checks do not prove
runtime Harmony ordering, visuals, gameplay, multiplayer delivery or performance.

## Required real-game acceptance (pending)

1. Single-player and listen-host: open MED, wait beyond two renewals; no internal
   request in chat, no send, explanatory unavailable status.
2. Unsupported multiplayer with a second unmodified client: open/close MED and
   wait; no probe/request visible to either player or sent through ordinary chat.
3. After the private MEDALS 1.13.2+ backend is installed by the server operator,
   join Aryx with two clients. Each panel must receive only its player's records,
   with no raw command text in either client's chat.
4. Exercise Next/Earned/All, category/track filters, scrolling, UI scales and AIR/TAL.
5. Earn an award, check renewal/stale recovery, disconnect/reconnect, switch maps
   and supported/unsupported servers. Old personal records must not carry over.
6. Exercise normal chat and native commands alongside simultaneous MED viewers.

Database impact: none. Configuration impact: none; retain the existing Enabled
setting. The 296-medal catalogue and 52-entry reward allow-list are unchanged.
The separate private server upgrade is required for data. This public release
does not install or restart it; older servers leave the panel safely unavailable.
