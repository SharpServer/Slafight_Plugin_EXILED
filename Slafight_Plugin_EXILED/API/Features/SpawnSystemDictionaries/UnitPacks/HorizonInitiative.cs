using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.API.Features.SpawnSystemDictionaries.UnitPacks;

public static class HorizonInitiativeUnitPacks
{
    public static void Register()
    {
        var sneNormalPack = new UnitPack(
            "GOI_Initiative", 
            new()
            {
                {
                    SpawnTypeId.GoiHorizonInitiative,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.InitiativeUltimateJesus), (1f, true) },
                    }
                }
            }
        );
        UnitPackRegistry.Register(sneNormalPack);
    }
}