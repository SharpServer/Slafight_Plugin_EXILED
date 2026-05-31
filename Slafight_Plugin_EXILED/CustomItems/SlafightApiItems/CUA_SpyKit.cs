using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class CUA_SpyKit : CItemKeycard
{
    public override string DisplayName => "CUA式スパイキット";
    public override string Description => "カオスの潜入工作員が持つ、潜入任務用変装セット。\nTキーでDクラス、Iキーでカオスに見た目を切り替えられる\n※カオスの一部の工作員のみが使用可能です";

    protected override string UniqueKey => "CUA_SpyKit";
    protected override ItemType BaseItem => ItemType.KeycardCustomSite02;

    protected override string KeycardLabel => "CUA. SpyKit";
    protected override Color32? KeycardLabelColor => new Color32(255, 255, 255, 255);
    protected override string KeycardName => "Chaos Insurgency";
    protected override Color32? TintColor => new Color32(0, 68, 0, 255);
    protected override Color32? KeycardPermissionsColor => new Color32(0, 0, 0, 255);
    protected override KeycardPermissions Permissions => KeycardPermissions.None;
    protected override byte Rank => 2;
    protected override string SerialNumber => "";

    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => CustomColor.ChaoticGreen.ToUnityColor();

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Item.InspectingItem += OnInspecting;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Item.InspectingItem -= OnInspecting;
        base.UnregisterEvents();
    }

    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        if (ev.Player?.GetCustomRole() != CRoleTypeId.ChaosUndercoverAgent) return;
        if (!ev.IsThrown)
        {
            ev.Player.ChangeAppearance(RoleTypeId.ChaosRifleman, Player.List.Where(p => p != null).ToList());
            return;
        }
        ev.Player?.ChangeAppearance(RoleTypeId.ClassD, Player.List.Where(p => p != null).ToList());
MeowExtensions.ShowHint(         ev.Player, "<size=24><color=#EE7760>Class-D Personnel</color>に変装しました", 2.5f);
        ev.IsAllowed = false;
    }

    private void OnInspecting(InspectingItemEventArgs ev)
    {
        if (ev.Player?.GetCustomRole() != CRoleTypeId.ChaosUndercoverAgent) return;
        if (!Check(ev.Item)) return;
        ev.Player?.ChangeAppearance(RoleTypeId.ChaosRifleman, Player.List.Where(p => p != null).ToList());
MeowExtensions.ShowHint(         ev.Player, "<size=24><color=#228B22>Chaos Insurgency Rifleman</color>に変装しました", 2.5f);
        ev.IsAllowed = false;
    }
}

