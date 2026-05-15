using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using PlayerStatsSystem;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Others.SergeyMakarov;

public class SergeyMakarovRole : CRole
{
    protected override string RoleName { get; set; } = "<color=#dc143c>施設管理官 - セルゲイ・マカロフ</color>";
    protected override string Description { get; set; } = "<size=25>" +
                                                          "部下に疎まれ、裏切り者に殺され、復讐に憑りつかれ蘇った施設管理官。\n" +
                                                          "彼は戻ってきた。自身を蔑ろにした全てに復讐するために...\n" +
                                                          "<b><color=red>持てる全てを使い、奴らへの復讐を果たせ</color></b>";

    protected override float DescriptionDuration { get; set; } = 10f;
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SergeyMakarov;
    protected override CTeam Team { get; set; } = CTeam.Others;
    protected override string UniqueRoleKey { get; set; } = "TheSergeyHimSelf";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.Scientist);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        player.AddItem(ItemType.GunCrossvec);
        player.AddItem(ItemType.KeycardFacilityManager);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.Medkit);
        player.AddItem(ItemType.ArmorCombat);
        player.AddItem(ItemType.Radio);
        var pos = Room.Get(RoomType.HczIncineratorWayside).WorldPosition(new Vector3(0f,12.55f,0f));
        player.Position = pos;
            
        player.SetCustomInfo("Facility Manager");
        
        UnitPackRegistry.TryGet("MTF_NtfNormal", out var ntfpack);
        UnitPackRegistry.TryGet("GOI_ChaosBackup", out var ntfbackupPack);
        UnitPackRegistry.TryGet("GOI_ChaosNormal", out var chaospack);
        UnitPackRegistry.TryGet("GOI_ChaosBackup", out var chaosbackupPack);
        var chaosOnlyContext = new SpawnContext(
            "SM_VanillaOnly",
            new()
            {
                { SpawnTypeId.MtfNtfNormal, 100 }
            },
            new ()
            {
                { SpawnTypeId.GoiChaosNormal, 100 }
            },
            new ()
            {
                { SpawnTypeId.MtfNtfBackup, 100 }
            },
            new ()
            {
                { SpawnTypeId.GoiChaosNormal, 100 }
            },
            ntfpack,ntfbackupPack,chaospack,chaosbackupPack
        );
        SpawnContextRegistry.Register(chaosOnlyContext);
        SpawnContextRegistry.SetActive("SM_VanillaOnly");
        
        Timing.CallDelayed(0.05f, () =>
        {
            player.CustomName = $"セルゲイ・マカロフ ({player.Nickname})";
            CustomInfoDisplay.Refresh(player);
        });
        Timing.RunCoroutine(SergeySharedContents.SergeySharedCoroutine(player));
    }

    protected override void OnDying(DyingEventArgs ev)
    {
        if (ev.Attacker == null && ev.DamageHandler.Base is CustomReasonDamageHandler) return;
        ev.IsAllowed = false;
        UnitPackRegistry.TryGet("GOI_ChaosNormal", out var pack);
        UnitPackRegistry.TryGet("GOI_ChaosBackup", out var backupPack);
        var chaosOnlyContext = new SpawnContext(
            "SM_ChaosOnly",
            new()
            {
                { SpawnTypeId.GoiChaosNormal, 100 }
            },
            new ()
            {
                { SpawnTypeId.GoiChaosNormal, 100 }
            },
            new ()
            {
                { SpawnTypeId.GoiChaosBackup, 100 }
            },
            new ()
            {
                { SpawnTypeId.GoiChaosBackup, 100 }
            },
            pack,backupPack
        );
        SpawnContextRegistry.Register(chaosOnlyContext);
        SpawnContextRegistry.SetActive("SM_ChaosOnly");
        Timing.RunCoroutine(SergeyMakarovExtensionAndUtils.AwakenScene(ev.Player));
        base.OnDying(ev);
    }
}
