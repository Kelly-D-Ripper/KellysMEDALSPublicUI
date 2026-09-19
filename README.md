# Kelly's MEDALS Public UI

An optional personal achievement panel for Nuclear Option, styled to match the
TALON and AIRLIFT map panels. Open the enlarged map and press **MED** on the last
vacant left MFD button.

**Public preview v0.1.0 — requires MEDALS server 1.13.0 or later.**
The client cannot display stats from an older server. The companion server update
is distributed separately; this repository contains the client only.

## Download and install

1. Download `KellysMEDALSPublicUI-0.1.0-CLIENT.zip` from
   [Releases](https://github.com/Kelly-D-Ripper/KellysMEDALSPublicUI/releases).
2. Close Nuclear Option. The client requires an existing BepInEx 5 installation.
3. Extract the ZIP's `BepInEx` folder into your Nuclear Option game directory.
   The DLL should be at `BepInEx/plugins/KellysMEDALSPublicUI/KellysMEDALSPublicUI.dll`.
4. Keep one active copy of the DLL. Put backups outside `BepInEx/plugins`.
5. Join a server running MEDALS 1.13.0+, enlarge the map and press **MED**.

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

The open panel refreshes about every five seconds. It marks data stale after 20
seconds without a complete response and clears data on player/map/connection
changes. Requests share the game's chat rate limit, so a busy chat may delay updates.
On an older server, the panel waits for compatible data and the unsupported request
may appear in chat. Close the panel to stop renewing its subscription.

MED reserves the last vacant extra MFD position. It never replaces the three native
buttons or an occupied mod button. If all extra buttons are occupied, it waits for
a vacancy. Closing the map hides the panel; switching native MFD pages closes it.

The first run creates `BepInEx/config/kelly.nuclearoption.medals.publicui.cfg`.
Set `[General] Enabled = false` to disable the panel. Existing configs and databases
are unaffected, and the client maintains no local stats database.

## Build

Use a legal local Nuclear Option installation with BepInEx 5. Game assemblies are
referenced for compilation only and must not be redistributed.

With Visual Studio 2022 Build Tools on Windows:

```powershell
./build.ps1 -GameDir 'C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option'
```

Or use the .NET SDK with the .NET Framework 4.7.2 targeting pack:

```powershell
$env:NUCLEAR_OPTION_DIR = 'C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option'
dotnet build KellysMEDALSPublicUI.csproj -c Release
```

Pure protocol, filter and MFD slot tests require only the .NET 8 SDK:

```text
dotnet run --project tests/LogicTests.csproj -c Release
```

## Preview status

The client builds against the local game API, and the combined MEDALS test suite
passed 1,527 checks before packaging. The adapter's private MFD/chat signatures were
checked against the installed game assemblies. These checks do not prove in-game
rendering or multiplayer behaviour; that acceptance remains pending for this preview.
Please include game version, plugin version, resolution/UI scale and relevant
BepInEx log excerpts when reporting a problem. Remove personal information first.

MIT licensed. The native MFD adapter derives from Kelly's TALON/AIRLIFT client work;
copyright notices are retained in [LICENSE](LICENSE). Game UI assets remain owned by
their respective creators and are accessed at runtime, not bundled here.
