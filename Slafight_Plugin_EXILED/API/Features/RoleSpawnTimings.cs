namespace Slafight_Plugin_EXILED.API.Features;

public static class RoleSpawnTimings
{
    public const float NextFrame = 0.02f;
    public const float AfterRoleSet = 0.05f;
    public const float AfterSpawnFinalize = 0.1f;
    public const float RestoreRoleState = 0.25f;
    public const float TeamNpcSpawn = 0.25f;
    public const float TeamNpcPostSpawnSetup = 0.6f;
    public const float TeamNpcCleanupAfterRoleChange = 0.25f;
    public const float FirstRolesRoundUnlockFallback = 1f;
    public const float SpawnSystemDefaultWaveReset = NextFrame;
    public const float CustomRoleRemovalCleanup = 1f;
    public const float HudRecreateAfterClear = 0.25f;
}
