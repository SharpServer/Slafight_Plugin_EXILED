using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomRoles.Fifthists;
using PlayerRoles;
using UnityEngine;
using LabItem = LabApi.Features.Wrappers.Item;

namespace Slafight_Plugin_EXILED.CustomItems.Weapons;

/// <summary>第五教会 案内人の杖です。これでとどめを刺された相手は死なずに改宗者になります。</summary>
public sealed class CaneOfTheStars : CustomItem
{
    public override string Name => "Cane of the Stars";
    public override string Description =>
        "第五教会の案内人が持つ杖。\n殴った対象の脳内に第五主義思想を直接流し込み、\n強制的に第五主義者に改宗させる能力を持つ";
    public override ItemType BaseType => ItemType.Jailbird;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.magenta;

    protected override void Customize(LabItem item)
    {
        if (Exiled.API.Features.Items.Item.Get<Jailbird>(item.Base) is { } jailbird)
        {
            jailbird.ChargeDamage = 555f;
            jailbird.MeleeDamage = 55f;
        }

        base.Customize(item);
    }

    protected override void RegisterEvents() => Exiled.Events.Handlers.Player.Dying += OnDying;

    protected override void UnregisterEvents() => Exiled.Events.Handlers.Player.Dying -= OnDying;

    private void OnDying(DyingEventArgs ev)
    {
        if (!ev.IsAllowed || !CheckHeld(ev.Attacker)) return;

        if (CustomRole.Spawn<FifthistConvert>(ev.Player, RoleSpawnFlags.AssignInventory) is not null)
            ev.IsAllowed = false;
    }
}
