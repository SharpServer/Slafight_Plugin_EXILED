using System;
using System.Linq;
using Exiled.API.Features;
using Interactables.Interobjects.DoorUtils;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features;

public enum DoorAccessDenialReason
{
    Locked,
    MissingPermissions,
    IncorrectCode,
    Custom,
}

public sealed class SendingDoorAccessDeniedHintEventArgs : EventArgs
{
    internal SendingDoorAccessDeniedHintEventArgs(
        Player player, DoorVariant? door, UsefulDoorObject? uDoor, DoorAccessDenialReason reason, string message)
    {
        Player = player;
        Door = door;
        UDoor = uDoor;
        Reason = reason;
        Message = message;
    }

    public Player Player { get; }
    public DoorVariant? Door { get; }
    public UsefulDoorObject? UDoor { get; }
    public DoorAccessDenialReason Reason { get; }
    public string Message { get; set; }
    public float Duration { get; set; } = 3f;
    public bool IsAllowed { get; set; } = true;
}

/// <summary>Configure door access feedback and customize hints before they are sent.</summary>
public static class DoorAccessManager
{
    private static readonly DoorPermissionFlags[] NamedPermissions = Enum.GetValues(typeof(DoorPermissionFlags))
        .Cast<DoorPermissionFlags>()
        .Where(flag => flag != DoorPermissionFlags.None && flag != DoorPermissionFlags.All)
        .ToArray();

    public static event EventHandler<SendingDoorAccessDeniedHintEventArgs>? SendingHint;

    private static DoorAccessDeniedHintConfig HintConfig =>
        global::Slafight_Plugin_EXILED.Plugin.Singleton?.Config?.DoorAccessDeniedHint
        ?? new DoorAccessDeniedHintConfig();

    public static void SetDenialMessage(DoorVariant door, string? message) =>
        GetOrAddSettings(door).DenialMessage = message ?? string.Empty;

    public static void SetDenialMessage(UsefulDoorObject door, string? message) =>
        door.AccessDeniedMessage = message ?? string.Empty;

    public static void SetSecretCode(DoorVariant door, string? code) =>
        GetOrAddSettings(door).SecretCode = code ?? string.Empty;

    public static void SetSecretCode(UsefulDoorObject door, string? code) =>
        door.SecretCode = code ?? string.Empty;

    public static void ShowCustomDenial(Player player, DoorVariant door, string message) =>
        Send(player, door, null, DoorAccessDenialReason.Custom, message);

    public static void ShowCustomDenial(Player player, UsefulDoorObject door, string message) =>
        Send(player, null, door, DoorAccessDenialReason.Custom, message);

    private static DoorAccessFeedback GetOrAddSettings(DoorVariant door) =>
        door.GetComponent<DoorAccessFeedback>() ?? door.gameObject.AddComponent<DoorAccessFeedback>();

    internal static DoorPermissionFlags AvailablePermissions(ReferenceHub hub, IDoorPermissionRequester door)
    {
        if (hub.serverRoles.BypassMode)
            return DoorPermissionFlags.All;

        IDoorPermissionProvider? provider = hub.roleManager.CurrentRole as IDoorPermissionProvider
            ?? hub.inventory.CurInstance as IDoorPermissionProvider;
        return provider?.GetPermissions(door) ?? DoorPermissionFlags.None;
    }

    internal static bool HasCorrectCode(ReferenceHub hub, string? expectedCode) =>
        string.IsNullOrEmpty(expectedCode) || hub.serverRoles.BypassMode ||
        (ServerSpecificUserSettings.TryGetPasscode(Player.Get(hub), out string enteredCode)
         && string.Equals(enteredCode, expectedCode, StringComparison.Ordinal));

    internal static void Show(Player? player, DoorVariant door, DoorAccessDenialReason reason) =>
        Send(player, door, null, reason, door.GetComponent<DoorAccessFeedback>()?.DenialMessage);

    internal static void Show(Player player, UsefulDoorObject door, DoorAccessDenialReason reason) =>
        Send(player, null, door, reason, door.AccessDeniedMessage);

    private static void Send(
        Player? player, DoorVariant? door, UsefulDoorObject? uDoor, DoorAccessDenialReason reason, string? customMessage)
    {
        if (player is null || !player.IsSafePlayer())
            return;

        DoorAccessDeniedHintConfig config = HintConfig;
        string reasonText = reason == DoorAccessDenialReason.IncorrectCode ? config.IncorrectCodeMessage
            : !string.IsNullOrWhiteSpace(customMessage) ? customMessage!
            : reason switch
            {
                DoorAccessDenialReason.Locked => config.LockedMessage,
                DoorAccessDenialReason.MissingPermissions => FormatPermissions(player.ReferenceHub, (IDoorPermissionRequester?)door ?? uDoor!, config),
                _ => config.GenericMessage,
            };

        SendingDoorAccessDeniedHintEventArgs args = new(
            player, door, uDoor, reason, FormatWith(config.MessageFormat, reasonText));
        if (SendingHint is { } sending)
        {
            foreach (EventHandler<SendingDoorAccessDeniedHintEventArgs> handler in sending.GetInvocationList())
            {
                try { handler(null, args); }
                catch (Exception exception) { Log.Error($"DoorAccessManager.SendingHint: {exception.GetType().Name}"); }
            }
        }

        if (args.IsAllowed && !string.IsNullOrWhiteSpace(args.Message) &&
            args.Duration > 0f && !float.IsInfinity(args.Duration) && player.IsSafePlayer())
            player.ShowHint(args.Message, args.Duration);
    }

    private static string FormatPermissions(ReferenceHub hub, IDoorPermissionRequester door, DoorAccessDeniedHintConfig config)
    {
        DoorPermissionFlags required = door.PermissionsPolicy.RequiredPermissions;
        if (required == DoorPermissionFlags.None)
            return config.MissingPermissionsMessage;

        DoorPermissionFlags available = AvailablePermissions(hub, door);
        string permissions = string.Join(", ", NamedPermissions.Where(flag => (required & flag) != 0).Select(flag =>
            ((available & flag) != 0 ? config.GrantedPermissionMark : config.DeniedPermissionMark) + flag));
        return permissions.Length == 0
            ? config.MissingPermissionsMessage
            : FormatWith(config.MissingPermissionsWithPermissionsFormat, permissions);
    }

    private static string FormatWith(string format, string argument)
    {
        try
        {
            return string.Format(format, argument);
        }
        catch (FormatException)
        {
            Log.Warn($"[DoorAccessManager] アクセス拒否メッセージの書式が不正です: {format}");
            return format;
        }
    }
}
