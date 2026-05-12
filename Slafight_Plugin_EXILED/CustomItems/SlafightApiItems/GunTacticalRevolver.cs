using Exiled.API.Enums;
using Exiled.API.Features.DamageHandlers;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunTacticalRevolver : CItemWeapon
{
    public override string DisplayName => "Tactical Revolver";
    public override string Description =>
        "ヘッドショットをすると暫く脳震盪を与えられる精密なリボルバー。\nリロード時暫くは精度良く扱える";

    protected override string UniqueKey => "GunTacticalRevolver";
    protected override ItemType BaseItem => ItemType.GunRevolver;

    protected override float   Damage       => 30f;
    protected override byte    MagazineSize => 7;
    protected override Vector3 Scale        => new(1f, 1f, 1.15f);

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.yellow;

    /// <summary>ヘッドショット時にだけ Poisoned を付与し、Damage 上書き (base) は通常通り走らせる。</summary>
    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.DamageHandler.Base is GenericDamageHandler { Hitbox: HitboxType.Headshot })
            ev.Player?.EnableEffect(EffectType.Concussed, 255, 10f);

        base.OnHurtingOthers(ev);
    }

    /// <summary>リロード完了で Scp1853 を短時間付与 (狙撃補助)。</summary>
    protected override void OnReloaded(ReloadedWeaponEventArgs ev)
    {
        ev.Player?.EnableEffect(EffectType.Scp1853, 2, 12f);
    }
}
