using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.API.Features.SpawnSystemDictionaries.UnitPacks;

public static class MtfCoreForceUnitPacks
{
    public static void Register()
    {
        var sneNormalPack = new UnitPack(
            "MTF_Lws", 
            new()
            {
                {
                    SpawnTypeId.MtfSneNormal,
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
        var sneBackupPack = new UnitPack(
            "MTF_Rrh", 
            new()
            {
                {
                    SpawnTypeId.MtfSneBackup,
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
        UnitPackRegistry.Register(sneNormalPack);
        UnitPackRegistry.Register(sneBackupPack);
    }
}