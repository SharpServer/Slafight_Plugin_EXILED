using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.API.Features.SpawnSystemDictionaries.UnitPacks;

public static class MtfSeeNoEvilUnitPacks
{
    public static void Register()
    {
        var sneNormalPack = new UnitPack(
            "MTF_SneNormal", 
            new()
            {
                {
                    SpawnTypeId.MtfSneNormal,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.SneOperator), (1f, true) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.SneGears), (2f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.SneNeutralitist), (2f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.SnePurify), (2f, false) },
                    }
                }
            }
        );
        var sneBackupPack = new UnitPack(
            "MTF_SneBackup", 
            new()
            {
                {
                    SpawnTypeId.MtfSneBackup,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.SneGears), (1f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.SneNeutralitist), (1f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.SnePurify), (1f, false) },
                    }
                }
            }
        );
        UnitPackRegistry.Register(sneNormalPack);
        UnitPackRegistry.Register(sneBackupPack);
    }
}