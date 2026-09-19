# MEDALS Public UI v0.1.0 — public preview

Adds **MED** to the last vacant left MFD button, using the same native styling as
the TALON/AIRLIFT panels. Browse earned medals, progress toward upcoming awards,
category and PvP/PvE/Universal filters, progress bars and full requirements.

**Requires MEDALS server 1.13.0+. The companion server update is staged separately
and has not been activated as part of this release.** An older server cannot
populate the panel and may show the unsupported request in chat.

Download the **CLIENT.zip**, close the game, and extract its BepInEx folder into
an existing BepInEx 5 Nuclear Option installation. Keep exactly one active copy of
KellysMEDALSPublicUI.dll. This client package contains no server DLL, game assemblies,
databases or credentials. See the README for configuration and removal.

Stats remain server-authoritative and private to the authenticated player. Existing
AIR/TAL and stock buttons are preserved. The client has no local stats database.

Validation: local runtime build, native API signature checks, and 1,527 combined
MEDALS checks passed. In-game visual and multiplayer acceptance remains pending,
so this release is marked as a preview. SHA-256 checksums are attached.
