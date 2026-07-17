using System;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Warhead;
using LightContainmentZoneDecontamination;
using Slafight_Plugin_EXILED.API.Interface;
using MapHandler = Exiled.Events.Handlers.Map;
using WarheadHandler = Exiled.Events.Handlers.Warhead;

namespace Slafight_Plugin_EXILED.API.Features;

public sealed class RoundHazardController : IBootstrapHandler, IDisposable
{
    private const string DefaultDecontaminationCancelMessage = "除染は取り消されました";

    private static RoundHazardController _instance;
    private static string _lightDecontaminationCancelMessage = DefaultDecontaminationCancelMessage;
    private static DecontaminationSnapshot? _decontaminationSnapshot;

    private readonly EventSubscriptionScope _subscriptions = new();
    private bool _disposed;

    private RoundHazardController()
    {
        _subscriptions.Add(
            () => WarheadHandler.DeadmanSwitchInitiating += OnDeadmanSwitchInitiating,
            () => WarheadHandler.DeadmanSwitchInitiating -= OnDeadmanSwitchInitiating);

        _subscriptions.Add(
            () => MapHandler.Decontaminating += OnDecontaminating,
            () => MapHandler.Decontaminating -= OnDecontaminating);
    }

    public static bool IsDeadmanSwitchBlocked { get; private set; }

    public static bool IsLightDecontaminationBlocked { get; private set; }

    public static bool IsLightDecontaminationControllerDisableRequested { get; private set; }

    public static void Register()
    {
        Unregister();
        _instance = new RoundHazardController();
    }

    public static void Unregister()
    {
        _instance?.Dispose();
        _instance = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _subscriptions.Dispose();
        ResetRoundState();
        GC.SuppressFinalize(this);
    }

    public static void SetAlphaWarheadDisarmLocked(bool locked)
    {
        try
        {
            Warhead.IsLocked = locked;
        }
        catch (Exception ex)
        {
            Log.Debug($"[RoundHazardController] Failed to set alpha warhead lock to {locked}: {ex.Message}");
        }
    }

    public static void SetDeadmanSwitchBlocked(bool blocked)
    {
        IsDeadmanSwitchBlocked = blocked;
    }

    public static void BlockLightDecontamination()
    {
        IsLightDecontaminationBlocked = true;
    }

    public static void DisableLightDecontamination(string cancelMessage = DefaultDecontaminationCancelMessage)
    {
        IsLightDecontaminationBlocked = true;
        IsLightDecontaminationControllerDisableRequested = true;
        _lightDecontaminationCancelMessage = string.IsNullOrWhiteSpace(cancelMessage)
            ? DefaultDecontaminationCancelMessage
            : cancelMessage;

        TryDisableLightDecontaminationController();
    }

    public static void EnableLightDecontamination()
    {
        IsLightDecontaminationBlocked = false;
        IsLightDecontaminationControllerDisableRequested = false;
        _lightDecontaminationCancelMessage = DefaultDecontaminationCancelMessage;
        TryRestoreLightDecontaminationController();
    }

    public static void ResetRoundState()
    {
        IsDeadmanSwitchBlocked = false;
        EnableLightDecontamination();
        SetAlphaWarheadDisarmLocked(false);
    }

    private static void OnDeadmanSwitchInitiating(DeadmanSwitchInitiatingEventArgs ev)
    {
        if (!IsDeadmanSwitchBlocked)
            return;

        ev.IsAllowed = false;
        Log.Debug("[RoundHazardController] Deadman switch initiation blocked.");
    }

    private static void OnDecontaminating(DecontaminatingEventArgs ev)
    {
        if (IsLightDecontaminationControllerDisableRequested)
            TryDisableLightDecontaminationController();

        if (!IsLightDecontaminationBlocked)
            return;

        ev.IsAllowed = false;
        Log.Debug("[RoundHazardController] Light containment decontamination blocked.");
    }

    private static void TryDisableLightDecontaminationController()
    {
        var controller = DecontaminationController.Singleton;
        if (controller == null)
        {
            Log.Debug("[RoundHazardController] DecontaminationController.Singleton is null; event cancellation remains active.");
            return;
        }

        try
        {
            CaptureDecontaminationSnapshot(controller);

            controller.DecontaminationOverride = DecontaminationController.DecontaminationStatus.Disabled;
            controller.TimeOffset = int.MinValue;
            DecontaminationController.DeconBroadcastDeconMessage = _lightDecontaminationCancelMessage;
        }
        catch (Exception ex)
        {
            Log.Error($"[RoundHazardController] Failed to disable light containment decontamination: {ex}");
        }
    }

    private static void TryRestoreLightDecontaminationController()
    {
        if (_decontaminationSnapshot == null)
            return;

        var snapshot = _decontaminationSnapshot.Value;
        _decontaminationSnapshot = null;

        var controller = DecontaminationController.Singleton;
        if (controller == null)
            return;

        try
        {
            controller.TimeOffset = snapshot.TimeOffset;
            controller.DecontaminationOverride = snapshot.Status;
            DecontaminationController.DeconBroadcastDeconMessage = snapshot.BroadcastMessage;
        }
        catch (Exception ex)
        {
            Log.Error($"[RoundHazardController] Failed to restore light containment decontamination: {ex}");
        }
    }

    private static void CaptureDecontaminationSnapshot(DecontaminationController controller)
    {
        if (_decontaminationSnapshot != null)
            return;

        _decontaminationSnapshot = new DecontaminationSnapshot(
            controller.DecontaminationOverride,
            controller.TimeOffset,
            DecontaminationController.DeconBroadcastDeconMessage);
    }

    private readonly struct DecontaminationSnapshot
    {
        public DecontaminationSnapshot(
            DecontaminationController.DecontaminationStatus status,
            float timeOffset,
            string broadcastMessage)
        {
            Status = status;
            TimeOffset = timeOffset;
            BroadcastMessage = broadcastMessage;
        }

        public DecontaminationController.DecontaminationStatus Status { get; }

        public float TimeOffset { get; }

        public string BroadcastMessage { get; }
    }
}
