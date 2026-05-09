using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.API.Features.SpawnSystemDictionaries.UnitPacks;

public static class SnowWarriersPacks
{
    public static void Register()
    {
        var snowNormalPack = new UnitPack(
            "GOI_SnowNormal", 
            new()
            {
                {
                    SpawnTypeId.GoiSnowNormal,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.SnowWarrier), (99f, false) },
                    }
                }
            }
        );
        var snowBackupPack = new UnitPack(
            "GOI_SnowBackup", 
            new()
            {
                {
                    SpawnTypeId.GoiSnowBackup,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.SnowWarrier), (99f, false) },
                    }
                }
            }
        );
        UnitPackRegistry.Register(snowNormalPack);
        UnitPackRegistry.Register(snowBackupPack);
    }
}