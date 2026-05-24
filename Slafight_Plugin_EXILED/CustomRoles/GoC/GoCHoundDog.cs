using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.API.Features.Pickups.Projectiles;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using UnityEngine;
using Scp1344 = CustomPlayerEffects.Scp1344;

namespace Slafight_Plugin_EXILED.CustomRoles.GoC;

public class GoCHoundDog : CRole
{
    protected override string RoleName { get; set; } = "GoC: Hound Dog マークⅡ戦闘強化服Combat Garment(ホワイト・スーツ)";

    protected override string Description { get; set; } =
        "GoC製のとても強い戦闘強化服。色んな機能・装備が盛り込まれている。\n" +
        "Passive: ホワイトスーツ\n" +
        "ホワイトスーツの超駆動により常時コーラ一本分の速度を提供する。\n" +
        "Passive: VERITAS\n" +
        "遠くにいる敵等を認識できる\n" +
        "Passive: 自爆装置\n" +
        "死亡、拘束され際に起動し、グレネード一個分の自爆を引き起こす。\n" +
        "Passive: 自動認証\n" +
        "施設内のすべてのキーカード認証等を素通りできる。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.GoCHoundDog;
    protected override CTeam Team { get; set; } = CTeam.GoC;
    protected override string UniqueRoleKey { get; set; } = "GoCHoundDog";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.NtfSpecialist;
    protected override float? SpawnMaxHealth => 120f;
    protected override IReadOnlyList<object> SpawnItems =>
    [
        typeof(ArmorVip),
        typeof(GunSuperLogicer),
        typeof(GunGoCRailgunFull),
        typeof(CloakGenerator),
        ItemType.Adrenaline,
        ItemType.Medkit,
    ];
    protected override IReadOnlyDictionary<AmmoType, ushort> SpawnAmmo => new Dictionary<AmmoType, ushort>
    {
        [AmmoType.Nato762] = 140,
    };
    protected override string SpawnCustomInfo => "Global Occult Collision: Hound Dog Mark II Combat Garment White Suit";

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.CustomHumeShieldStat.MaxValue = 1500;
        player.CustomHumeShieldStat.CurValue = player.CustomHumeShieldStat.MaxValue;
        player.CustomHumeShieldStat.ShieldRegenerationMultiplier = 3.5f;
        player.IsBypassModeEnabled = true;

        Timing.RunCoroutine(Coroutine(player));
    }

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        var grenade = Pickup.CreateAndSpawn(ItemType.GrenadeHE, ev.Player.Position, Quaternion.identity);
        if (grenade is GrenadePickup grenadeBase)
        {
            grenadeBase.FuseTime = 0.01f;
        }
        
        base.OnRoleDying(ev);
    }

    protected override void OnRoleBeingHandcuffed(HandcuffingEventArgs ev)
    {
        ev.Target.Kill("自爆機能による力");
        var grenade = Pickup.CreateAndSpawn(ItemType.GrenadeFlash, ev.Player.Position, Quaternion.identity);
        if (grenade is FlashbangProjectile grenadeBase)
        {
            grenadeBase.AdditionalBlindedEffect = 5.5f;
            grenadeBase.FuseTime = 0.01f;
        }
    }

    private IEnumerator<float> Coroutine(Player player)
    {
        while (true)
        {
            if (!Check(player)) yield break;
            if (!player.IsEffectActive<MovementBoost>())
            {
                player.EnableEffect(EffectType.MovementBoost, 25);
            }
            if (!player.IsEffectActive<Scp1344>())
            {
                player.EnableEffect(EffectType.Scp1344, 1);
            }
            yield return Timing.WaitForSeconds(3f);
        }
    }
}
