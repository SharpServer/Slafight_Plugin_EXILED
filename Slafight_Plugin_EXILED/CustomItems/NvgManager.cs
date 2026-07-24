using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using InventorySystem.Items.Usables.Scp1344;
using MEC;
using Mirror;
using PlayerRoles.FirstPersonControl.Thirdperson.Subcontrollers.Wearables;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using ExiledScp1344 = Exiled.API.Features.Items.Scp1344;
using Light = Exiled.API.Features.Toys.Light;
using Server = Exiled.Events.Handlers.Server;

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
    /// <summary>装着中に維持する Blindness 強度。0 で装着中 Blindness なし。</summary>
    public byte  WornBlindnessIntensity { get; init; }
    /// <summary>true のとき、電池切れで Blindness を最大強度で付与する。</summary>
    public bool  UseBlackout    { get; init; }

    public static NvgProfile Default => new()
    {
        DrainPerSecond = 1.85f,
        LightColor     = new Color(0.6f, 1f, 0.6f),
        LightRange     = 30f,
        LightIntensity = 10000f,
        WornBlindnessIntensity = Blindness.MinIntensity,
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
    private static readonly Dictionary<int, string>            LastNvgInfoByOwner = new();
    private static bool _registered;

    // --------------------------------------------------------
    // イベント登録 / 解除
    // --------------------------------------------------------

    public static void Register()
    {
        if (_registered) return;

        Server.RoundStarted += OnRoundStarted;
        Server.RestartingRound += OnRoundRestarting;
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

        Server.RoundStarted -= OnRoundStarted;
        Server.RestartingRound -= OnRoundRestarting;
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
        {
            ClearNvgInfo(data);
            KillRuntimeData(data);
        }

        ActiveData.Clear();
        LastNvgInfoByOwner.Clear();

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

    public static bool IsManagedNvg(Scp1344Item? item)
    {
        if (item == null) return false;

        return CItem.SerialTracker.TryGet(item.ItemSerial, out var cItem)
               && cItem is CItemNvg;
    }

    public static void ReapplyManagedBlindness(Scp1344Item? item)
    {
        if (!IsManagedNvg(item)) return;

        try
        {
            var player = Player.Get(item.Owner);
            if (player == null || !player.IsConnected)
                return;

            if (!ActiveData.ContainsKey(item.ItemSerial))
                return;

            if (ShouldApplyBlackout(item.ItemSerial))
            {
                item.BlindnessEffect.Intensity = 255;
            }
            else if (TryGetWornBlindnessIntensity(item.ItemSerial, out var wornIntensity))
            {
                item.BlindnessEffect.Intensity = wornIntensity;
            }
            else
            {
                player.DisableEffect<Blindness>();
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[NvgManager] ReapplyManagedBlindness failed: {ex.Message}");
        }
    }

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

        float battery = isInfinite ? MaxBattery
                      : BatteryData.TryGetValue(serial, out var saved) ? saved : MaxBattery;
        battery = Mathf.Clamp(battery, 0f, MaxBattery);

        if (!isInfinite && battery <= 0f)
            Log.Debug($"[NvgManager] StartNvg: 電池切れ状態で起動 serial={serial}");

        // 同一シリアルまたは同一所有者の古いライトを必ず消してから作り直す。
        StopNvgBySerial(serial, clearBattery: false);
        StopAllNvgByOwner(player, clearBattery: false);

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

        if (TryGetPlayerNvgItem(player, serial, out var nvgItem))
            ReapplyManagedBlindness(nvgItem);

        UpdateNvgInfo(player, serial);
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
        ClearNvgInfo(data);

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
    private static void ApplyOwnerVisibility(Player owner, NetworkIdentity? identity)
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

    private static void HideFromAll(NetworkIdentity? identity)
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
            if (prof.DrainPerSecond <= 0f)
            {
                if (TryGetPlayerNvgItem(player, serial, out var infiniteNvgItem))
                    ReapplyManagedBlindness(infiniteNvgItem);

                UpdateNvgInfo(player, serial);
                continue;
            }

            float battery = BatteryData.TryGetValue(serial, out var b) ? b : 0f;

            if (battery <= 0f)
            {
                BatteryData[serial] = 0f;

                // 電池切れ中は StopNvg されるまで常に真っ暗にする。
                // TickInterval より少し長めに付与して、更新ズレで一瞬切れないようにする。
                if (prof.UseBlackout)
                {
                    if (TryGetPlayerNvgItem(player, serial, out var blackoutNvgItem))
                        ReapplyManagedBlindness(blackoutNvgItem);
                    else
                        player.EnableEffect<Blindness>(255);
                }

                // ライトは消灯状態にするが、NVG自体の Runtime は残す。
                if (data.NvgLight?.Base != null)
                {
                    data.NvgLight.Intensity = 0f;
                    data.NvgLight.Range = 0f;
                }

                UpdateNvgInfo(player, serial);

                continue;
            }

            // 電池消費
            float drain = prof.DrainPerSecond * TickInterval;
            battery = Math.Max(0f, battery - drain);
            BatteryData[serial] = battery;

            if (TryGetPlayerNvgItem(player, serial, out var nvgItem))
                ReapplyManagedBlindness(nvgItem);

            UpdateNvgInfo(player, serial);

            // 残量に応じて輝度を落とす
            if (data.NvgLight?.Base != null)
            {
                float ratio = battery / MaxBattery;
                data.NvgLight.Intensity = prof.LightIntensity * (0.4f + 0.6f * ratio);
            }

        }
    }

    private static bool IsValidOwner(Player player)
        => player != null && player.IsConnected && player.IsAlive;

    private static bool PlayerHasSerial(Player player, ushort serial)
        => player != null && player.Items.Any(i => i != null && i.Serial == serial);

    private static bool ShouldApplyBlackout(ushort serial)
        => ActiveData.TryGetValue(serial, out var data)
           && data.Profile.UseBlackout
           && BatteryData.TryGetValue(serial, out var battery)
           && battery <= 0f;

    private static bool TryGetWornBlindnessIntensity(ushort serial, out byte intensity)
    {
        intensity = 0;
        if (!ActiveData.TryGetValue(serial, out var data))
            return false;

        intensity = data.Profile.WornBlindnessIntensity;
        return intensity > 0;
    }

    private static void UpdateNvgInfo(Player? player, ushort serial)
    {
        if (player == null || !player.IsConnected) return;
        if (!ActiveData.TryGetValue(serial, out var data)) return;

        bool isInfinite = data.Profile.DrainPerSecond <= 0f;
        float battery = isInfinite
            ? MaxBattery
            : BatteryData.TryGetValue(serial, out var storedBattery) ? storedBattery : 0f;
        bool blackout = !isInfinite && battery <= 0f && data.Profile.UseBlackout;

        string text = BuildNvgInfoText(battery, isInfinite, blackout);
        if (LastNvgInfoByOwner.TryGetValue(player.Id, out var previous) && previous == text)
            return;

        LastNvgInfoByOwner[player.Id] = text;
        EffectedInfoTextProvider.Set(player, text);
    }

    private static string BuildNvgInfoText(float battery, bool isInfinite, bool blackout)
    {
        if (blackout)
            return "<color=#ff5555>NVG電池: 0% - 電池切れ</color>";

        if (isInfinite)
            return "<color=#88ff88>NVG電池: ∞</color>";

        int percent = Mathf.Clamp((int)battery, 0, 100);
        string color = percent <= 15 ? "#ffdd66" : "#88ff88";
        return $"<color={color}>NVG電池: {percent}%</color>";
    }

    private static void ClearNvgInfo(NvgRuntimeData? data)
    {
        if (data == null) return;

        try
        {
            var player = Player.Get(data.OwnerId);
            if (player == null || !player.IsConnected)
            {
                LastNvgInfoByOwner.Remove(data.OwnerId);
                return;
            }

            if (LastNvgInfoByOwner.Remove(player.Id))
                EffectedInfoTextProvider.Clear(player);
        }
        catch (Exception ex)
        {
            Log.Warn($"[NvgManager] ClearNvgInfo failed: {ex.Message}");
        }
    }

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
                if (TryGetPlayerNvgItem(owner, serial, out var nvgItem))
                    ReapplyManagedBlindness(nvgItem);
                else if (active.Profile.UseBlackout)
                    owner.DisableEffect<Blindness>();
            }
        }

        if (ActiveData.TryGetValue(serial, out var activeInfo))
        {
            var owner = Player.Get(activeInfo.OwnerId);
            if (owner != null && owner.IsConnected)
                UpdateNvgInfo(owner, serial);
        }

        return true;
    }

    public static bool AddBattery(ushort serial, float amount)
    {
        var current = GetBattery(serial);
        return TrySetBattery(serial, current + amount);
    }

    public static bool DrainBattery(ushort serial, float amount)
    {
        var current = GetBattery(serial);
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

        item = player.Items
            .OfType<ExiledScp1344>()
            .FirstOrDefault(i => i != null && i.Serial == serial)
            ?.Base;

        return item != null;
    }
}
