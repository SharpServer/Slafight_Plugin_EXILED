using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp3114;
using InventorySystem.Items.Usables.Scp1344;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp966Role : CRole
{
    protected override string RoleName { get; set; } = "SCP-966";

    protected override string Description { get; set; } = "透明な眠りを妨げるSCP。\n" +
                                                          "攻撃時敵の足と視界を一時的に妨害することが出来る。";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp966;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp966";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp3114;
    protected override float? SpawnMaxHealth => 1000f;
    protected override bool SpawnClearsInventory => true;
    protected override string SpawnCustomInfo => "SCP-966";
    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new(EffectType.NightVision, 255)
    ];
    private readonly Dictionary<Player, List<Player>> _invisibleEffectivePlayers = [];
    private readonly Dictionary<Player, byte> _speedLevels = [];
    private readonly Dictionary<Player, CoroutineHandle> _visibilityCoroutineHandles = [];
    private readonly Dictionary<Player, CoroutineHandle> _flickerCoroutineHandles = [];
    private readonly Dictionary<Player, CoroutineHandle> _speedCoroutineHandles = [];
    
    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Scp3114.Disguised += OnDisguised;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Scp3114.Disguised -= OnDisguised;
        base.UnregisterEvents();
    }

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.Position = Room.Get(RoomType.LczGlassBox).WorldPosition(Vector3.up * 0.5f);
        player.Scale = new Vector3(0.94f, 1.15f, 0.94f);
        player.MaxHumeShield = 500f;
        player.HumeShield = player.MaxHumeShield;
        
        _invisibleEffectivePlayers[player] = [];
        _speedLevels[player] = 1;
        _visibilityCoroutineHandles[player] = new CoroutineHandle();
        _flickerCoroutineHandles[player] = new CoroutineHandle();
        _speedCoroutineHandles[player] = new CoroutineHandle();
        _visibilityCoroutineHandles[player] = Timing.RunCoroutine(VisibilityCoroutine(player));
        _flickerCoroutineHandles[player] = Timing.RunCoroutine(FlickerCoroutine(player));
        _speedCoroutineHandles[player] = Timing.RunCoroutine(SpeedCoroutine(player));
        
        RoleSpecificTextProvider.Set(player, $"Speed Level: {_speedLevels[player]} / 5");
        base.OnRoleSpawned(player, roleSpawnFlags);
    }

    protected override void OnRoleHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.Player is null) return;
        ev.Player.EnableEffect<Slowness>(20, 10f);
        ev.Player.EnableEffect<Blindness>(40, 10f);
        base.OnRoleHurtingOthers(ev);
    }

    protected override void OnRoleChanging(ChangingRoleEventArgs ev)
    {
        Timing.KillCoroutines(_visibilityCoroutineHandles[ev.Player]);
        Timing.KillCoroutines(_flickerCoroutineHandles[ev.Player]);
        Timing.KillCoroutines(_speedCoroutineHandles[ev.Player]);
        base.OnRoleChanging(ev);
    }

    private void OnDisguised(DisguisedEventArgs ev)
    {
        if (!Check(ev.Player)) return;
        _speedLevels[ev.Player]++;
    }

    private IEnumerator<float> VisibilityCoroutine(Player player)
    {
        while (true)
        {
            if (Round.IsLobby || player.IsDead)
                yield break;
            var result = new List<Player>();
            foreach (var target in Player.List)
            {
                if (target is null) continue;
                if (HasViewCondition(target))
                {
                    result.Add(target);
                }

                if (result.Contains(player) && !_invisibleEffectivePlayers.ContainsKey(target))
                {
                    target.CurrentRoom?.SetRoomLightsForTargetOnly(target, false);
                }
            }
            _invisibleEffectivePlayers[player] = result;
            PlayerVisibilitySyncProvider.SetHiddenRule(player, p => !result.Contains(p));
            yield return Timing.WaitForSeconds(0.1f);
        }
    }

    private static IEnumerator<float> FlickerCoroutine(Player player)
    {
        while (true)
        {
            if (Round.IsLobby || player.IsDead)
                yield break;
            player.CurrentRoom?.TurnOffLights(Random.Range(1f, 3.5f));
            yield return Timing.WaitForSeconds(Random.Range(0f, 7f));
        }
    }

    private IEnumerator<float> SpeedCoroutine(Player player)
    {
        while (true)
        {
            if (Round.IsLobby || player.IsDead)
                yield break;
            RoleSpecificTextProvider.Set(player, $"Speed Level: {_speedLevels[player]} / 5");
            yield return Timing.WaitForSeconds(1f);
        }
    }

    private static bool HasViewCondition(Player? player)
    {
        if (player is null) return false;
        if (player.GetTeam() is CTeam.SCPs || !player.IsAlive)
        {
            return true;
        }

        foreach (var item in player.Items)
        {
            if (item?.Base is not Scp1344Item { IsWorn: true } scp1344) continue;
            if (CItem.TryGet(scp1344.ItemSerial, out var cItem) && cItem is CItemNvg)
            {
                return CItemNvg.HasBattery(scp1344.ItemSerial);
            }
            return true;
        }
        return player.CurrentItem is Firearm { NightVisionEnabled: true };
    }
}