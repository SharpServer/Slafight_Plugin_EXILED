using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.API.Features.SpawnSystemDictionaries.UnitPacks;

public static class MtfPandrasBoxUnitPacks
{
    public static void Register()
    {
        var pdxNormalPack = new UnitPack(
            "MTF_Pdx",
            new()
            {
                {
                    SpawnTypeId.MtfPdx,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.Scp076), (1f, true) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.PdxWarden), (1f, true) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.PdxWatcher), (6f, false) },
                    }
                }
            }
        );
        UnitPackRegistry.Register(pdxNormalPack);
    }
}
