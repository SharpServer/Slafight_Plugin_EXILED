using Exiled.Events.EventArgs.Player;
using Exiled.Events.Handlers;
using InventorySystem.Items.Firearms.Attachments;
using PlayerStatsSystem;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunSL8 : CItemWeapon
{
    public override string DisplayName => "SL-8G";
    public override string Description => "カオスが所有するスナイパーライフル。質はそこそこ";
    protected override string UniqueKey => "GunSL8";
    protected override ItemType BaseItem => ItemType.GunFRMG0;
    protected override float Damage => 40f;
    protected override byte MagazineSize => 5;
    protected override Vector3 Scale => new(1f, 1f, 1.75f);
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => CustomColor.ChaoticGreen.ToUnityColor();
    protected override AttachmentName[] Attachments =>
    [
        AttachmentName.ScopeSight,
        AttachmentName.SoundSuppressor,
    ];
    protected override bool AllowAttachmentChanges => false;

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        ev.DamageHandler.StartVelocity *= 1.025f;
        if (ev.DamageHandler.Base is StandardDamageHandler { Hitbox: HitboxType.Headshot })
        {
            ev.Amount += 15f;
        }
        base.OnHurtingOthers(ev);
    }

    public override void RegisterEvents()
    {
        Player.SendingGunSound += OnSound;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Player.SendingGunSound -= OnSound;
        base.UnregisterEvents();
    }

    private void OnSound(SendingGunSoundEventArgs ev)
    {
    }
}
