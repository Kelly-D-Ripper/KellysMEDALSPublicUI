# Kelly's MEDALS Public UI

**Public preview v0.1.2 — only works on the Aryx Shipyard Server.**
The client cannot display stats from any other server. The companion server update
is not distributed; this repository contains the client only.

## Download and install

1. Download `KellysMEDALSPublicUI-0.1.2-CLIENT.zip` from
   [Releases](https://github.com/Kelly-D-Ripper/KellysMEDALSPublicUI/releases).
2. Close Nuclear Option. The client requires an existing BepInEx 5 installation.
3. Extract the ZIP's `BepInEx` folder into your Nuclear Option game directory.
   The DLL should be at `BepInEx/plugins/KellysMEDALSPublicUI/KellysMEDALSPublicUI.dll`.
4. Keep one active copy of the DLL. Put backups outside `BepInEx/plugins`.
5. Join a server running MEDALS 1.13.1+, enlarge the map and press **MED**.

The download contains no server plugin, game files, BepInEx, SQLite or other mods.
TALON and AIRLIFT are optional; their MFD buttons are preserved when present.
Do not install this client DLL on a dedicated server. Remove it with the game
closed to uninstall. Checksums accompany the release.

## Your medal record

- **Next Awards:** unearned medals, closest completion first.
- **Earned:** awarded medals, newest first.
- **All Medals:** the complete server catalogue, including revoked medals.
- Cycle **Category** and **Track** to filter combat, mastery, TALON Command and
  other server-defined categories, or PvP/PvE/Universal awards.
- Browse five medals per page. Select a medal for requirements and award date;
  scroll the details box for long descriptions.

The server supplies your own committed record. The client cannot grant medals,
change progress or claim rewards. Progress bars show recorded metrics; reaching
a threshold alone is not treated as an earned award. Revoked medals are labelled
and excluded from Next Awards and Earned.

Version 0.1.1 caches unchanged medal pages and uses small keep-alive replies instead
of downloading the full catalogue repeatedly. Idle payload fell by approximately
99.25% in the synthetic benchmark. Server replies and database admissions are bounded
globally, with gameplay facts taking priority over UI reads. See
[performance measurements and their limits](PERFORMANCE.md).

MED reserves the last vacant extra MFD position. It never replaces the three native
buttons or an occupied mod button. If all extra buttons are occupied, it waits for
a vacancy. Closing the map hides the panel; switching native MFD pages closes it.

The first run creates `BepInEx/config/kelly.nuclearoption.medals.publicui.cfg`.
Set `[General] Enabled = false` to disable the panel. Existing configs and databases
are unaffected, and the client maintains no local stats database.

## Nuclear Option 0.34.5 compatibility

Version 0.1.2 is rebuilt and checked against Nuclear Option 0.34.5, Steam client
build **26272685**. It preserves the direct private request route, which does not
use the game's new typed slash-command parser. Medal rules, protocol, cached views
and the MEDALS server 1.13.1+ requirement are unchanged. No database or configuration
migration is needed. This is a compatibility rebuild, not a fix for a reproduced
MEDALS gameplay failure.

413 client logic checks, native hook/MFD checks and 167 game/Unity/Mirage member
bindings pass. The audit also rejects four deliberately broken API contracts.
Full multiplayer interaction and frame-time acceptance of this rebuilt client
remain pending; preview status is retained. See [compatibility](COMPATIBILITY.md).

## Build and test

With the game and BepInEx 5 installed, run `./build.ps1`, `./tests/run.ps1` and
`./tests/Test-GameApi.ps1 -SelfTest`. `./package.ps1` runs all three and packages
only the client DLL and documentation. The native build uses Visual Studio 2022
Build Tools; `-GameDir` selects the game directory. The game-free logic suite also
runs with `dotnet run --project tests/LogicTests.csproj` on .NET 8.

MIT licensed. The native MFD adapter derives from Kelly's TALON/AIRLIFT client work;
copyright notices are retained in [LICENSE](LICENSE). Game UI assets remain owned by
their respective creators and are accessed at runtime, not bundled here.
