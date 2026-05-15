using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.API.Features.SpawnSystemDictionaries.UnitPacks;

public static class DefaultUnitPacks
{
    public static void Register()
    {
        // ▼ NTF ノーマル波
        var ntfNormalPack = new UnitPack(
            "MTF_NtfNormal",
            new()
            {
                {
                    SpawnTypeId.MtfNtfNormal,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.NtfGeneral),    (1f,  false)  },
                        { new SpawnSystem.SpawnRoleKey(RoleTypeId.NtfCaptain),     (1f,  true)   },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.NtfLieutenant), (2f,  false)  },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.NtfDetainer),   (1f,  false)  },
                        { new SpawnSystem.SpawnRoleKey(RoleTypeId.NtfSergeant),    (2f,  false)  },
                        { new SpawnSystem.SpawnRoleKey(RoleTypeId.NtfPrivate),     (99f, true)   },
                    }
                }
            }
        );
        UnitPackRegistry.Register(ntfNormalPack);

        // ▼ NTF バックアップ波
        var ntfBackupPack = new UnitPack(
            "MTF_NtfBackup",
            new()
            {
                {
                    SpawnTypeId.MtfNtfBackup,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(RoleTypeId.NtfSergeant), (1f,  true) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.NtfDetainer), (1f,  false) },
                        { new SpawnSystem.SpawnRoleKey(RoleTypeId.NtfPrivate),  (99f, true) },
                    }
                }
            }
        );
        UnitPackRegistry.Register(ntfBackupPack);

        // ▼ Hammer Down
        var hdNormalPack = new UnitPack(
            "MTF_HDNormal",
            new()
            {
                {
                    SpawnTypeId.MtfHdNormal,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.HdMarshal),   (1f,  false)  },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.HdCommander), (2f,  true)   },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.HdShotgunner),  (2f, false)  },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.HdDisarmer),  (2f, false)  },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.HdInfantry),  (99f, false)  },
                    }
                }
            }
        );
        UnitPackRegistry.Register(hdNormalPack);

        var hdBackupPack = new UnitPack(
            "MTF_HDBackup",
            new()
            {
                {
                    SpawnTypeId.MtfHdBackup,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.HdCommander), (1f,  true)  },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.HdShotgunner),  (2f, false)  },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.HdDisarmer),  (2f, false)  },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.HdInfantry),  (99f, false) },
                    }
                }
            }
        );
        UnitPackRegistry.Register(hdBackupPack);

        // ▼ Chaos
        var chaosNormalPack = new UnitPack(
            "GOI_ChaosNormal",
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
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.ChaosSniper), (2f, false) },
                        { new SpawnSystem.SpawnRoleKey(RoleTypeId.ChaosRifleman),  (99f, false) },
                    }
                }
            }
        );
        UnitPackRegistry.Register(chaosNormalPack);

        var chaosBackupPack = new UnitPack(
            "GOI_ChaosBackup",
            new()
            {
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
        UnitPackRegistry.Register(chaosBackupPack);

        // ▼ Fifthist
        var fifthNormalPack = new UnitPack(
            "GOI_FifthistNormal",
            new()
            {
                {
                    SpawnTypeId.GoiFifthistNormal,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.FifthistPriest),   (1f,  true)  },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.FifthistRescure),  (3f,  false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.FifthistGuidance), (1f,  false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.FifthistConvert),  (99f, false) },
                    }
                }
            }
        );
        UnitPackRegistry.Register(fifthNormalPack);

        var fifthBackupPack = new UnitPack(
            "GOI_FifthistBackup",
            new()
            {
                {
                    SpawnTypeId.GoiFifthistBackup,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.FifthistRescure),  (1f,  false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.FifthistGuidance), (1f,  false) },
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.FifthistConvert),  (99f, false) },
                    }
                }
            }
        );
        UnitPackRegistry.Register(fifthBackupPack);

        var securityTeamPack = new UnitPack(
            "SecurityTeam",
            new()
            {
                {
                    SpawnTypeId.SecurityTeam,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.SecurityTeamGuard), (99f, false) }
                    }
                }
            }
        );
        UnitPackRegistry.Register(securityTeamPack);

        var chaosAgentPack = new UnitPack(
            "ChaosAgents",
            new()
            {
                {
                    SpawnTypeId.ChaosAgents,
                    new()
                    {
                        { new SpawnSystem.SpawnRoleKey(CRoleTypeId.ChaosIntruder), (99f, false) },
                    }
                }
            }
        );
        UnitPackRegistry.Register(chaosAgentPack);
    }
}
