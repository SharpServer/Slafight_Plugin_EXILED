using Exiled.Events.EventArgs.Player;
using Exiled.Events.Handlers;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunSuppressiver : CItemWeapon
{
    public override string DisplayName => "Suppressiver";
    public override string Description => string.Empty;

    protected override string UniqueKey => "GunSuppressiver";
    protected override ItemType BaseItem => ItemType.GunFSP9;

    protected override float Damage => 30f;
    protected override byte MagazineSize => 42;
    protected override Vector3 Scale => new(1f, 1f, 1.15f);

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => CustomColor.ChaoticGreen.ToUnityColor();

    public override void RegisterEvents()
    {
        Player.SendingGunSound += OnSendingGunSound;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Player.SendingGunSound -= OnSendingGunSound;
        base.UnregisterEvents();
    }

    private void OnSendingGunSound(SendingGunSoundEventArgs ev)
    {
        if (!Check(ev.Item) || ev.AudioIndex is not (0 or 1 or 2)) return;
        ev.Pitch = 1;
    }
}
