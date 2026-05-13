using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomRoles.GoC;

public class GoCOperative : CRole
{
    protected override string RoleName { get; set; } = "GoC: Broken Dagger 工作員";
    protected override string Description { get; set; } = "部隊の任務を遂行する\nPassive: VERITAS\n遠くにいる敵等を認識できる";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.GoCOperative;
    protected override CTeam Team { get; set; } = CTeam.GoC;
    protected override string UniqueRoleKey { get; set; } = "GoCOperative";

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.NtfPrivate);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 100;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        player.AddItem(ItemType.GunCrossvec);
        player.AddItem(ItemType.GunShotgun);
        player.AddItem(ItemType.KeycardMTFOperative);
        player.AddItem(ItemType.Medkit);
        CItem.Get<GoCRecruitPaper>()?.Give(player);
        player.AddItem(ItemType.Radio);
        player.AddItem(ItemType.ArmorCombat);
            
        player.SetAmmo(AmmoType.Nato9,140);

        //PlayerExtensions.OverrideRoleName(player,$"{player.GroupName}","Hammer Down Infantry");
        player.SetCustomInfo("Global Occult Collision: Broken Dagger Operative");
        Timing.RunCoroutine(Coroutine(player));
    }
    
    private IEnumerator<float> Coroutine(Player player)
    {
        while (true)
        {
            if (!Check(player)) yield break;
            if (!player.IsEffectActive<Scp1344>())
            {
                player.EnableEffect(EffectType.Scp1344, 1);
            }
            yield return Timing.WaitForSeconds(3f);
        }
    }
}