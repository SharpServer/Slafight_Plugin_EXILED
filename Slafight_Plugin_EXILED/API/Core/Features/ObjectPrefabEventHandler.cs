using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// ObjectPrefab の Interactable とラウンドライフサイクルを LabAPI から配送します。
/// EventHandlerRegistry により自動登録されます。
/// </summary>
public sealed class ObjectPrefabEventHandler : EventHandlerBase
{
    public override void OnPlayerSearchingToy(PlayerSearchingToyEventArgs ev)
    {
        Player player = ev.Player?.ReferenceHub is { } hub ? Player.Get(hub) : null;
        if (player == null)
            return;

        if (ObjectPrefabInteractionRouter.TryRoute(ev.Interactable, out InteractableHandle handle))
        {
            handle.RaiseInteracting(player, ev);
            handle.Owner.InvokeToyInteractingNearby(ev);
            return;
        }

        DispatchNearby(ev.Interactable?.Position ?? ev.Player.Position,
            prefab => prefab.InvokeToyInteractingNearby(ev));
    }

    public override void OnPlayerSearchedToy(PlayerSearchedToyEventArgs ev)
        => DispatchInteracted(ev);

    public override void OnPlayerInteractedToy(PlayerInteractedToyEventArgs ev)
    {
        if (ev.Player?.ReferenceHub == null || ev.Interactable?.Base == null)
            return;

        DispatchInteracted(new PlayerSearchedToyEventArgs(ev.Player.ReferenceHub, ev.Interactable.Base));
    }

    public override void OnServerRoundStarted()
    {
        foreach (ObjectPrefab prefab in ObjectPrefabInstances.GetAllSnapshot())
            prefab?.InvokeRoundStarted();
    }

    public override void OnServerRoundEnded(RoundEndedEventArgs ev)
        => ObjectPrefabInstances.ResetRoundState();

    public override void OnServerRoundRestarted()
        => ObjectPrefabInstances.ResetRoundState();

    protected override void OnDisposed()
        => ObjectPrefabInstances.DestroyAll();

    private static void DispatchInteracted(PlayerSearchedToyEventArgs ev)
    {
        Player player = ev.Player?.ReferenceHub is { } hub ? Player.Get(hub) : null;
        if (player == null)
            return;

        if (ObjectPrefabInteractionRouter.TryRoute(ev.Interactable, out InteractableHandle handle))
        {
            handle.RaiseInteracted(player, ev);
            handle.Owner.InvokeToyInteractedNearby(ev);
            return;
        }

        DispatchNearby(ev.Interactable?.Position ?? ev.Player.Position,
            prefab => prefab.InvokeToyInteractedNearby(ev));
    }

    private static void DispatchNearby(Vector3 position, System.Action<ObjectPrefab> dispatch)
    {
        foreach (ObjectPrefab prefab in ObjectPrefabInstances.GetRadiusCandidates(position))
            dispatch(prefab);
    }
}
