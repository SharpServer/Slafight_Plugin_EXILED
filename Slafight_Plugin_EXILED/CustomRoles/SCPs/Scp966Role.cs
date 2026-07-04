using System.Collections.Generic;
using System.Linq;
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
    private const float BlackoutRadius = 15f;
    private const float BlackoutCheckInterval = 0.5f;

    private readonly Dictionary<Player, List<Player>> _invisibleEffectivePlayers = [];
    private readonly Dictionary<Player, byte> _speedLevels = [];
    private readonly Dictionary<Player, CoroutineHandle> _visibilityCoroutineHandles = [];
    private readonly Dictionary<Player, CoroutineHandle> _speedCoroutineHandles = [];
    private readonly HashSet<Room> _blackoutRooms = [];
    private bool _blackoutCoroutineRunning;
    
    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Scp3114.Disguising += OnDisguising;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Scp3114.Disguising -= OnDisguising;
        base.UnregisterEvents();
    }

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        CleanupPlayer(player);
        TrySetPlayerPosition(player, Room.Get(RoomType.LczGlassBox).WorldPosition(Vector3.up * 0.5f), nameof(Scp966Role));
        player.Scale = new Vector3(0.94f, 1.15f, 0.94f);
        player.MaxHumeShield = 500f;
        player.HumeShield = player.MaxHumeShield;
        
        _invisibleEffectivePlayers[player] = [];
        _speedLevels[player] = 1;
        _visibilityCoroutineHandles[player] = new CoroutineHandle();
        _speedCoroutineHandles[player] = new CoroutineHandle();
        _visibilityCoroutineHandles[player] = Timing.RunCoroutine(VisibilityCoroutine(player));
        _speedCoroutineHandles[player] = Timing.RunCoroutine(SpeedCoroutine(player));

        if (!_blackoutCoroutineRunning)
        {
            _blackoutCoroutineRunning = true;
            Timing.RunCoroutine(BlackoutCoroutine());
        }
        
        RoleSpecificTextProvider.Set(player, $"Speed Level: {_speedLevels[player]} / 5");
        base.OnRoleSpawned(player, roleSpawnFlags);
    }

    protected override void OnRoleHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.Player is null || ev.Attacker is null) return;
        ev.Amount = 20f + (_speedLevels.TryGetValue(ev.Attacker, out var speedLevel) ? speedLevel : 1);
        ev.Player.EnableEffect<Slowness>(20, 10f);
        ev.Player.EnableEffect<Blindness>(40, 10f);
        if (HasViewCondition(ev.Player))
            EffectedInfoTextProvider.Set(ev.Player, "見えない何かから攻撃を受けている・・・？", 3);
        base.OnRoleHurtingOthers(ev);
    }

    protected override void OnRoleChanging(ChangingRoleEventArgs ev)
    {
        CleanupPlayer(ev.Player);
        base.OnRoleChanging(ev);
    }

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        CleanupPlayer(ev.Player);
        CassieHelper.AnnounceTermination(ev, "SCP 9 6 6", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }

    protected override void OnRoleLeft(LeftEventArgs ev)
    {
        CleanupPlayer(ev.Player);
        base.OnRoleLeft(ev);
    }

    private void OnDisguising(DisguisingEventArgs ev)
    {
        if (!Check(ev.Player)) return;
        ev.IsAllowed = false;
        ev.Ragdoll?.Destroy();
        var speedLevel = _speedLevels.TryGetValue(ev.Player, out var level) ? level : (byte)1;
        if (speedLevel >= 5)
        {
            ev.Player.Heal(10f);
        }
        else
        {
            _speedLevels[ev.Player] = (byte)(speedLevel + 1);
        }
    }

    private IEnumerator<float> VisibilityCoroutine(Player player)
    {
        while (true)
        {
            if (!Check(player)) yield break;
            if (Round.IsLobby || player.ReferenceHub == null || player.IsDead)
                yield break;
            var result = new List<Player>();
            foreach (var target in Player.List)
            {
                if (target is null) continue;
                if (HasViewCondition(target))
                {
                    result.Add(target);
                }

                // 966 が見えるプレイヤーは暗く、見えないプレイヤーは明るく
                bool isVisible = result.Contains(target);
                if (target.GetTeam() is CTeam.SCPs) continue;
                target.CurrentRoom?.SetRoomLightsForTargetOnly(target, !isVisible);
            }
            _invisibleEffectivePlayers[player] = result;
            PlayerVisibilitySyncProvider.SetHiddenRule(player, p => !result.Contains(p));
            yield return Timing.WaitForSeconds(0.1f);
        }
    }

    private IEnumerator<float> BlackoutCoroutine()
    {
        while (true)
        {
            if (Round.IsLobby)
                break;

            var scpPositions = new List<Vector3>();
            foreach (var player in Player.List)
            {
                if (player != null && Check(player) && player.IsAlive)
                    scpPositions.Add(player.Position);
            }

            if (scpPositions.Count == 0)
                break;

            var roomsInRange = new HashSet<Room>();
            foreach (var room in Room.List)
            {
                if (room == null) continue;

                foreach (var position in scpPositions)
                {
                    if ((room.Position - position).sqrMagnitude > BlackoutRadius * BlackoutRadius)
                        continue;

                    roomsInRange.Add(room);
                    break;
                }
            }

            foreach (var room in roomsInRange)
            {
                if (_blackoutRooms.Add(room))
                    room.AreLightsOff = true;
            }

            foreach (var room in _blackoutRooms.ToList())
            {
                if (roomsInRange.Contains(room)) continue;
                room.AreLightsOff = false;
                _blackoutRooms.Remove(room);
            }

            yield return Timing.WaitForSeconds(BlackoutCheckInterval);
        }

        RestoreBlackoutRooms();
        _blackoutCoroutineRunning = false;
    }

    private void RestoreBlackoutRooms()
    {
        foreach (var room in _blackoutRooms)
        {
            if (room != null)
                room.AreLightsOff = false;
        }

        _blackoutRooms.Clear();
    }

    private IEnumerator<float> SpeedCoroutine(Player player)
    {
        while (true)
        {
            if (!Check(player)) yield break;
            if (Round.IsLobby || player.ReferenceHub == null || player.IsDead)
                yield break;
            if (!_speedLevels.TryGetValue(player, out var speedLevel))
                yield break;

            switch (speedLevel)
            {
                case 1:
                    player.EnableEffect<Slowness>(30);
                    break;
                case 2:
                    player.EnableEffect<Slowness>(20);
                    break;
                case 3:
                    player.EnableEffect<Slowness>(10);
                    break;
                case 4:
                    player.EnableEffect<Slowness>(0);
                    break;
                case 5:
                    player.EnableEffect<MovementBoost>(10);
                    break;
                default:
                    player.EnableEffect<Slowness>(30);
                    break;
            }
            RoleSpecificTextProvider.Set(player, $"Speed Level: {speedLevel} / 5");

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

    private void CleanupPlayer(Player player)
    {
        if (player == null)
            return;

        if (_visibilityCoroutineHandles.TryGetValue(player, out var visibilityHandle))
            Timing.KillCoroutines(visibilityHandle);

        if (_speedCoroutineHandles.TryGetValue(player, out var speedHandle))
            Timing.KillCoroutines(speedHandle);

        _visibilityCoroutineHandles.Remove(player);
        _speedCoroutineHandles.Remove(player);
        _invisibleEffectivePlayers.Remove(player);
        _speedLevels.Remove(player);
        RoleSpecificTextProvider.Clear(player);
        PlayerVisibilitySyncProvider.ShowToAll(player);
    }
}
