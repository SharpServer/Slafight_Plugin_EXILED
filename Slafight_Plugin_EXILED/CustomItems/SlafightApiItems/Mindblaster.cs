using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Player;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomRoles.Others.SergeyMakarov;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class Mindblaster : CItemKeycard
{
    public const int InfiniteShots = -1;

    private sealed class SerialState
    {
        public int RemainingShots;
        public float ChargeSeconds;
        public bool IsCharging;
        public float ChargeStartedAt;
        public CoroutineHandle ChargeHandle;
    }

    public readonly struct SerialStatus
    {
        internal SerialStatus(int remainingShots, float chargeSeconds, bool isCharging, float remainingChargeSeconds)
        {
            RemainingShots = remainingShots;
            ChargeSeconds = chargeSeconds;
            IsCharging = isCharging;
            RemainingChargeSeconds = remainingChargeSeconds;
        }

        public int RemainingShots { get; }
        public float ChargeSeconds { get; }
        public bool IsCharging { get; }
        public float RemainingChargeSeconds { get; }
        public bool HasInfiniteShots => RemainingShots < 0;
    }

    private static readonly Dictionary<ushort, SerialState> SerialStates = [];

    public static int DefaultShots { get; set; } = InfiniteShots;
    public static float DefaultChargeSeconds { get; set; } = 10f;

    public override string DisplayName => "第五思壊線";
    public override string Description => $"非常に<color={CTeam.Fifthists.GetTeamColor()}>第五的な光</color>を発射し思考を破壊する";
    protected override string UniqueKey => "Mindblaster";
    protected override ItemType BaseItem => ItemType.KeycardCustomTaskForce;
    protected override string KeycardLabel => "Mindblaster";
    protected override Color32? KeycardLabelColor => new Color32(255, 0, 250, 255);
    protected override string KeycardName => "Mgc. Fifth";
    protected override Color32? TintColor => new Color32(255, 0, 250, 255);
    protected override Color32? KeycardPermissionsColor => new Color32(255, 255, 255, 255);
    protected override KeycardPermissions Permissions => KeycardPermissions.None;
    protected override byte Rank => 1;
    protected override string SerialNumber => "555555555555";
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.magenta;

    /// <summary>
    /// Serial ごとの残り発射回数とチャージ時間を設定する。
    /// remainingShots が負数の場合は発射回数を無限として扱う。
    /// </summary>
    public static void SetSerialSettings(ushort serial, int remainingShots, float chargeSeconds)
    {
        if (!SerialStates.TryGetValue(serial, out var state))
        {
            state = new SerialState();
            SerialStates[serial] = state;
        }

        state.RemainingShots = NormalizeShots(remainingShots);
        state.ChargeSeconds = Mathf.Max(0f, chargeSeconds);
    }

    public static bool TryGetSerialStatus(ushort serial, out SerialStatus status)
    {
        if (!SerialStates.TryGetValue(serial, out var state))
        {
            status = default;
            return false;
        }

        var remainingChargeSeconds = state.IsCharging
            ? Mathf.Max(0f, state.ChargeSeconds - (Time.time - state.ChargeStartedAt))
            : 0f;

        status = new SerialStatus(
            state.RemainingShots,
            state.ChargeSeconds,
            state.IsCharging,
            remainingChargeSeconds);
        return true;
    }

    public override void UnregisterEvents()
    {
        ClearSerialStates();
        base.UnregisterEvents();
    }

    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        GetOrCreateState(ev.Item.Serial);
        base.OnAcquired(ev, displayMessage);
    }

    protected override void OnSpawned(Pickup pickup)
    {
        GetOrCreateState(pickup.Serial);
        base.OnSpawned(pickup);
    }

    protected override void OnWaitingForPlayers()
    {
        ClearSerialStates();
        base.OnWaitingForPlayers();
    }

    protected override void OnSerialUntracked(ushort serial)
    {
        RemoveSerialState(serial);
        base.OnSerialUntracked(serial);
    }

    /// <summary>
    /// 投げる (Drop) で SCP3005 schematic を発射し、チャージ中カードへ切り替える。
    /// 軌道上の他プレイヤーに継続ダメージを与える。
    /// </summary>
    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        var serial = ev.Item.Serial;
        var state = GetOrCreateState(serial);

        // チャージ中の灰色カードは通常どおり受け渡し・ドロップできる。
        if (state.IsCharging) return;

        ev.IsAllowed = false;

        if (state.RemainingShots == 0)
        {
            ev.Player.ShowHint("<size=23>第五思壊線の発射回数を使い切っています。</size>", 3f);
            return;
        }

        try
        {
            var schem = ObjectSpawner.SpawnSchematic("SCP3005", ev.Player.Position, ev.Player.CameraTransform.forward);
            Timing.RunCoroutine(MissileCoroutine(schem, ev.Player));
        }
        catch (Exception ex)
        {
            Log.Error($"[Mindblaster] Schematic spawn failed: {ex}");
            ev.Player.ShowHint("<size=23>第五思壊線の発射に失敗しました。</size>", 3f);
            return;
        }

        if (state.RemainingShots > 0)
            state.RemainingShots--;

        if (state.RemainingShots == 0)
        {
            RemoveSerialState(serial);
            SerialTracker.ForceUnregister(serial);
            ev.Player.RemoveItem(ev.Item, destroy: true);
            ev.Player.ShowHint("<size=23>第五思壊線の発射回数を使い切りました。</size>", 3f);
            return;
        }

        state.IsCharging = true;
        state.ChargeStartedAt = Time.time;

        if (!TryReplaceInventoryItem(ev.Player, ev.Item, ItemType.KeycardJanitor, false, out _))
        {
            Log.Error($"[Mindblaster] Failed to create charging keycard for serial={serial}.");
            RemoveSerialState(serial);
            return;
        }

        state.ChargeHandle = Timing.RunCoroutine(ChargeCoroutine(serial, state));
        ev.Player.ShowHint(
            $"<size=23>第五思壊線をチャージ中です（{state.ChargeSeconds:0.#}秒）。</size>",
            3f);
    }

    private IEnumerator<float> ChargeCoroutine(ushort serial, SerialState expectedState)
    {
        while (SerialStates.TryGetValue(serial, out var state) &&
               ReferenceEquals(state, expectedState) &&
               state.IsCharging)
        {
            if (Round.IsLobby || Round.IsEnded)
            {
                RemoveSerialState(serial, killCoroutine: false);
                yield break;
            }

            if (Time.time - state.ChargeStartedAt >= state.ChargeSeconds)
                break;

            yield return Timing.WaitForSeconds(0.1f);
        }

        // Pickup -> Item の切り替え瞬間に重なっても消失しないよう、数フレーム再試行する。
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (!SerialStates.TryGetValue(serial, out var state) ||
                !ReferenceEquals(state, expectedState) ||
                !state.IsCharging)
                yield break;

            if (TryCompleteRecharge(serial, state))
                yield break;

            yield return Timing.WaitForOneFrame;
        }

        Log.Warn($"[Mindblaster] Charging card location was not found for serial={serial}; discarding state.");
        RemoveSerialState(serial, killCoroutine: false);
        SerialTracker.ForceUnregister(serial);
    }

    private bool TryCompleteRecharge(ushort serial, SerialState state)
    {
        foreach (var player in Player.List)
        {
            var chargingItem = player?.Items.FirstOrDefault(item => item?.Serial == serial);
            if (chargingItem == null) continue;

            if (!TryReplaceInventoryItem(player, chargingItem, BaseItem, true, out _))
                return false;

            state.IsCharging = false;
            state.ChargeHandle = default;
            player.ShowHint($"<size=23>第五思壊線のチャージが完了しました。\n{BuildShotsText(state)}</size>", 3f);
            return true;
        }

        var chargingPickup = Pickup.Get(serial);
        if (chargingPickup == null) return false;

        var position = chargingPickup.Position;
        var rotation = chargingPickup.Rotation;

        SerialTracker.ForceUnregister(serial);
        chargingPickup.Destroy();

        var item = Item.Create(BaseItem);
        if (item == null) return false;

        item.Serial = serial;
        ApplyKeycardCustomization(item);

        var rechargedPickup = item.CreatePickup(position, rotation, spawn: false);
        if (rechargedPickup == null) return false;

        SerialTracker.ForceRegister(serial, this);
        rechargedPickup.Spawn();

        state.IsCharging = false;
        state.ChargeHandle = default;
        return true;
    }

    private bool TryReplaceInventoryItem(
        Player player,
        Item oldItem,
        ItemType replacementType,
        bool customizeAsMindblaster,
        out Item replacement)
    {
        replacement = null;
        if (player == null || oldItem == null) return false;

        var serial = oldItem.Serial;
        var wasHeld = player.CurrentItem?.Serial == serial;

        var created = Item.Create(replacementType);
        if (created == null) return false;

        created.Serial = serial;

        SerialTracker.ForceUnregister(serial);
        player.RemoveItem(oldItem, destroy: true);

        replacement = player.AddItem(created.Base, created);
        if (replacement == null) return false;

        if (customizeAsMindblaster)
            ApplyKeycardCustomization(replacement);

        SerialTracker.ForceRegister(serial, this);

        if (wasHeld)
            player.CurrentItem = replacement;

        return true;
    }

    private static string BuildShotsText(SerialState state)
        => state.RemainingShots < 0
            ? "残り発射回数: ∞"
            : $"残り発射回数: {state.RemainingShots}";

    private static int NormalizeShots(int shots)
        => shots < 0 ? InfiniteShots : shots;

    private static SerialState GetOrCreateState(ushort serial)
    {
        if (SerialStates.TryGetValue(serial, out var state))
            return state;

        state = new SerialState
        {
            RemainingShots = NormalizeShots(DefaultShots),
            ChargeSeconds = Mathf.Max(0f, DefaultChargeSeconds),
        };
        SerialStates[serial] = state;
        return state;
    }

    private static void RemoveSerialState(ushort serial, bool killCoroutine = true)
    {
        if (!SerialStates.TryGetValue(serial, out var state)) return;

        SerialStates.Remove(serial);
        if (killCoroutine && state.ChargeHandle.IsValid)
            Timing.KillCoroutines(state.ChargeHandle);
    }

    private static void ClearSerialStates()
    {
        foreach (var state in SerialStates.Values)
        {
            if (state.ChargeHandle.IsValid)
                Timing.KillCoroutines(state.ChargeHandle);
        }

        SerialStates.Clear();
    }

    private static IEnumerator<float> MissileCoroutine(SchematicObject schem, Player pushPlayer)
    {
        if (schem == null || schem.transform == null) yield break;

        const float totalDuration = 0.8f;
        var elapsed = 0f;
        var startPos = schem.transform.position;
        var forward = pushPlayer != null ? pushPlayer.CameraTransform.forward.normalized : Vector3.forward;
        var endPos = startPos + forward * 25f + new Vector3(0f, 0.15f, 0f);

        while (elapsed < totalDuration)
        {
            if (Round.IsLobby || Round.IsEnded) break;
            if (schem == null || schem.transform == null) break;
            if (pushPlayer != null && !pushPlayer.IsConnected) break;

            foreach (var player in Player.List)
            {
                if (player == null || !player.IsConnected || !player.IsAlive) continue;
                if (player == pushPlayer) continue;
                if (Vector3.Distance(schem.transform.position, player.Transform.position) > 1f) continue;

                try
                {
                    player.EnableEffect<Burned>(255, 15);
                    player.EnableEffect<Concussed>(255, 15);
                    player.EnableEffect<Asphyxiated>(1, 15);
                    player.Hurt(pushPlayer, 10f, DamageType.Unknown, null,
                        !pushPlayer.IsSergeyMarkov()
                            ? "<color=#ff00fa>第五的</color>な力による影響"
                            : "<color=red><b>怨念的</b></color>な力による影響");
                    pushPlayer?.ShowHitMarker();
                }
                catch (Exception ex)
                {
                    Log.Error($"[Mindblaster] Hurt error: {ex}");
                }
            }

            elapsed += Time.deltaTime;
            schem.transform.position = Vector3.Lerp(startPos, endPos, elapsed / totalDuration);
            yield return 0f;
        }

        try { schem?.Destroy(); }
        catch (Exception ex) { Log.Error($"[Mindblaster] Error destroying schem: {ex}"); }
    }
}
