using System;
using Exiled.API.Enums;
using Exiled.Events.EventArgs.Player;
using InventorySystem.Items.Firearms.Attachments;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class S41MedicalPistol : CItemWeapon
{
    private const byte MedicalPistolMagazineSize = 6;
    private const byte VitalityIntensity = 20;
    private const float VitalityDuration = 2.5f;

    public override string DisplayName => "S-41 MEDICAL PISTOL";
    public override string Description =>
        "着弾した人間に鎮痛剤のようなリジェネ回復を付与し、火傷・出血・窒息・心停止を治療する。SCPには効果がない。";

    protected override string UniqueKey => "S41MedicalPistol";
    protected override ItemType BaseItem => ItemType.GunCOM18;
    protected override byte MagazineSize => MedicalPistolMagazineSize;

    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => new(0.35f, 1f, 0.65f);
    protected override AttachmentName[] Attachments =>
    [
        AttachmentName.StandardMagFMJ,
        AttachmentName.SoundSuppressor,
    ];
    protected override bool AllowAttachmentChanges => false;

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.Player is null || ev.Attacker is null)
            return;

        ev.Amount = 0f;
        ev.IsAllowed = false;

        if (ev.Player.IsScp || ev.Player.GetTeam() == CTeam.SCPs)
            return;

        Treat(ev.Player);
        ev.Attacker.ShowHitMarker();
    }

    private static void Treat(Exiled.API.Features.Player player)
    {
        player.DisableEffect(EffectType.Burned);
        player.DisableEffect(EffectType.Bleeding);
        player.DisableEffect(EffectType.Hemorrhage);
        player.DisableEffect(EffectType.Asphyxiated);
        player.DisableEffect(EffectType.CardiacArrest);
        player.EnableEffect(EffectType.Vitality, VitalityIntensity, VitalityDuration);

        if (player.Health < player.MaxHealth)
            player.Health = Math.Min(player.MaxHealth, player.Health + 5f);
    }

}
