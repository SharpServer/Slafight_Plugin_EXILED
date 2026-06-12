using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using InventorySystem.Items.Usables.Scp1344;
using MEC;
using PlayerRoles.FirstPersonControl.Thirdperson.Subcontrollers.Wearables;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.CustomItems;

/// <summary>
/// NVG ライトの挙動をアイテムごとに定義するプロファイル。
/// </summary>
public readonly struct NvgProfile
{
    /// <summary>1秒あたりの電池消費量（%）。0 以下で無限。</summary>
    public float DrainPerSecond { get; init; }
    public Color LightColor     { get; init; }
    public float LightRange     { get; init; }
    public float LightIntensity { get; init; }
    /// <summary>true のとき、電池切れで Blindness を最大強度で付与する。</summary>
    public bool  UseBlackout    { get; init; }

    public static NvgProfile Default => new()
    {
        DrainPerSecond = 1.85f,
        LightColor     = new Color(0.6f, 1f, 0.6f),
        LightRange     = 30f,
        LightIntensity = 10000f,
        UseBlackout    = true,
    };
}

/// <summary>
/// NVG のライト・視界効果とバッテリー管理を行うマネージャ。
/// NetworkVisibilityManager の Owner / Spectator API を使って可視管理を行う。
/// </summary>
public static class NvgManager
{
    private const float MaxBattery   = 100f;
    private const float TickInterval = 0.1f;

    private static readonly Dictionary<ushort, NvgRuntimeData> ActiveData  = new();
    private static readonly Dictionary<ushort, float>          BatteryData = new();
    private static bool _registered;

    // --------------------------------------------------------
    // イベント登録 / 解除
    // --------------------------------------------------------

    public static void Register()
    {
        if (_registered) return;

        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
        Exiled.Events.Handlers.Server.RestartingRound += OnRoundRestarting;
        Exiled.Events.Handlers.Player.ChangingRole  += OnChangingRole;
        Exiled.Events.Handlers.Player.Died          += OnDied;
        Exiled.Events.Handlers.Player.Left          += OnLeft;
        Exiled.Events.Handlers.Player.ItemRemoved   += OnItemRemoved;

        _registered = true;
        // Verified / ChangingSpectatedPlayer / Spawned は NetworkVisibilityManager が処理するため不要
    }

    public static void Unregister()
    {
        if (!_registered)
        {
            ClearRuntimeState(clearBattery: true);
            return;
        }

        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
        Exiled.Events.Handlers.Server.RestartingRound -= OnRoundRestarting;
        Exiled.Events.Handlers.Player.ChangingRole  -= OnChangingRole;
        Exiled.Events.Handlers.Player.Died          -= OnDied;
        Exiled.Events.Handlers.Player.Left          -= OnLeft;
        Exiled.Events.Handlers.Player.ItemRemoved   -= OnItemRemoved;

        _registered = false;
        ClearRuntimeState(clearBattery: true);
    }

    // --------------------------------------------------------
    // イベントハンドラ
    // --------------------------------------------------------

    private static void OnRoundStarted()
        => ClearRuntimeState(clearBattery: true);

    private static void OnRoundRestarting()
        => ClearRuntimeState(clearBattery: true);

    private static void ClearRuntimeState(bool clearBattery)
    {
        foreach (var data in ActiveData.Values)
            KillRuntimeData(data);
        ActiveData.Clear();
        if (clearBattery)
            BatteryData.Clear();
    }

    private static void OnChangingRole(ChangingRoleEventArgs? ev)
    {
        if (ev?.Player == null) return;
        if (!ev.IsAllowed) return;
        StopAllNvgByOwner(ev.Player, clearBattery: false);
    }

    private static void OnDied(DiedEventArgs? ev)
    {
        if (ev?.Player == null) return;
        StopAllNvgByOwner(ev.Player, clearBattery: false);
    }

    private static void OnLeft(LeftEventArgs? ev)
    {
        if (ev?.Player == null) return;
        StopAllNvgByOwner(ev.Player, clearBattery: false);
    }

    private static void OnItemRemoved(ItemRemovedEventArgs? ev)
    {
        if (ev?.Item == null) return;
        StopNvgBySerial(ev.Item.Serial, clearBattery: false);
    }

    // --------------------------------------------------------
    // 公開 API
    // --------------------------------------------------------

    /// <summary>NVG を起動する。プロファイル未指定時は NvgProfile.Default を使用。</summary>
    public static void StartNvg(Player player, ushort serial, NvgProfile? profile = null)
    {
        if (player == null) return;
        if (!IsValidOwner(player) || !PlayerHasSerial(player, serial))
        {
            Log.Debug($"[NvgManager] StartNvg拒否: 所有者/アイテム不整合 serial={serial}");
            return;
        }

        var prof = profile ?? NvgProfile.Default;
        Log.Debug($"[NvgManager] StartNvg: {player.Nickname} serial={serial} drain={prof.DrainPerSecond}/s");

        bool isInfinite = prof.DrainPerSecond <= 0f;

        // 電池0%なら起動しない（無限電池は常に起動可）
        if (!isInfinite && BatteryData.TryGetValue(serial, out var savedBattery) && savedBattery <= 0f)
        {
            player.ShowHint("このNVGの電池は完全に切れています。", 3f);
            Log.Debug($"[NvgManager] StartNvg拒否: 電池切れ serial={serial}");
            return;
        }

        // 同一シリアルまたは同一所有者の古いライトを必ず消してから作り直す。
        StopNvgBySerial(serial, clearBattery: false);
        StopAllNvgByOwner(player, clearBattery: false);

        float battery = isInfinite ? MaxBattery
                      : BatteryData.TryGetValue(serial, out var saved) ? saved : MaxBattery;

        var light = CreateNvgLight(player, prof);
        if (light == null)
        {
            Log.Error($"[NvgManager] StartNvg: NVGライト生成失敗 ({player.Nickname})");
            return;
        }

        var data = new NvgRuntimeData
        {
            Serial          = serial,
            OwnerId         = player.Id,
            NvgLight        = light,
            Profile         = prof,
            CoroutineHandle = Timing.RunCoroutine(BatteryLoop(player, serial)),
        };

        BatteryData[serial] = battery;
        ActiveData[serial]  = data;

        ApplyOwnerVisibility(player, light.Base?.netIdentity);
    }

    /// <summary>NVG を停止する。電池残量は保持される。</summary>
    public static void StopNvg(Player player, ushort serial)
    {
        if (player == null) return;
        if (ActiveData.TryGetValue(serial, out var data) && data.OwnerId != player.Id)
            return;

        StopNvgBySerial(serial, clearBattery: false);
    }

    // --------------------------------------------------------
    // 内部停止処理
    // --------------------------------------------------------

    /// <param name="clearBattery">true = 電池データも削除 / false = ライトのみ破棄</param>
    private static void StopNvgBySerial(ushort serial, bool clearBattery)
    {
        if (!ActiveData.TryGetValue(serial, out var data))
        {
            if (clearBattery)
                BatteryData.Remove(serial);
            return;
        }

        // NVG由来のブラックアウトを解除
        TryDisableBlackout(data);

        // 破棄前に全員へ明示 Hide を送る。
        // 一度でも通常プレイヤーへスポーンが届いた場合の残留をここで潰す。
        HideFromAll(data.NvgLight?.Base?.netIdentity);

        ActiveData.Remove(serial);
        KillRuntimeData(data);

        if (clearBattery)
            BatteryData.Remove(serial);
    }

    private static void StopAllNvgByOwner(Player player, bool clearBattery)
    {
        if (player == null) return;

        foreach (var serial in ActiveData
                     .Where(kv => kv.Value.OwnerId == player.Id)
                     .Select(kv => kv.Key)
                     .ToList())
        {
            StopNvgBySerial(serial, clearBattery);
        }
    }
    
    private static void TryDisableBlackout(NvgRuntimeData? data)
    {
        if (data == null) return;
        if (!data.Profile.UseBlackout) return;

        try
        {
            Player player = Player.Get(data.OwnerId);

            if (player == null || !player.IsConnected)
                return;

            player.DisableEffect<Blindness>();
        }
        catch (Exception ex)
        {
            Log.Warn($"[NvgManager] TryDisableBlackout failed: {ex.Message}");
        }
    }

    // --------------------------------------------------------
    // 可視状態同期ユーティリティ
    // --------------------------------------------------------

    /// <summary>
    /// owner 本人と owner を現在観戦中のプレイヤーだけに identity を表示し、
    /// それ以外の接続プレイヤーには明示 Hide を送る。
    /// </summary>
    private static void ApplyOwnerVisibility(Player owner, Mirror.NetworkIdentity? identity)
    {
        if (owner == null || identity == null) return;

        foreach (var target in Player.List)
        {
            if (target == null || !target.IsConnected) continue;

            bool shouldShow = target.Id == owner.Id ||
                              (!target.IsAlive && target.CurrentSpectatingPlayers.Any(s => s?.Id == owner.Id));

            if (shouldShow) target.ShowNetworkIdentity(identity);
            else            target.HideNetworkIdentity(identity);
        }
    }

    private static void HideFromAll(Mirror.NetworkIdentity? identity)
    {
        if (identity == null) return;

        foreach (var target in Player.List)
        {
            if (target == null || !target.IsConnected) continue;
            target.HideNetworkIdentity(identity);
        }
    }

    // --------------------------------------------------------
    // ライト生成
    // --------------------------------------------------------

    private static Light? CreateNvgLight(Player player, NvgProfile prof)
    {
        try
        {
            var light = Light.Create(
                player.CameraTransform.position,
                player.Rotation.eulerAngles,
                null,
                spawn: false);

            if (light?.Base == null || light.Base.netIdentity == null)
            {
                Log.Error("[NvgManager] CreateNvgLight: Light または netIdentity が null");
                return null;
            }

            light.Range     = 0f;
            light.Intensity = 0f;
            light.Color     = prof.LightColor;
            light.Transform.SetParent(player.Transform, true);
            light.Spawn();

            if (light.Base?.netIdentity == null)
            {
                Log.Error("[NvgManager] CreateNvgLight: Spawn 後の netIdentity が null");
                light.SafeDestroy();
                return null;
            }

            light.InitShowState(new NetworkShowState
            {
                OwnerId             = player.Id,
                ShowToOwner         = true,
                SpectatorVisibility = SpectatorVisibility.Show,
            });

            light.Range     = prof.LightRange;
            light.Intensity = prof.LightIntensity;

            return light;
        }
        catch (Exception ex)
        {
            Log.Error($"[NvgManager] CreateNvgLight 例外: {ex}");
            return null;
        }
    }

    // --------------------------------------------------------
    // バッテリーループ
    // --------------------------------------------------------

    private static void KillRuntimeData(NvgRuntimeData? data)
    {
        if (data == null) return;

        Timing.KillCoroutines(data.CoroutineHandle);

        HideFromAll(data.NvgLight?.Base?.netIdentity);
        data.NvgLight?.SafeDestroy();
        data.NvgLight = null;
    }

    private static IEnumerator<float> BatteryLoop(Player player, ushort serial)
    {
        while (true)
        {
            yield return Timing.WaitForSeconds(TickInterval);

            if (player == null || !player.IsConnected) yield break;
            if (!ActiveData.TryGetValue(serial, out var data)) yield break;
            if (data.OwnerId != player.Id) yield break;

            if (!player.IsAlive || data.NvgLight?.Base == null)
            {
                StopNvgBySerial(serial, clearBattery: false);
                yield break;
            }

            // インベントリからアイテムが消えていたら強制停止
            // （コマンドによるクリア・その他の異常経路でアイテムが失われた場合の保険）
            if (!PlayerHasSerial(player, serial))
            {
                Log.Debug($"[NvgManager] BatteryLoop: serial={serial} がインベントリから消えた。強制停止。");
                DisableNvgWearable(player);
                StopNvgBySerial(serial, clearBattery: false);
                yield break;
            }

            var prof = data.Profile;

            // 無限電池はそのまま継続
            if (prof.DrainPerSecond <= 0f) continue;

            float battery = BatteryData.TryGetValue(serial, out var b) ? b : 0f;

            if (battery <= 0f)
            {
                BatteryData[serial] = 0f;

                // 電池切れ中は StopNvg されるまで常に真っ暗にする。
                // TickInterval より少し長めに付与して、更新ズレで一瞬切れないようにする。
                if (prof.UseBlackout)
                    player.EnableEffect<Blindness>(255);

                // ライトは消灯状態にするが、NVG自体の Runtime は残す。
                if (data.NvgLight?.Base != null)
                {
                    data.NvgLight.Intensity = 0f;
                    data.NvgLight.Range = 0f;
                }

                player.ShowHint("NVGの電池が切れた...視界が真っ暗になった。", 1f);

                continue;
            }

            // 電池消費
            float drain = prof.DrainPerSecond * TickInterval;
            battery = Math.Max(0f, battery - drain);
            BatteryData[serial] = battery;

            // 残量に応じて輝度を落とす
            if (data.NvgLight?.Base != null)
            {
                float ratio = battery / MaxBattery;
                data.NvgLight.Intensity = prof.LightIntensity * (0.4f + 0.6f * ratio);
            }

            player.ShowHint($"NVG電池: {(int)battery}%", 1f);
        }
    }

    private static bool IsValidOwner(Player player)
        => player != null && player.IsConnected && player.IsAlive;

    private static bool PlayerHasSerial(Player player, ushort serial)
        => player != null && player.Items.Any(i => i != null && i.Serial == serial);

    private static void DisableNvgWearable(Player? player)
    {
        try
        {
            player?.ReferenceHub?.DisableWearables(WearableElements.Scp1344Goggles);
        }
        catch (Exception ex)
        {
            Log.Warn($"[NvgManager] DisableNvgWearable failed: {ex.Message}");
        }
    }

    private class NvgRuntimeData
    {
        public ushort          Serial          { get; set; }
        public int             OwnerId         { get; set; }
        public Light?          NvgLight        { get; set; }
        public CoroutineHandle CoroutineHandle { get; set; }
        public NvgProfile      Profile         { get; set; }
    }
    
    public static bool TryGetBattery(ushort serial, out float battery)
    {
        battery = 0f;
        return BatteryData.TryGetValue(serial, out battery);
    }

    public static float GetBattery(ushort serial, float fallback = 0f)
        => BatteryData.TryGetValue(serial, out var battery) ? battery : fallback;

    public static bool HasBattery(ushort serial)
        => BatteryData.TryGetValue(serial, out var battery) && battery > 0f;

    public static bool TrySetBattery(ushort serial, float battery, bool reviveIfDead = false)
    {
        battery = Mathf.Clamp(battery, 0f, MaxBattery);
        BatteryData[serial] = battery;

        if (ActiveData.TryGetValue(serial, out var data) && data.NvgLight != null)
        {
            if (battery <= 0f)
            {
                data.NvgLight.Intensity = 0f;
                data.NvgLight.Range = 0f;
            }
            else
            {
                float ratio = battery / MaxBattery;
                data.NvgLight.Intensity = data.Profile.LightIntensity * (0.4f + 0.6f * ratio);
                data.NvgLight.Range = data.Profile.LightRange;
            }
        }

        if (reviveIfDead && battery > 0f && ActiveData.TryGetValue(serial, out var active))
        {
            var owner = Player.Get(active.OwnerId);
            if (owner != null && owner.IsConnected && owner.IsAlive)
            {
                if (active.Profile.UseBlackout)
                    owner.DisableEffect<Blindness>();
            }
        }

        return true;
    }

    public static bool AddBattery(ushort serial, float amount)
    {
        var current = GetBattery(serial, 0f);
        return TrySetBattery(serial, current + amount);
    }

    public static bool DrainBattery(ushort serial, float amount)
    {
        var current = GetBattery(serial, 0f);
        return TrySetBattery(serial, current - amount);
    }

    public static bool CanStartNvg(ushort serial)
        => BatteryData.TryGetValue(serial, out var battery) ? battery > 0f : true;

    public static bool IsActive(ushort serial)
        => ActiveData.ContainsKey(serial);
    
    public static bool TryGetPlayerNvgItem(Player? player, ushort serial, out Scp1344Item? item)
    {
        item = null;
        if (player == null) return false;

        item = player.Items.OfType<Scp1344Item>().FirstOrDefault(i => i != null && i.ItemSerial == serial);
        return item != null;
    }
}
