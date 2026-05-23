using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.API.Features.SpawnSystemDictionaries.UnitPacks;

public static class MtfCoreForceUnitPacks
{
    public static void Register()
    {
        var lwsNormalPack = new UnitPack(
            "MTF_Lws", 
            new()
            {
                {
                    SpawnTypeId.MtfLwsNormal,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.LwsJudgement), (1f, true) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.LwsLiaison), (1f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.LwsForensic), (1f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.LwsAgent), (1f, false) },
                    }
                }
            }
        );
        var rrhNormalPack = new UnitPack(
            "MTF_Rrh", 
            new()
            {
                {
                    SpawnTypeId.MtfRrhNormal,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.RrhWarden), (1f, true) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.RrhEnforcer), (1f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.RrhAegis), (1f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.RrhAssaulter), (1f, false) },
                    }
                }
            }
        );
        UnitPackRegistry.Register(lwsNormalPack);
        UnitPackRegistry.Register(rrhNormalPack);
    }
}