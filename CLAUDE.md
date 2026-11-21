# Claude Working Instructions

Read `AGENTS.md` completely before working in this repository. It is the canonical
description of the family repositories, build commands, deployment destinations,
network invariants, and log locations.

## Working checklist

1. Run `git status` in Slafight and every family repository involved in the task.
2. Verify APIs against current source, `.csproj` files, or exact assemblies in
   `%SL_References%`; do not rely on remembered EXILED/ProjectMER APIs.
3. Make the smallest coherent change in the repository that owns the behavior.
4. Keep source changes, Unity asset changes, and generated MapWorks data in
   separate commits.
5. Run the applicable component build, then always run:

   ```powershell
   dotnet build .\Slafight_Plugin_EXILED.sln --configuration Release
   ```

6. Confirm copied DLL timestamps or hashes in the port `7777` runtime folders.
7. Review the final diff and Git status before committing or reporting completion.

## Claude-specific cautions

- Never commit `.claude/settings.local.json`; it contains machine-local permission
  state and absolute paths.
- `%APPDATA%\SCP Secret Laboratory\LabAPI\configs\ProjectMER` is not merely generated
  output. It is the live `ProjectMER-MapWorks` Git repository.
- Unity exports directly into that MapWorks repository. Check both Git working
  trees after exporting.
- HSM runtime builds use the `Exiled` configuration and require manual deployment.
- ProjectMER and Slafight Release builds have automatic copy targets; verify them
  instead of copying a second, possibly stale assembly.
- The production server is a remote VPS. Local runtime folders are a test/deployment
  mirror, not proof of production state.
- Do not weaken authentication, `netId`, role-sync, round-cleanup, or NPC lifecycle
  guards for convenience. When touching these paths, follow the invariants in
  `AGENTS.md`.

## Custom zone / room volumes

- Server-authored spatial volumes live in `API/Features/CustomRoom.cs` and
  `API/Features/CustomRooms.cs` (Waypoint-style static registry, immutable
  snapshot, no `Config.cs` fields). Use `CustomRooms.TryGetAt/TryGet`,
  `IsInRoom`, `IsInZone`, and `GetZoneOverride` for queries.
- Authoring is via a ProjectMER ObjectPrefab marker with `Zone` (`LCZ`/`HCZ`/
  `EZ`/`Surface`/`Pocket`/`Other`), optional `RoomId`, and optional `Size`;
  see `CustomMaps/ObjectPrefabs/CustomRoomMarkerPrefab.cs`. No ProjectMER
  rebuild is needed for this feature.
- Vanilla decontamination checks `MapGeneration.RoomUtils.GetLastKnownZone()`,
  not `GetCurrentZone()`. `Patches/CustomRoomZonePatch.cs` patches all three
  `RoomUtils` zone queries together; never patch only one of them.
- Exiled `Player.Zone` is **not** affected by the patch (it reads
  `CurrentRoom.Zone`). Callers that need custom-zone awareness must use the
  `CustomRooms` API. `ZoneType.Pocket` maps to `FacilityZone.Other`.

