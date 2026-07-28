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

public class Mindblaster : CItemHybrid
{
    public const int InfiniteShots = -1;

    private sealed class RuntimeState
    {
        public ushort Serial;
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

    private static readonly Dictionary<ushort, RuntimeState> States = [];

    private readonly ReadyCard _readyCard;
    private readonly ChargingCard _chargingCard;
    private RuntimeState _pendingRuntimeState;

    public Mindblaster()
    {
        _readyCard = new ReadyCard(this);
        _chargingCard = new ChargingCard(this);
    }

    public static int DefaultShots { get; set; } = InfiniteShots;
    public static float DefaultChargeSeconds { get; set; } = 10f;

    public override string DisplayName => "第五思壊線";
    public override string Description =>
        $"非常に<color={CTeam.Fifthists.GetTeamColor()}>第五的な光</color>を発射し思考を破壊する";

    protected override string UniqueKey => "Mindblaster";
    protected override bool EnableKeyModeSwitch => false;
    protected override bool ShowModeSwitchHint => false;

    protected override List<CItemHybridMode> BuildSubModes()
        =>
        [
            new(_readyCard, "発射可能"),
            new(_chargingCard, "チャージ中"),
        ];

    /// <summary>
    /// Serial ごとの残り発射回数とチャージ時間を設定する。
    /// remainingShots が負数の場合は発射回数を無限として扱う。
    /// Hybrid のモード切替後は、新しい Serial へ設定が自動的に引き継がれる。
    /// </summary>
    public static void SetSerialSettings(ushort serial, int remainingShots, float chargeSeconds)
    {
        var state = GetOrCreateState(serial);
        state.RemainingShots = NormalizeShots(remainingShots);
        state.ChargeSeconds = Mathf.Max(0f, chargeSeconds);
    }

    public static bool TryGetSerialStatus(ushort serial, out SerialStatus status)
    {
        if (!States.TryGetValue(serial, out var state))
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
        ClearStates();
        base.UnregisterEvents();
    }

    protected override Pickup CreatePickupForSpawn(Vector3 position)
    {
        var item = Item.Create(_readyCard.GetBaseItem());
        if (item == null) return null;

        _readyCard.CallCustomizeItem(item);
        return item.CreatePickup(position);
    }

    protected override void OnAcquired(ItemAddedEventArgs ev, bool displayMessage)
    {
        BindRuntimeState(ev.Item.Serial, _pendingRuntimeState);
        base.OnAcquired(ev, displayMessage);
    }

    protected override void OnSpawned(Pickup pickup)
    {
        BindRuntimeState(pickup.Serial, _pendingRuntimeState);
        base.OnSpawned(pickup);
    }

    protected override void OnWaitingForPlayers()
    {
        ClearStates();
        base.OnWaitingForPlayers();
    }

    protected override void OnSerialUntracked(ushort serial)
    {
        RemoveState(serial);
        base.OnSerialUntracked(serial);
    }

    private void Fire(DroppingItemEventArgs ev)
    {
        if (!ev.IsThrown) return;

        ev.IsAllowed = false;

        var oldSerial = ev.Item.Serial;
        var state = GetOrCreateState(oldSerial);
        if (state.IsCharging) return;

        if (state.RemainingShots == 0)
        {
            ev.Player.ShowHint("<size=23>第五思壊線の発射回数を使い切っています。</size>", 3f);
            return;
        }

        try
        {
            var schem = ObjectSpawner.SpawnSchematic(
                "SCP3005",
                ev.Player.Position,
                ev.Player.CameraTransform.forward);
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
            RemoveFrom(ev.Player);
            ev.Player.ShowHint("<size=23>第五思壊線の発射回数を使い切りました。</size>", 3f);
            return;
        }

        state.IsCharging = true;
        state.ChargeStartedAt = Time.time;

        if (!TryChangeInventoryMode(oldSerial, ev.Player, state))
        {
            state.IsCharging = false;
            Log.Error($"[Mindblaster] Failed to enter charging mode for serial={oldSerial}.");
            ev.Player.ShowHint("<size=23>第五思壊線のチャージ開始に失敗しました。</size>", 3f);
            return;
        }

        state.ChargeHandle = Timing.RunCoroutine(ChargeCoroutine(state));
        ev.Player.ShowHint(
            $"<size=23>第五思壊線をチャージ中です（{state.ChargeSeconds:0.#}秒）。</size>",
            3f);
    }

    private void BlockChargingCardDrop(DroppingItemEventArgs ev)
    {
        if (!ev.IsThrown) return;

        ev.IsAllowed = false;

        if (!States.TryGetValue(ev.Item.Serial, out var state))
            return;

        var remaining = Mathf.Max(0f, state.ChargeSeconds - (Time.time - state.ChargeStartedAt));
        ev.Player.ShowHint($"<size=23>チャージ中です（残り {remaining:0.#}秒）。</size>", 2f);
    }

    private IEnumerator<float> ChargeCoroutine(RuntimeState expectedState)
    {
        while (expectedState.IsCharging)
        {
            if (Round.IsLobby || Round.IsEnded)
            {
                RemoveState(expectedState.Serial, killCoroutine: false);
                yield break;
            }

            if (Time.time - expectedState.ChargeStartedAt < expectedState.ChargeSeconds)
            {
                yield return Timing.WaitForSeconds(0.1f);
                continue;
            }

            if (TryCompleteCharge(expectedState))
                yield break;

            // 死亡時などに床へ落ちた直後でも、次の tick で Pickup として処理できる。
            yield return Timing.WaitForSeconds(0.1f);
        }
    }

    private bool TryCompleteCharge(RuntimeState state)
    {
        var serial = state.Serial;

        foreach (var player in Player.List)
        {
            if (player?.Items.All(item => item?.Serial != serial) != false)
                continue;

            if (!TryChangeInventoryMode(serial, player, state))
                return false;

            state.IsCharging = false;
            state.ChargeHandle = default;
            player.ShowHint(
                $"<size=23>第五思壊線のチャージが完了しました。\n{BuildShotsText(state)}</size>",
                3f);
            return true;
        }

        var chargingPickup = Pickup.Get(serial);
        if (chargingPickup == null)
            return false;

        var position = chargingPickup.Position;
        var rotation = chargingPickup.Rotation;

        States.Remove(serial);
        SerialTracker.ForceUnregister(serial);
        base.OnSerialUntracked(serial);
        chargingPickup.Destroy();

        _pendingRuntimeState = state;
        Pickup readyPickup;
        try
        {
            readyPickup = Spawn(position);
        }
        finally
        {
            _pendingRuntimeState = null;
        }

        if (readyPickup == null)
        {
            Log.Error($"[Mindblaster] Failed to create recharged pickup for serial={serial}.");
            state.IsCharging = false;
            state.ChargeHandle = default;
            return true;
        }

        readyPickup.Rotation = rotation;
        state.IsCharging = false;
        state.ChargeHandle = default;
        return true;
    }

    private bool TryChangeInventoryMode(ushort oldSerial, Player player, RuntimeState state)
    {
        if (player == null) return false;

        var previouslyHeld = player.CurrentItem;
        var targetWasHeld = previouslyHeld?.Serial == oldSerial;

        _pendingRuntimeState = state;
        try
        {
            SwitchMode(oldSerial, player);
        }
        finally
        {
            _pendingRuntimeState = null;
        }

        var replacement = player.Items.FirstOrDefault(item =>
            item != null &&
            item.Serial != oldSerial &&
            States.TryGetValue(item.Serial, out var mapped) &&
            ReferenceEquals(mapped, state));

        if (replacement == null)
            return false;

        States.Remove(oldSerial);
        state.Serial = replacement.Serial;

        if (!targetWasHeld &&
            previouslyHeld != null &&
            player.Items.Any(item => item?.Serial == previouslyHeld.Serial))
        {
            player.CurrentItem = previouslyHeld;
        }

        return true;
    }

    private static void BindRuntimeState(ushort serial, RuntimeState pendingState)
    {
        var state = pendingState ?? GetOrCreateState(serial);
        state.Serial = serial;
        States[serial] = state;
    }

    private static RuntimeState GetOrCreateState(ushort serial)
    {
        if (States.TryGetValue(serial, out var state))
            return state;

        state = new RuntimeState
        {
            Serial = serial,
            RemainingShots = NormalizeShots(DefaultShots),
            ChargeSeconds = Mathf.Max(0f, DefaultChargeSeconds),
        };
        States[serial] = state;
        return state;
    }

    private static int NormalizeShots(int shots)
        => shots < 0 ? InfiniteShots : shots;

    private static string BuildShotsText(RuntimeState state)
        => state.RemainingShots < 0
            ? "残り発射回数: ∞"
            : $"残り発射回数: {state.RemainingShots}";

    private static void RemoveState(ushort serial, bool killCoroutine = true)
    {
        if (!States.TryGetValue(serial, out var state)) return;

        States.Remove(serial);
        if (killCoroutine && state.ChargeHandle.IsValid)
            Timing.KillCoroutines(state.ChargeHandle);
    }

    private static void ClearStates()
    {
        foreach (var state in States.Values.Distinct())
        {
            if (state.ChargeHandle.IsValid)
                Timing.KillCoroutines(state.ChargeHandle);
        }

        States.Clear();
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

    [CItemAutoRegisterIgnore]
    private sealed class ReadyCard : CItemKeycard
    {
        private readonly Mindblaster _owner;

        public ReadyCard() { }
        public ReadyCard(Mindblaster owner) => _owner = owner;

        public override string DisplayName => "第五思壊線";
        public override string Description => string.Empty;
        protected override string UniqueKey => "Mindblaster.Ready";
        protected override ItemType BaseItem => ItemType.KeycardCustomTaskForce;
        protected override string KeycardLabel => "Mindblaster";
        protected override Color32? KeycardLabelColor => new(255, 0, 250, 255);
        protected override string KeycardName => "Mgc. Fifth";
        protected override Color32? TintColor => new(255, 0, 250, 255);
        protected override Color32? KeycardPermissionsColor => new(255, 255, 255, 255);
        protected override KeycardPermissions Permissions => KeycardPermissions.None;
        protected override byte Rank => 1;
        protected override string SerialNumber => "555555555555";
        protected override bool PickupLightEnabled => true;
        protected override Color PickupLightColor => Color.magenta;

        protected override void OnDropping(DroppingItemEventArgs ev)
            => _owner?.Fire(ev);
    }

    [CItemAutoRegisterIgnore]
    private sealed class ChargingCard : CItemKeycard
    {
        private readonly Mindblaster _owner;

        public ChargingCard() { }
        public ChargingCard(Mindblaster owner) => _owner = owner;

        public override string DisplayName => "第五思壊線（チャージ中）";
        public override string Description => "第五思壊線をチャージしている";
        protected override string UniqueKey => "Mindblaster.Charging";
        protected override ItemType BaseItem => ItemType.KeycardCustomTaskForce;
        protected override string KeycardLabel => "CHARGING";
        protected override Color32? KeycardLabelColor => new(210, 210, 210, 255);
        protected override string KeycardName => "Mindblaster";
        protected override Color32? TintColor => new(105, 105, 105, 255);
        protected override Color32? KeycardPermissionsColor => new(145, 145, 145, 255);
        protected override KeycardPermissions Permissions => KeycardPermissions.None;
        protected override byte Rank => 1;
        protected override string SerialNumber => "CHARGING";
        protected override bool PickupLightEnabled => false;

        protected override void OnDropping(DroppingItemEventArgs ev)
            => _owner?.BlockChargingCardDrop(ev);
    }
}
