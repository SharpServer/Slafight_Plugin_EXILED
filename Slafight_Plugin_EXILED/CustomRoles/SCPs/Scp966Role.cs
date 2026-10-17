using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp3114;
using Exiled.Events.Handlers;
using InventorySystem.Items.Usables.Scp1344;
using MapGeneration.Holidays;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Player = Exiled.API.Features.Player;

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
    private const float TintRadius = 15f;

    // 周囲の部屋ライトの色変更: 基準色(0-255)を、ランダムに割る(暗く)か掛ける(明るく)。
    private const float TintFadeDuration = 1f;
    private const byte TintR = 155;
    private const byte TintG = 22;
    private const byte TintB = 4;
    private const float TintDivideMax = 10f;
    private const float TintMultiplyMax = 2f;
    private const float TintCheckInterval = 0.5f;

    // 可視性の再評価周期。以前は 0.1 秒ごとに全プレイヤーへ部屋ライトの
    // 対象限定 RPC を差分なしで投げていた。
    private const float VisibilityCheckInterval = 0.25f;

    // 破棄され得る Player を遅延辞書のキーにしないため、すべて Player.Id で持つ。
    private readonly Dictionary<int, HashSet<int>> _invisibleEffectivePlayers = [];
    private readonly Dictionary<int, byte> _speedLevels = [];
    private readonly Dictionary<int, CoroutineHandle> _visibilityCoroutineHandles = [];
    private readonly Dictionary<int, CoroutineHandle> _speedCoroutineHandles = [];
    private readonly Dictionary<int, CoroutineHandle> _flickerCoroutineHandles = [];

    // 観測者 ID -> 直近に送ったライト状態 (true = 消灯を送った)。
    // 変化したときだけ RPC を出すためのキャッシュ。
    private readonly Dictionary<int, bool> _lightStateByViewer = [];
    private readonly HashSet<Room> _tintedRooms = [];
    private readonly Dictionary<Room, Color> _originalRoomColors = [];
    private bool _tintCoroutineRunning;
    
    public override void RegisterEvents()
    {
        Scp3114.Disguising += OnDisguising;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Scp3114.Disguising -= OnDisguising;
        base.UnregisterEvents();
    }

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        CleanupPlayer(player);
        TrySetPlayerPosition(player, Room.Get(RoomType.LczGlassBox).WorldPosition(Vector3.up * 0.5f), nameof(Scp966Role));
        player.Scale = new Vector3(0.94f, 1.15f, 0.94f);
        player.MaxHumeShield = 500f;
        player.HumeShield = player.MaxHumeShield;
        
        // このラウンド最初の 966。前ラウンドのライト状態キャッシュが残っていると
        // 「変化なし」と誤判定して必要な RPC を落とすので、ここで捨てる。
        if (_visibilityCoroutineHandles.Count == 0)
            _lightStateByViewer.Clear();

        _invisibleEffectivePlayers[player.Id] = [];
        _speedLevels[player.Id] = 1;
        _visibilityCoroutineHandles[player.Id] = Timing.RunCoroutine(VisibilityCoroutine(player));
        _speedCoroutineHandles[player.Id] = Timing.RunCoroutine(SpeedCoroutine(player));
        _flickerCoroutineHandles[player.Id] = Timing.RunCoroutine(FlickerCoroutine(player));

        if (!_tintCoroutineRunning)
        {
            _tintCoroutineRunning = true;
            RoundScopedCoroutines.Run(TintCoroutine());
        }
        
        RoleSpecificTextProvider.Set(player, $"Speed Level: {_speedLevels[player.Id]} / 5");
        base.OnRoleSpawned(player, roleSpawnFlags);
    }

    protected override void OnRoleHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.Player is null || ev.Attacker is null) return;
        ev.Amount = 20f + (_speedLevels.TryGetValue(ev.Attacker.Id, out var speedLevel) ? speedLevel : 1);
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
        var speedLevel = _speedLevels.TryGetValue(ev.Player.Id, out var level) ? level : (byte)1;
        if (speedLevel >= 5)
        {
            ev.Player.Heal(10f);
        }
        else
        {
            _speedLevels[ev.Player.Id] = (byte)(speedLevel + 1);
        }
    }

    private IEnumerator<float> VisibilityCoroutine(Player player)
    {
        while (true)
        {
            if (!Check(player)) yield break;
            if (Round.IsLobby || player.ReferenceHub == null || player.IsDead)
                yield break;
            // 可視プレイヤーは ID の HashSet で持つ。
            // 以前は構築中の List に対する Contains（線形探索）で判定していたため、
            // 人数の二乗に比例し、しかも判定順に依存していた。
            var visibleIds = new HashSet<int>();
            foreach (var target in Player.List)
            {
                if (target is null) continue;
                if (HasViewCondition(target))
                    visibleIds.Add(target.Id);
            }

            foreach (var target in Player.List)
            {
                if (target is null) continue;
                if (target.GetTeam() is CTeam.SCPs) continue;

                // 966 が見えるプレイヤーは暗く、見えないプレイヤーは明るく。
                // ライト同期は対象限定 RPC なので、状態が変わったときだけ送る。
                bool lightsOff = !visibleIds.Contains(target.Id);
                if (_lightStateByViewer.TryGetValue(target.Id, out var previous) && previous == lightsOff)
                    continue;

                _lightStateByViewer[target.Id] = lightsOff;
                target.CurrentRoom?.SetRoomLightsForTargetOnly(target, lightsOff);
            }

            _invisibleEffectivePlayers[player.Id] = visibleIds;
            PlayerVisibilitySyncProvider.SetHiddenRule(player, p => p != null && !visibleIds.Contains(p.Id));
            yield return Timing.WaitForSeconds(VisibilityCheckInterval);
        }
    }

    // 966 の居る部屋を、モードに関係なく定期的にちかちかさせる。
    private IEnumerator<float> FlickerCoroutine(Player player)
    {
        while (true)
        {
            if (!Check(player)) yield break;
            if (Round.IsLobby || player.ReferenceHub == null || player.IsDead)
                yield break;

            player.CurrentRoom?.TurnOffLights(Random.Range(1f, 3.5f));

            yield return Timing.WaitForSeconds(Random.Range(0f, 7f));
        }
    }

    private IEnumerator<float> TintCoroutine()
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
                    if ((room.Position - position).sqrMagnitude > TintRadius * TintRadius)
                        continue;

                    roomsInRange.Add(room);
                    break;
                }
            }

            foreach (var room in roomsInRange)
            {
                if (_tintedRooms.Add(room))
                    TintRoom(room);
            }

            foreach (var room in _tintedRooms.ToList())
            {
                if (roomsInRange.Contains(room)) continue;
                RestoreRoom(room);
                _tintedRooms.Remove(room);
            }

            yield return Timing.WaitForSeconds(TintCheckInterval);
        }

        RestoreTintedRooms();
        _tintCoroutineRunning = false;
    }

    /// <summary>可視性ループが消灯を送ったままのプレイヤーを明るく戻す。</summary>
    private void RestoreViewerLights()
    {
        foreach (var pair in _lightStateByViewer)
        {
            if (!pair.Value)
                continue;

            var viewer = Player.Get(pair.Key);
            viewer?.CurrentRoom?.SetRoomLightsForTargetOnly(viewer, false);
        }

        _lightStateByViewer.Clear();
    }

    private void RestoreTintedRooms()
    {
        foreach (var room in _tintedRooms)
            RestoreRoom(room);

        _tintedRooms.Clear();
        _originalRoomColors.Clear();
    }

    private void TintRoom(Room room)
    {
        _originalRoomColors[room] = room.Color;
        room.FadeColor(RandomTintColor(), TintFadeDuration);
    }

    private void RestoreRoom(Room room)
    {
        if (room == null)
            return;

        if (_originalRoomColors.TryGetValue(room, out var original))
        {
            room.FadeColor(original, TintFadeDuration);
            _originalRoomColors.Remove(room);
        }
    }

    /// <summary>基準色を、ランダムに割る(暗く)か掛ける(明るく)かして返す。</summary>
    private static Color RandomTintColor()
    {
        const float divideMax = TintDivideMax < 1f ? 1f : TintDivideMax;
        const float multiplyMax = TintMultiplyMax < 1f ? 1f : TintMultiplyMax;
        var factor = Random.value < 0.5f
            ? 1f / Random.Range(1f, divideMax)
            : Random.Range(1f, multiplyMax);

        return new Color(
            Mathf.Clamp01(TintR / 255f * factor),
            Mathf.Clamp01(TintG / 255f * factor),
            Mathf.Clamp01(TintB / 255f * factor));
    }

    private IEnumerator<float> SpeedCoroutine(Player player)
    {
        while (true)
        {
            if (!Check(player)) yield break;
            if (Round.IsLobby || player.ReferenceHub == null || player.IsDead)
                yield break;
            if (!_speedLevels.TryGetValue(player.Id, out var speedLevel))
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

        if (_visibilityCoroutineHandles.TryGetValue(player.Id, out var visibilityHandle))
            Timing.KillCoroutines(visibilityHandle);

        if (_speedCoroutineHandles.TryGetValue(player.Id, out var speedHandle))
            Timing.KillCoroutines(speedHandle);

        if (_flickerCoroutineHandles.TryGetValue(player.Id, out var flickerHandle))
            Timing.KillCoroutines(flickerHandle);

        _flickerCoroutineHandles.Remove(player.Id);
        _visibilityCoroutineHandles.Remove(player.Id);
        _speedCoroutineHandles.Remove(player.Id);
        _invisibleEffectivePlayers.Remove(player.Id);
        _speedLevels.Remove(player.Id);
        RoleSpecificTextProvider.Clear(player);
        PlayerVisibilitySyncProvider.ShowToAll(player);

        // 最後の 966 が居なくなったら、暗くしたままのプレイヤーを明るく戻す。
        if (_visibilityCoroutineHandles.Count == 0)
            RestoreViewerLights();
    }
}
