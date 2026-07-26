# Repository Instructions

## Scope

This repository contains the Sharp Server SCP: Secret Laboratory plugin built on
EXILED. It targets `net48` and is tightly integrated with the server's ProjectMER,
HintServiceMeow, SNAPI-HSM, audio, map, and Unity asset stack.

Treat the current source, project files, local dependency assemblies, and sibling
repositories as the source of truth. Do not assume that upstream ProjectMER,
HintServiceMeow, EXILED examples, old MapEditorReborn APIs, or remembered version
numbers match this server.

Before changing anything:

1. Check this repository's Git status.
2. Check every sibling repository that will be read from or modified.
3. Read the relevant `.csproj`, source entry points, registration, and cleanup paths.
4. Inspect exact referenced assemblies with `ilspycmd` when source is unavailable.
5. Preserve unrelated local changes and keep commits separated by repository.

## Required build

Before completing any task, build the solution in Release mode:

```powershell
dotnet build .\Slafight_Plugin_EXILED.sln --configuration Release
```

The project targets EXILED `9.14.2`, copies the built plugin automatically to
`%APPDATA%\EXILED\Plugins\7777`, and copies its managed runtime dependencies to
`%APPDATA%\EXILED\Plugins\dependencies`.

Never report a successful build or deployment without checking the command result.
For deployment verification, compare the output and destination timestamps or
SHA-256 hashes.

## Family repositories

These repositories form one server feature stack. They are separate Git
repositories and must not be committed as if they were one working tree. Do not
assume where a contributor cloned them; discover sibling checkouts from the current
workspace or ask for their locations when they are required.

| Component | Repository | Responsibility |
| --- | --- | --- |
| Slafight | `https://github.com/SharpServer/Slafight_Plugin_EXILED` | Main EXILED plugin, roles, items, events, HUD, maps, and server behavior |
| ProjectMER | `https://github.com/SharpServer/ProjectMER` | LabAPI schematic loader, map objects, markers, animation, and spawn/update/despawn behavior |
| HintServiceMeow | `https://github.com/SharpServer/HintServiceMeow` | Shared hint compositor and the EXILED output plugin |
| Unity assets | `https://github.com/SharpServer/SL-CustomObjects-dev` | Unity 2021.3.17f1 source for ProjectMER schematics and asset bundles |
| MapWorks | `https://github.com/SharpServer/ProjectMER-MapWorks` | Live `Maps`, `Schematics`, and exported asset bundles; the configured export directory may itself be this Git working tree |
| SL references | `https://github.com/SharpServer/SL_References` | Exact local compile/decompilation assemblies shared by the family projects |

Important boundaries:

- Unity exports schematics directly into the MapWorks working tree configured by
  `Assets/config.json`.
- Editing Unity source does not automatically mean exported MapWorks data changed,
  and editing MapWorks JSON does not update Unity source.
- Each repository's `.claude\settings.local.json` is machine-local permission state.
  Do not commit it unless the user explicitly requests that exact file.
- `SL_References` is a development reference mirror, not a runtime plugin directory.

## Build and deployment matrix

### Slafight

```powershell
dotnet build .\Slafight_Plugin_EXILED.sln --configuration Release
```

Automatic destinations:

- `%APPDATA%\EXILED\Plugins\7777\Slafight_Plugin_EXILED.dll`
- `%APPDATA%\EXILED\Plugins\dependencies\` for copied managed dependencies

### ProjectMER

Run from the ProjectMER checkout:

```powershell
dotnet build .\ProjectMER.csproj --configuration Release
```

Its Release target automatically copies `ProjectMER.dll` to:

- `%APPDATA%\SCP Secret Laboratory\LabAPI\plugins\7777`
- `%SL_References%`

### HintServiceMeow

Run from the HintServiceMeow checkout:

```powershell
dotnet build .\HintServiceMeow.sln --configuration Release
dotnet build .\HintServiceMeow\HintServiceMeow.csproj --configuration Exiled
```

The runtime assembly is `bin\Exiled\HintServiceMeow-Exiled.dll`. HSM has no local
post-build deployment target, so copy that exact file manually to:

- `%APPDATA%\EXILED\Plugins\7777`
- `%SL_References%`

### Unity schematics

- Required editor: Unity `2021.3.17f1`.
- Project: the contributor's `SL-CustomObjects-dev` checkout.
- Export destination:
  `%APPDATA%\SCP Secret Laboratory\LabAPI\configs\ProjectMER\Schematics`.
- After editor script changes, wait for compilation and check the Unity Console.
- Validate generated JSON and asset bundles before committing the MapWorks repository.

## Runtime layout for port 7777

```text
%APPDATA%\EXILED\Plugins\7777\
  Slafight_Plugin_EXILED.dll
  HintServiceMeow-Exiled.dll
  SNAPI-HSM.dll

%APPDATA%\EXILED\Plugins\dependencies\
  0Harmony.dll
  AudioPlayerApi.dll
  SCPSLAudioApi.dll
  ...managed/audio dependencies

%APPDATA%\SCP Secret Laboratory\LabAPI\plugins\7777\
  ProjectMER.dll
  MEROptimizerLabAPI.dll

%APPDATA%\SCP Secret Laboratory\LabAPI\configs\ProjectMER\
  Maps\
  Schematics\
```

Do not copy a LabAPI plugin into EXILED or an EXILED plugin into LabAPI merely
because both are used by Slafight.

## Network and lifecycle invariants

This server creates real players, dummy NPCs, internal NPCs, and partially
authenticated hubs during round transitions. Network code is therefore sensitive
to object lifetime and authentication state.

- For client-bound messages, `connection.isReady` alone is insufficient. A real
  client must also be `ClientInstanceMode.ReadyClient`.
- HSM is the central last-line guard for hint delivery. Do not scatter redundant
  NPC checks through every HUD loop.
- Use `PlayerSafetyExtensions.IsSafePlayer` / `IsNotHost` for Slafight player
  targeting. Non-NPC players must be verified; legitimate NPC flows remain
  supported.
- Do not use a `ReferenceHub` or another destroyed Unity object as a delayed
  `HashSet`/`Dictionary` key. Prefer stable `netId` keys, verify object identity
  when the callback runs, and invalidate pending work on round restart.
- Preserve registration symmetry and round cleanup for events, Harmony patches,
  coroutines, dictionaries, spawned objects, and network state.
- Avoid adding `IsNPC` filters to non-player-facing systems without evidence; many
  custom roles, turrets, hitboxes, and schematic interactions intentionally use
  NPCs.

## ProjectMER and schematic rules

- Search the exact ProjectMER fork before using an API; this fork contains
  server-specific bridges and object-prefab metadata that upstream examples may
  not have.
- Negative-scale normalization is safe only where the occupied primitive geometry
  is preserved. Do not blindly take the absolute scale of parent transforms; that
  mirrors child positions and can break intentional shear hierarchies.
- Plane/Quad conversions must preserve local axes, dimensions, normal direction,
  collider behavior, and children. Current ProjectMER converts eligible leaf
  Planes to Quads at runtime.
- Do not bulk rewrite or re-export all MapWorks data for a narrow source change.
  Inspect generated diffs and keep Unity source and exported data commits separate.

## Logs and diagnosis

Local paths:

- Main server log:
  `%APPDATA%\SCP Secret Laboratory\LocalAdminLogs\7777\`
- Client log:
  `%USERPROFILE%\AppData\LocalLow\Northwood\SCPSL\Player.log`
- EXILED configuration:
  `%APPDATA%\EXILED\Configs\Plugins\<plugin>\7777.yml`
- LabAPI configuration:
  `%APPDATA%\SCP Secret Laboratory\LabAPI\configs\`

For disconnects, protocol errors, rendering errors, or client crashes, inspect the
client `Player.log` together with the matching LocalAdmin log. For live incidents,
the production server runs on a remote VPS; local logs and local plugin folders do
not prove what the VPS loaded. Request the VPS LocalAdmin log and confirm its DLL
versions or hashes.

## Git and completion

- Keep Slafight, ProjectMER, HSM, Unity assets, MapWorks, and SL references as
  independent commits and pushes.
- Never mix pre-existing user work into a new implementation commit without
  explicit direction. If the user requests a clean baseline, commit and push that
  existing work first as a separate commit.
- Do not reset, clean, discard, or rewrite unrelated changes.
- Before finishing, review every affected repository's final diff/status, run the
  required builds, verify deployment, and state any behavior that still requires a
  server restart or remote VPS validation.
