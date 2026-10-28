# MEDALS Public UI v0.1.3 — internal-command chat safeguard

- Removes all ordinary-chat request sending; there is no chat fallback or capability probe.
- Uses the game's native command RPC only when the current dedicated server advertises
  the exact MEDALS capability. Local client/built-in command collisions are rejected.
- Single-player, listen hosts, unsupported/old servers and disconnected contexts send
  nothing. The panel shows why it is unavailable and discards the old session on changes.
- Requires the separately managed, private MEDALS server **1.13.2+** update. Until
  that update is activated, the panel is unavailable, including on an older Aryx backend.
- Database impact: none. Configuration impact: none. Preserve the existing Enabled setting.

Download `KellysMEDALSPublicUI-0.1.3-CLIENT.zip`, close the game, and replace the
client DLL. Keep one active copy; backups belong outside the plugins directory.
Only the UI and public documentation are included, never server code or player data.

Validation: 62 actual-plugin tests with simulated game contexts, 413 existing client
checks, native command-route inspection, 180 bindings and five negative API tests.
The private server adapter also builds and passes 23 focused tests plus 2,320 existing
MEDALS checks. These do not prove real-game/network behaviour: retain preview status
pending single-player, listen-host and supported/unsupported two-client acceptance.
No installation or server restart was performed as part of this release.

## Historical v0.1.2 — Nuclear Option 0.34.5 compatibility

The ordinary-chat route described below is superseded by 0.1.3. Its apparent privacy
depended on a supporting server intercepting the request; it was unsafe on unsupported sessions.

Client-only compatibility rebuild for Nuclear Option 0.34.5, Steam client build
26272685. For the Aryx Shipyard Server; requires MEDALS server 1.13.1 or newer.

- Preserves the direct private MEDALS request route through the native network
  chat API. It does not depend on the redesigned typed slash-command parser.
- Adds repeatable checks for native chat hooks, request routing, MFD fields,
  client metadata and 167 game/Unity/Mirage member bindings. Four fault-injection
  cases verify that incompatible APIs are rejected by the release audit.
- Adds a standalone client package script and game-free CI logic tests.
- Keeps all medal definitions, rewards, network protocol, cached views and settings
  unchanged. Database impact: none. Configuration impact: none.

Download `KellysMEDALSPublicUI-0.1.2-CLIENT.zip`, close Nuclear Option and replace
the client DLL in `BepInEx/plugins/KellysMEDALSPublicUI/`. Keep one active copy;
retain backups outside the plugins directory. Preserve your existing configuration.
No server DLL, game assembly, database, credentials or third-party mod is bundled.

Validation: 413 client logic checks and native compatibility checks pass against
the updated game. The companion server's 2,320 MEDALS regression checks also pass.
This is not a fix for a reproduced MEDALS gameplay fault. Existing 0.1.1 runtime
logs show loading, MFD registration and panel opening, but do not prove the rebuilt
0.1.2 client's full multiplayer behaviour or frame-time performance. This release
retains preview status pending that acceptance. See COMPATIBILITY.md for the checks.

## Previous release: v0.1.1 performance preview (September 2026)

Adds **MED** to the last vacant left MFD button, using the same native styling as
the TALON/AIRLIFT panels. Browse earned medals, progress toward upcoming awards,
category and PvP/PvE/Universal filters, progress bars and full requirements.

**For the Aryx Shipyard Server only. Requires its MEDALS server 1.13.1+ backend. The server update is staged
and has not been activated as part of this release.** An older server cannot
populate the panel and may show the unsupported request in chat.

Download the **CLIENT.zip**, close the game, and extract its BepInEx folder into
an existing BepInEx 5 Nuclear Option installation. Keep exactly one active copy of
KellysMEDALSPublicUI.dll. This client package contains no server DLL, game assemblies,
databases or credentials. See the README for configuration and removal.

Stats remain server-authoritative and private to the authenticated player. Existing
AIR/TAL and stock buttons are preserved. The client has no local stats database.

The panel now caches unchanged rows and text layout. Small keep-alive replies replace
full catalogue downloads when stats have not changed: about 99.25% smaller idle
payload in the synthetic benchmark. The server limits replies to four packets per
frame globally, spreads database reads across viewers and yields to queued gameplay
facts. Full data is resent for changed stats and stale/reconnect recovery.

Desktop synthetic p95 measurements: 0.398 ms for full client decode and 0.912 ms for
an indexed worker snapshot. These are not Unity FPS measurements. See PERFORMANCE.md
for the workload, regression coverage and remaining in-game profiling.

Validation: local runtime build, native API signature checks, and 2,320 combined
MEDALS checks passed. In-game visual and multiplayer acceptance remains pending,
so this release is marked as a preview. SHA-256 checksums are attached.
