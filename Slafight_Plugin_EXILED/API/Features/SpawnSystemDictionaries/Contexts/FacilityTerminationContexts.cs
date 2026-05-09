using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Features.SpawnSystemDictionaries.Contexts;

public static class FacilityTerminationContexts
{
    public static void Register()
    {
        UnitPackRegistry.TryGet("FT_LastOperation", out var lastOpPack);
        UnitPackRegistry.TryGet("FT_GoC",          out var gocPack);
        UnitPackRegistry.TryGet("FT_Chaos",        out var chaosPack);

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
            gocPack,
            chaosPack
        );

        SpawnContextRegistry.Register(ctx);
    }
}