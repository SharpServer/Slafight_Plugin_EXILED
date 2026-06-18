using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.API.Features.SpawnSystemDictionaries.Contexts;

public static class DefaultContexts
{
    public static void Register(SpawnSystem.SpawnConfig config)
    {
        // UnitPack は先に登録済み前提
        UnitPackRegistry.TryGet("MTF_NtfNormal",      out var ntfNormalPack);
        UnitPackRegistry.TryGet("MTF_HDNormal",       out var hdNormalPack);
        UnitPackRegistry.TryGet("MTF_SneNormal",      out var sneNormalPack);
        UnitPackRegistry.TryGet("GOI_ChaosNormal",    out var chaosNormalPack);
        UnitPackRegistry.TryGet("GOI_FifthistNormal", out var fifthNormalPack);

        UnitPackRegistry.TryGet("MTF_NtfBackup",      out var ntfBackupPack);
        UnitPackRegistry.TryGet("MTF_HDBackup",       out var hdBackupPack);
        UnitPackRegistry.TryGet("MTF_SneBackup",      out var sneBackupPack);
        UnitPackRegistry.TryGet("MTF_Lws",            out var lwsPack);
        UnitPackRegistry.TryGet("MTF_Rrh",            out var rrhPack);
        UnitPackRegistry.TryGet("GOI_ChaosBackup",    out var chaosBackupPack);
        UnitPackRegistry.TryGet("GOI_FifthistBackup", out var fifthBackupPack);
        UnitPackRegistry.TryGet("GOI_Initiative", out var initiativePack);
        
        UnitPackRegistry.TryGet("SecurityTeam",       out var securityTeamPack);
        UnitPackRegistry.TryGet("ChaosAgents",        out var chaosAgentsPack);

        var defaultContext = new SpawnContext(
            "Default",
            config.FoundationStaffWaveWeights,
            config.FoundationEnemyWaveWeights,
            config.FoundationStaffMiniWaveWeights,
            config.FoundationEnemyMiniWaveWeights,
            ntfNormalPack,
            ntfBackupPack,
            hdNormalPack,
            hdBackupPack,
            sneNormalPack,
            sneBackupPack,
            lwsPack,
            rrhPack,
            chaosNormalPack,
            chaosBackupPack,
            fifthNormalPack,
            fifthBackupPack,
            initiativePack,
            
            securityTeamPack,
            chaosAgentsPack
        );

        SpawnContextRegistry.Register(defaultContext);
    }
}
