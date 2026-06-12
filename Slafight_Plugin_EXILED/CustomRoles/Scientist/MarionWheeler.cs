using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Scientist;

public class MarionWheeler : CRole
{
    protected override string RoleName { get; set; } = "マリオン・ホイーラー";
    protected override string Description { get; set; } = "現在貴方の部門は壊滅状態に陥っている...\n" +
                                                          "下層のDクラス収容房最奥にある反ミーム爆弾を起動してこのアウトブレイクをリセットしなければならない。\n" +
                                                          "<color=red>例え命を落とそうとも</color>";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.MarionWheeler;
    protected override CTeam Team { get; set; } = CTeam.Scientists;
    protected override string UniqueRoleKey { get; set; } = "MarionWheeler";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scientist;
    protected override float? SpawnMaxHealth => 120f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.KeycardContainmentEngineer,
        ItemType.Medkit,
        ItemType.Medkit,
        ItemType.Medkit,
        typeof(GunScp7381),
        typeof(ClassZMemoryForcePil),
    ];
    protected override Vector3? SpawnPosition => Door.Get(DoorType.Intercom).Position + Vector3.up * 1.25f;
    protected override string SpawnCustomInfo => "Marion Wheeler";

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        Timing.CallDelayed(RoleSpawnTimings.FastSpawnFinalize, () =>
        {
            Timing.RunCoroutine(Coroutine(player));
        });
    }

    private IEnumerator<float> Coroutine(Player player)
    {
        while (true)
        {
            if (!Check(player)) yield break;
            player.EnableEffect<Slowness>(25, 5f);
            yield return Timing.WaitForSeconds(1f);
        }
    }
}
