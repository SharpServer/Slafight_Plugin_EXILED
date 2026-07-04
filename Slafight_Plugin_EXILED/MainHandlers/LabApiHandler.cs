using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using Player = LabApi.Features.Wrappers.Player;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.CustomMaps.Bridges;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class LabApiHandler : SlafightLabApiHandler, IBootstrapHandler
{
    private static LabApiHandler _instance;
    private static TriggerPointRegistry _triggerPointRegistry;

    public static LabApiHandler Instance => _instance;

    public static void Register()
    {
        _instance = LabApiHandlerRegistry.Register(_instance);
        _triggerPointRegistry = LabApiHandlerRegistry.Register(_triggerPointRegistry);
    }

    public static void Unregister()
    {
        LabApiHandlerRegistry.Unregister(ref _triggerPointRegistry);
        LabApiHandlerRegistry.Unregister(ref _instance);
    }

    protected override void RegisterEvents(EventSubscriptionScope subscriptions)
    {
        subscriptions.Add(() => LabApi.Events.Handlers.PlayerEvents.RaPlayerListAddingPlayer += HideWatchFromRaPlayerList, () => LabApi.Events.Handlers.PlayerEvents.RaPlayerListAddingPlayer -= HideWatchFromRaPlayerList);
        subscriptions.Add(() => LabApi.Events.Handlers.PlayerEvents.RequestedRaPlayerInfo += HideWatchFromRaPlayerInfo, () => LabApi.Events.Handlers.PlayerEvents.RequestedRaPlayerInfo -= HideWatchFromRaPlayerInfo);
    }

    public bool ActivatedAntiMemeProtocol => FacilityControlRoom.IsAntiMemeProtocolActive;
    public bool ActivatedAntiMemeProtocolInPast => FacilityControlRoom.HasAntiMemeProtocolActivatedInPast;

    private static bool IsHideWatchTarget(Player? labPlayer)
    {
        if (labPlayer == null)
            return false;

        var player = Exiled.API.Features.Player.Get(labPlayer.ReferenceHub);
        return player?.GetCustomRole() == CRoleTypeId.HideWatch;
    }

    private static void HideWatchFromRaPlayerList(PlayerRaPlayerListAddingPlayerEventArgs ev)
    {
        if (IsHideWatchTarget(ev.Target))
            ev.InOverwatch = false;
    }

    private static void HideWatchFromRaPlayerInfo(PlayerRequestedRaPlayerInfoEventArgs ev)
    {
        if (!IsHideWatchTarget(ev.Target))
            return;

        ev.InfoBuilder.Replace(" <color=#008080>[OVERWATCH MODE]</color>", string.Empty);
    }
}
