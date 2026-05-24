using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.CustomMaps.Bridges;

/// <summary>
/// This Handler is wrapper for ObjectPrefabs event receiving.
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
    
    /// <summary>
    /// This method for prefab triggering. Get Near and Invoke that's invoked event.
    /// but it's for SearchingToy.
    /// </summary>
    /// <param name="ev"><seealso cref="PlayerSearchingToyEventArgs"/></param>
    private static void OnSearchingToy(PlayerSearchingToyEventArgs ev)
    {
        var exiledPlayer = Player.Get(ev.Player);
        if (exiledPlayer == null)
            return;

        // LabApi 側が渡してくる「実際に調べられたオブジェクト」の位置を優先
        // Interactable が null のときだけ Player の位置を fallback にする
        var toyPos = ev.Interactable != null
            ? ev.Interactable.Position
            : ev.Player.Position;

        foreach (var prefab in InstanceManager.GetAll())
        {
            if (prefab == null)
                continue;

            if (prefab.MatchesInteractableToy(ev.Interactable, toyPos))
            {
                prefab.InvokeToySearchingNearby(ev);
            }
        }
    }

    /// <summary>
    /// This method for prefab triggering. Get Near and Invoke that's invoked event.
    /// </summary>
    /// <param name="ev"><seealso cref="PlayerSearchedToyEventArgs"/></param>
    private static void OnSearchedToy(PlayerSearchedToyEventArgs ev)
    {
        var exiledPlayer = Player.Get(ev.Player);
        if (exiledPlayer == null)
            return;

        // LabApi 側が渡してくる「実際に調べられたオブジェクト」の位置を優先
        // Interactable が null のときだけ Player の位置を fallback にする
        var toyPos = ev.Interactable != null
            ? ev.Interactable.Position
            : ev.Player.Position;

        foreach (var prefab in InstanceManager.GetAll())
        {
            if (prefab == null)
                continue;

            if (prefab.MatchesInteractableToy(ev.Interactable, toyPos))
            {
                prefab.InvokeToySearchedNearby(ev);
            }
        }
    }

    // ==== Pure Triggering Wrappers ==== //
    
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
