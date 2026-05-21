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

public class GoCSquadLeader : CRole
{
    protected override string RoleName { get; set; } = "GoC: Broken Dagger 班長";
    protected override string Description { get; set; } = "部隊を指揮し、任務を遂行させる。\nPassive: VERITAS\n遠くにいる敵等を認識できる";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.GoCSquadLeader;
    protected override CTeam Team { get; set; } = CTeam.GoC;
    protected override string UniqueRoleKey { get; set; } = "GoCSquadLeader";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfCaptain;
    protected override float? SpawnMaxHealth => 110f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        ItemType.GunFRMG0,
        ItemType.KeycardMTFCaptain,
        ItemType.Medkit,
        ItemType.GrenadeHE,
        ItemType.Radio,
        typeof(GunGoCRailgun),
        typeof(SerumC),
        typeof(ArmorVip),
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato556] = 140,
    };
    protected override string SpawnCustomInfo => "Global Occult Collision: Broken Dagger Squad Leader";

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
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
