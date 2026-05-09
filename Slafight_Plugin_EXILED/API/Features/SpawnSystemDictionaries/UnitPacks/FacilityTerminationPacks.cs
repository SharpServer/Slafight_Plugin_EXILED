using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.API.Features.SpawnSystemDictionaries.UnitPacks;

public static class FacilityTerminationPacks
{
    public static void Register()
    {
        // LastOperation: 全員 Sculpture
        var lastOperationPack = new UnitPack(
            "FT_LastOperation",
            new()
            {
                {
                    SpawnTypeId.MtfLastOperationNormal,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.Sculpture), (99f, true) }
                    }
                },
                {
                    SpawnTypeId.MtfLastOperationBackup,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.Sculpture), (99f, true) }
                    }
                }
            }
        );
        UnitPackRegistry.Register(lastOperationPack);

        // GoC 部隊
        var gocPack = new UnitPack(
            "FT_GoC",
            new()
            {
                {
                    SpawnTypeId.GoiGoCNormal,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.GoCSquadLeader),   (1f, true)  },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.GoCDeputy),        (1f, true)  },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.GoCMedic),         (1f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.GoCThaumaturgist), (1f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.GoCCommunications),(1f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.GoCHoundDog),      (1f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.GoCOperative),     (99f, false)},
                    }
                },
                {
                    SpawnTypeId.GoiGoCBackup,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.GoCSquadLeader),   (1f, true)  },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.GoCDeputy),        (1f, true)  },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.GoCMedic),         (1f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.GoCThaumaturgist), (1f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.GoCCommunications),(1f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.GoCHoundDog),      (1f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.GoCOperative),     (99f, false)},
                    }
                }
            }
        );
        UnitPackRegistry.Register(gocPack);

        // Chaos 部隊（FacilityTermination 用）
        var chaosPack = new UnitPack(
            "FT_Chaos",
            new()
            {
                {
                    SpawnTypeId.GoiChaosNormal,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.ChaosCommando), (1f,  false) },
                        { new SpawnSystem.SpawnRoleKey(RoleTypeId.ChaosRepressor), (2f,  false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.ChaosSignal),   (2f,  false) },
                        { new SpawnSystem.SpawnRoleKey(RoleTypeId.ChaosMarauder),  (2f,  false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.ChaosTacticalUnit), (2f, false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.ChaosPenal), (2f, false) },
                        { new SpawnSystem.SpawnRoleKey(RoleTypeId.ChaosRifleman),  (99f, false) },
                    }
                },
                {
                    SpawnTypeId.GoiChaosBackup,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.ChaosSignal),  (1f,  true)  },
                        { new SpawnSystem.SpawnRoleKey(RoleTypeId.ChaosMarauder), (2f,  false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.ChaosPenal), (1f, false) },
                        { new SpawnSystem.SpawnRoleKey(RoleTypeId.ChaosRifleman), (99f, false) },
                    }
                }
            }
        );
        UnitPackRegistry.Register(chaosPack);
    }
}