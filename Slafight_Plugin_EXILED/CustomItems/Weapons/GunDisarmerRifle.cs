using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using InventorySystem.Items.Firearms.Attachments;
using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.Weapons;

/// <summary>
/// 命中した人間を拘束する単発狙撃銃です。
/// </summary>
public sealed class GunDisarmerRifle : CustomFirearm
{
    private static readonly IReadOnlyList<AttachmentName> FixedAttachments =
        [AttachmentName.ScopeSight];

    public override string Name => "Disarmer Rifle";

    public override string Description => "当たった対象を拘束できるスナイパーライフル";

    public override ItemType BaseType => ItemType.GunE11SR;

    protected override float? Damage => 1f;

    protected override int? MagazineCapacity => 1;

    protected override Vector3 PickupScale => new(1f, 1f, 1.045f);

    protected override bool PickupLightEnabled => true;

    protected override Color PickupLightColor => Color.gray;

    protected override IReadOnlyList<AttachmentName> Attachments => FixedAttachments;

    protected override bool AllowAttachmentChanges => false;

    private bool reloadAfterPickup;

    protected override void OnPickupStarting(PlayerPickingUpItemEventArgs ev)
    {
        reloadAfterPickup = ev.Pickup?.GetPreviousOwner() != ev.Player.AsExiled();
        base.OnPickupStarting(ev);
    }

    protected override void OnPickupCompleted(PlayerPickedUpItemEventArgs ev)
    {
        // 別の人が拾った銃は、旧実装と同じく次の射撃に備えて装填する。
        if (reloadAfterPickup && Item is { } item &&
            item.AsExiled() is Firearm firearm)
        {
            firearm.TryReload();
        }

        reloadAfterPickup = false;

        base.OnPickupCompleted(ev);
    }

    protected override void OnHurtingPlayer(PlayerHurtingEventArgs ev)
    {
        if (ev.Attacker?.ReferenceHub is null)
        {
            base.OnHurtingPlayer(ev);
            return;
        }

        Player target = Player.Get(ev.Player.ReferenceHub);
        Player attacker = Player.Get(ev.Attacker.ReferenceHub);

        if (target is not null && attacker is not null && !target.IsScp)
            target.Handcuff(attacker);

        base.OnHurtingPlayer(ev);
    }

    protected override void OnShotCompleted(PlayerShotWeaponEventArgs ev)
    {
        Player owner = Owner;
        ushort serial = Serial;

        if (owner is not null && serial != 0)
        {
            PlayerScope.Of(owner).Delay(20f, player =>
            {
                // ラウンド再開・持ち主の退出・拾い直し後の別インスタンスを取り違えない。
                if (CustomItem.Of<GunDisarmerRifle>(serial) is not { } rifle || rifle.Owner != player)
                    return;

                if (rifle.Item?.AsExiled() is not { } item)
                    return;

                player.CurrentItem = item;
                PlayerScope.Of(player).Delay(1f, _ =>
                {
                    if (CustomItem.Of<GunDisarmerRifle>(serial) is { Owner: not null } current &&
                        current.Owner == player && current.Item?.AsExiled() is Firearm firearm)
                    {
                        firearm.TryReload();
                    }
                });
            });
        }

        base.OnShotCompleted(ev);
    }

    protected override void OnReloadStarting(PlayerReloadingWeaponEventArgs ev)
    {
        ev.IsAllowed = false;
        base.OnReloadStarting(ev);
    }

}
