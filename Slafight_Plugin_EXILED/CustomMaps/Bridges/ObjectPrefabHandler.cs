using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.Bridges;

/// <summary>
/// ObjectPrefab へのイベント配送。
/// managed Interactable はルーター経由で O(1) に直接ディスパッチし、
/// Interactable を持たない Prefab のみ半径フォールバックで通知する。
/// </summary>
public class ObjectPrefabHandler : SlafightLabApiHandler, IBootstrapHandler
{
    private static ObjectPrefabHandler _instance;
    public static ObjectPrefabHandler Instance => _instance;
    public static void Register() => _instance = LabApiHandlerRegistry.Register(_instance);
    public static void Unregister() => LabApiHandlerRegistry.Unregister(ref _instance);

    protected override void RegisterEvents(EventSubscriptionScope subscriptions)
    {
        subscriptions.Add(() => PlayerEvents.SearchingToy += OnSearchingToy, () => PlayerEvents.SearchingToy -= OnSearchingToy);
        subscriptions.Add(() => PlayerEvents.SearchedToy += OnSearchedToy, () => PlayerEvents.SearchedToy -= OnSearchedToy);
        subscriptions.Add(() => ServerEvents.RoundStarted += OnRoundStarted, () => ServerEvents.RoundStarted -= OnRoundStarted);
        subscriptions.Add(() => ServerEvents.RoundRestarted += OnRoundRestarting, () => ServerEvents.RoundRestarted -= OnRoundRestarting);
    }

    private static void OnSearchingToy(PlayerSearchingToyEventArgs ev)
    {
        var player = Player.Get(ev.Player);
        if (player == null)
            return;

        if (ObjectPrefabInteractionRouter.TryRoute(ev.Interactable, out var handle))
        {
            handle.RaiseSearching(player, ev);
            handle.Owner.InvokeToySearchingNearby(ev);
            return;
        }

        Vector3 toyPos = ev.Interactable?.Position ?? ev.Player.Position;
        foreach (var prefab in InstanceManager.GetAll())
        {
            if (prefab != null && prefab.MatchesSearchRadius(toyPos))
                prefab.InvokeToySearchingNearby(ev);
        }
    }

    private static void OnSearchedToy(PlayerSearchedToyEventArgs ev)
    {
        var player = Player.Get(ev.Player);
        if (player == null)
            return;

        if (ObjectPrefabInteractionRouter.TryRoute(ev.Interactable, out var handle))
        {
            handle.RaiseSearched(player, ev);
            handle.Owner.InvokeToySearchedNearby(ev);
            return;
        }

        Vector3 toyPos = ev.Interactable?.Position ?? ev.Player.Position;
        foreach (var prefab in InstanceManager.GetAll())
        {
            if (prefab != null && prefab.MatchesSearchRadius(toyPos))
                prefab.InvokeToySearchedNearby(ev);
        }
    }

    private static void OnRoundStarted()
    {
        foreach (var prefab in InstanceManager.GetAll())
        {
            prefab?.InvokeRoundStarted();
        }
    }

    private static void OnRoundRestarting()
    {
        foreach (var prefab in InstanceManager.GetAll())
        {
            prefab?.InvokeRoundRestarting();
        }
    }
}
