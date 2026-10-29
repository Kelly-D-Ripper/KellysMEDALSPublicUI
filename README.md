# Kelly's MEDALS Public UI

**Public UI v0.1.4 — for the Aryx Shipyard Server.**
The client cannot display stats from any other server. The companion server update
is not distributed; this repository contains the client only.

**Requires its private MEDALS 1.13.2+ backend update.** Until that separate server
update is activated, the panel stays unavailable and sends nothing. This release
does not install the server.

## Download and install

1. Download `KellysMEDALSPublicUI-0.1.4-CLIENT.zip` from
   [Releases](https://github.com/Kelly-D-Ripper/KellysMEDALSPublicUI/releases).
2. Close Nuclear Option. The client requires an existing BepInEx 5 installation.
3. Extract the ZIP's `BepInEx` folder into your Nuclear Option game directory.
   The DLL should be at `BepInEx/plugins/KellysMEDALSPublicUI/KellysMEDALSPublicUI.dll`.
4. Keep one active copy of the DLL. Put backups outside `BepInEx/plugins`.
5. Join Aryx Shipyard after its MEDALS 1.13.2+ update, enlarge the map and press **MED**.

The download contains no server plugin, game files, BepInEx, SQLite or other mods.
Version 0.1.4 embeds a DOORMAN entry with the mod description, author, GitHub and

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

## Build and test

With the game and BepInEx 5 installed, run `./build.ps1`, `./tests/run.ps1` and
`./tests/Test-GameApi.ps1 -SelfTest`. `./package.ps1` runs all three and packages
only the client DLL and documentation. The native build uses Visual Studio 2022
Build Tools; `-GameDir` selects the game directory. The game-free logic suite also
runs with `dotnet run --project tests/LogicTests.csproj` on .NET 8.

MIT licensed. The native MFD adapter derives from Kelly's TALON/AIRLIFT client work;
copyright notices are retained in [LICENSE](LICENSE). Game UI assets remain owned by
their respective creators and are accessed at runtime, not bundled here.
