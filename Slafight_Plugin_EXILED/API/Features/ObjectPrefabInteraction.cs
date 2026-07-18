using System;
using System.Collections.Generic;
using AdminToys;
using Exiled.API.Features;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Features.Wrappers;
using UnityEngine;
using ExiledPlayer = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// ObjectPrefab が管理する 1 つの Interactable。
/// Interacting / Interacted はこの Interactable が対象のときだけ発火する
/// （プレイヤーは Exiled 解決済みで渡される）。
/// </summary>
public sealed class InteractableHandle
{
    internal InteractableHandle(
        ObjectPrefab owner,
        InteractableToy toy,
        Vector3 localOffset,
        Vector3 baseScale,
        string key = "",
        bool ownsToy = true,
        bool syncTransform = true)
    {
        Owner = owner;
        Toy = toy;
        LocalOffset = localOffset;
        BaseScale = baseScale;
        Key = key;
        OwnsToy = ownsToy;
        SyncTransform = syncTransform;
    }

    public ObjectPrefab Owner { get; }

    public InteractableToy Toy { get; }

    /// <summary>
    /// スキマティック内の ObjectPrefabSchematicInfo から採用された場合のキー。
    /// AddInteractable で生成したものは空。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Prefab が Toy の寿命を所有しているか。
    /// スキマティック採用分は false（スキマティック側が破棄する）。
    /// </summary>
    public bool OwnsToy { get; }

    /// <summary>
    /// SyncManagedObjects で transform を追従させるか。
    /// スキマティック採用分は false（親子関係で追従済みのため）。
    /// </summary>
    public bool SyncTransform { get; }

    /// <summary>Prefab 原点（Schematic があればその位置）からのローカルオフセット。</summary>
    public Vector3 LocalOffset { get; set; }

    /// <summary>Prefab の Scale と乗算される基本スケール。</summary>
    public Vector3 BaseScale { get; set; }

    /// <summary>false にすると Interacting が自動でキャンセルされる。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>この Interactable の長押し開始時。</summary>
    public event Action<ExiledPlayer, PlayerSearchingToyEventArgs>? Interacting;

    /// <summary>この Interactable の操作完了時。InteractionDuration が 0 の即時操作も含む。</summary>
    public event Action<ExiledPlayer, PlayerSearchedToyEventArgs>? Interacted;

    internal void RaiseInteracting(ExiledPlayer player, PlayerSearchingToyEventArgs ev)
    {
        if (!Enabled)
            ev.IsAllowed = false;

        Delegate[] handlers = Interacting?.GetInvocationList() ?? [];
        foreach (Delegate handler in handlers)
        {
            try
            {
                ((Action<ExiledPlayer, PlayerSearchingToyEventArgs>)handler)(player, ev);
            }
            catch (Exception e)
            {
                ev.IsAllowed = false;
                Log.Error(
                    $"[ObjectPrefab] Interacting handler failed for {Owner.GetType().Name} " +
                    $"(InstanceID='{Owner.ObjectInstanceID}', Tag='{Owner.Tag}', Key='{Key}', " +
                    $"Player='{player?.Nickname ?? "<unknown>"}'): {e}");
            }
        }
    }

    internal void RaiseInteracted(ExiledPlayer player, PlayerSearchedToyEventArgs ev)
    {
        Delegate[] handlers = Interacted?.GetInvocationList() ?? [];
        foreach (Delegate handler in handlers)
        {
            try
            {
                ((Action<ExiledPlayer, PlayerSearchedToyEventArgs>)handler)(player, ev);
            }
            catch (Exception e)
            {
                Log.Error(
                    $"[ObjectPrefab] Interacted handler failed for {Owner.GetType().Name} " +
                    $"(InstanceID='{Owner.ObjectInstanceID}', Tag='{Owner.Tag}', Key='{Key}', " +
                    $"Player='{player?.Nickname ?? "<unknown>"}'): {e}");
            }
        }
    }
}

/// <summary>
/// Toy 実体 → InteractableHandle の O(1) ルーティングテーブル。
/// 位置ベースのファジーマッチを置き換える。
/// </summary>
public static class ObjectPrefabInteractionRouter
{
    private static readonly Dictionary<InvisibleInteractableToy, InteractableHandle> Routes = new();

    internal static void Register(InteractableHandle handle)
    {
        if (handle.Toy?.Base != null)
            Routes[handle.Toy.Base] = handle;
    }

    internal static void Unregister(InteractableHandle handle)
    {
        if (handle.Toy?.Base != null)
            Routes.Remove(handle.Toy.Base);
    }

    /// <summary>
    /// イベントの Interactable から対応するハンドルを引く。
    /// </summary>
    public static bool TryRoute(InteractableToy? toy, out InteractableHandle handle)
    {
        handle = null!;
        return toy?.Base != null && Routes.TryGetValue(toy.Base, out handle!);
    }
}
