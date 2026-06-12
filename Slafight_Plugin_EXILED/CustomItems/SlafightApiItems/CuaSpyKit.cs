using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomRoles.ChaosInsurgency;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class CuaSpyKit : CItemKeycard
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
    private record struct SpyInfo(RoleTypeId RoleTypeId, string RoleName)
    {
        public readonly RoleTypeId RoleTypeId = RoleTypeId;
        public readonly string RoleName = RoleName;
    }
    private readonly Dictionary<Player, SpyInfo> _selectedRoles = [];
    private readonly List<SpyInfo> _infos = 
    [
        new (RoleTypeId.ChaosMarauder, $"<color={CTeam.ChaosInsurgency.GetTeamColor()}>変装を解除</color>"),
        new (RoleTypeId.ClassD, $"<color={CTeam.ClassD.GetTeamColor()}>Class-D Personnel</color>"),
        new (RoleTypeId.Scientist, $"<color={CTeam.Scientists.GetTeamColor()}>Scientist Personnel</color>")
    ];

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Item.InspectingItem += OnInspecting;
        Exiled.Events.Handlers.Player.Handcuffing += OnHandcuff;
        Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Item.InspectingItem -= OnInspecting;
        Exiled.Events.Handlers.Player.Handcuffing -= OnHandcuff;
        Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;
        _selectedRoles.Clear();
        base.UnregisterEvents();
    }

    protected override void OnWaitingForPlayers()
    {
        _selectedRoles.Clear();
        base.OnWaitingForPlayers();
    }

    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        _selectedRoles[ev.Player] = _infos.First();
        base.OnAcquired(ev, displayMessage);
    }

    protected override void OnChangingItem(ChangingItemEventArgs ev)
    {
        var playerId = ev.Player?.Id ?? 0;
        Timing.CallDelayed(1.5f, () =>
        {
            var player = Player.List.FirstOrDefault(p => p?.ReferenceHub != null && p.Id == playerId);
            if (player != null)
                Timing.RunCoroutine(Coroutine(player));
        });
        base.OnChangingItem(ev);
    }

    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        if (ev.Player?.GetCustomRole() != CRoleTypeId.ChaosUndercoverAgent) return;
        if (!ev.IsThrown)
        {
            Morph(ev.Player, _infos.First());
            return;
        }
        ev.IsAllowed = false;
        _selectedRoles[ev.Player] = _infos[(_infos.IndexOf(_selectedRoles[ev.Player]) + 1) % _infos.Count];
    }

    private void OnInspecting(InspectingItemEventArgs ev)
    {
        if (ev.Player?.GetCustomRole() != CRoleTypeId.ChaosUndercoverAgent) return;
        if (!Check(ev.Item)) return;
        ev.IsAllowed = false;
        if (_selectedRoles.TryGetValue(ev.Player, out var info))
        {
            Morph(ev.Player, info);
        }
    }

    private static void Morph(Player? player, SpyInfo spyInfo)
    {
        if (player is null) return;
        player.ChangeAppearance(spyInfo.RoleTypeId, Player.List.Where(p => p != null).ToList());
        player.SetCustomInfo(spyInfo.RoleTypeId.GetFullName().RemoveRichText());
        if (!spyInfo.RoleTypeId.IsChaos())
        {
            player.ShowHint($"<size=24>{spyInfo.RoleName}に変装しました", 2.5f);
        }
        else
        {
            player.SetCustomInfo(CRole.Get<ChaosUndercoverAgent>().CustomInfo);
            player.ShowHint($"<size=24>{spyInfo.RoleName}しました", 2.5f);
        }
    }

    private void OnHandcuff(HandcuffingEventArgs ev)
    {
        if (ev.Player?.GetCustomRole() != CRoleTypeId.ChaosUndercoverAgent) return;
        if (!ev.Player.Items.Where(Check).Any()) return;
        Morph(ev.Player, _infos.First());
    }

    private void OnPlayerLeft(LeftEventArgs ev)
    {
        if (ev.Player != null)
            _selectedRoles.Remove(ev.Player);
    }

    private IEnumerator<float> Coroutine(Player? player)
    {
        while (true)
        {
            if (Round.IsLobby || player?.ReferenceHub == null || !CheckHeld(player)) yield break;
            if (_selectedRoles.TryGetValue(player, out var info))
            {
                player.ShowHint($"<size=24>[変身メニュー]\n現在選択中： {info.RoleName}\nTキーで切り替えられ、Iで実行します。", 2.5f);
            }
            yield return Timing.WaitForSeconds(1f);
        }
    }
}

