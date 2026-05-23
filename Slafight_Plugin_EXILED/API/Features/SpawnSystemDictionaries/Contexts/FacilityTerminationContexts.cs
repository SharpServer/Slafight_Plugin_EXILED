using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Features.SpawnSystemDictionaries.Contexts;

public static class FacilityTerminationContexts
{
    public static void Register()
    {
        UnitPackRegistry.TryGet("FT_LastOperation", out var lastOpPack);
        UnitPackRegistry.TryGet("FT_GoC",          out var gocPack);
        UnitPackRegistry.TryGet("FT_Chaos",        out var chaosPack);
        UnitPackRegistry.TryGet("MTF_Lws",         out var lwsPack);
        UnitPackRegistry.TryGet("SecurityTeam",    out var securityTeamPack);
        UnitPackRegistry.TryGet("ChaosAgents",     out var chaosAgentsPack);

        var ctx = new SpawnContext(
            "FacilityTerminationCustom",
            // FoundationStaffWaveWeights
            new() 
            { 
                { SpawnTypeId.MtfLastOperationNormal, 100 },
            },
            // FoundationEnemyWaveWeights
            new() 
            { 
                { SpawnTypeId.GoiChaosNormal, 30 },
                { SpawnTypeId.GoiGoCNormal,   70 },
            },
            // FoundationStaffMiniWaveWeights
            new()
            {
                { SpawnTypeId.MtfLastOperationBackup, 100 },
            },
            // FoundationEnemyMiniWaveWeights
            new()
            {
                { SpawnTypeId.GoiChaosBackup, 30 },
                { SpawnTypeId.GoiGoCBackup,   70 },
            },
            lastOpPack,
            lwsPack,
            gocPack,
            chaosPack,
            securityTeamPack,
            chaosAgentsPack
        );

        SpawnContextRegistry.Register(ctx);
    }
}
