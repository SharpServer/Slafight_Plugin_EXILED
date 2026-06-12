namespace Slafight_Plugin_EXILED.API.Features;

public static class RoleSpawnTimings
{
    public const float NextFrame = 0.02f;
    public const float AfterRoleSet = 0.05f;
    public const float FastSpawnFinalize = 0.1f;
    public const float AfterSpawnFinalize = 0.25f;
    public const float RoleStateReapply = 0.25f;
    public const float RestoreRoleState = 1f;
    public const float Scp079Setup = 1f;
    public const float Scp3125Startup = 3f;
    public const float SpawnPointPollInterval = 0.25f;
    public const float TeamNpcSpawn = 0.6f;
    public const float TeamNpcPostSpawnSetup = 0.6f;
    public const float TeamNpcCleanupAfterRoleChange = 1f;
    public const float FirstRolesRoundUnlockFallback = 5f;
    public const float SpawnSystemDefaultWaveReset = NextFrame;
    public const float CustomRoleRemovalCleanup = 1f;
    public const float HudRecreateAfterClear = 0.25f;
}
