using System;
using Exiled.API.Features;
using NetworkManagerUtils.Dummies;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Extensions;

/// <summary>
/// <see cref="Npc"/>のRemoteAdmin用Dummyアクションを拡張するためのヘルパーです。
/// </summary>
/// <remarks>
/// 本体のDummyアクション収集は<see cref="DummyActionCollector"/>がHubのコンポーネント配列を一度だけキャッシュします。
/// そのためコンポーネントを後から追加した際は<see cref="DummyActionCollector.CollectionCache"/>を破棄して
/// プロバイダー配列を再構築させる必要があります。アクション内容の変更時は
/// <see cref="DummyActionProvider.DummyActionsDirty"/>のフラグだけでRemoteAdminへ反映されます。
/// </remarks>
public static class DummyActionExtensions
{
    /// <summary>
    /// このDummyの<see cref="DummyActionProvider"/>を取得します。
    /// </summary>
    /// <param name="npc">対象のNPC。</param>
    /// <param name="create">存在しない場合に新規アタッチするかどうか。</param>
    /// <returns>プロバイダー。取得・作成できない場合は<c>null</c>。</returns>
    public static DummyActionProvider? GetDummyActionProvider(this Npc? npc, bool create = true)
    {
        var hub = npc?.ReferenceHub;
        if (npc == null || hub == null)
            return null;

        var go = npc.GameObject;
        if (go == null)
            return null;

        var provider = go.GetComponent<DummyActionProvider>();
        if (provider != null || !create)
            return provider;

        if (!hub.IsDummy)
            Log.Warn($"[DummyAction] {npc.Nickname} (Id={npc.Id}) はDummyではありません。アクションはRemoteAdminに表示されない可能性があります。");

        provider = go.AddComponent<DummyActionProvider>();

        // プロバイダー配列はキャッシュ生成時にGetComponentsで一度だけ固定されるため、
        // 後付けしたコンポーネントを認識させるにはキャッシュを破棄して再構築させる必要がある。
        DummyActionCollector.CollectionCache.Remove(hub);

        return provider;
    }

    /// <summary>
    /// Dummyアクションを追加します。コールバックは押した管理者や選択Dummyを含む
    /// <see cref="DummyActionContext"/>を受け取ります。
    /// </summary>
    public static Npc AddDummyAction(this Npc npc, string name, Action<DummyActionContext> callback, string category = DummyActionProvider.DefaultCategory)
    {
        npc.GetDummyActionProvider()?.AddAction(name, callback, category);
        return npc;
    }

    /// <summary>
    /// Dummyアクションを追加します。コールバックは引数にこのDummyを受け取ります。
    /// </summary>
    public static Npc AddDummyAction(this Npc npc, string name, Action<Npc?> callback, string category = DummyActionProvider.DefaultCategory)
    {
        npc.GetDummyActionProvider()?.AddAction(name, callback, category);
        return npc;
    }

    /// <summary>
    /// Dummyアクションを追加します。
    /// </summary>
    public static Npc AddDummyAction(this Npc npc, string name, Action callback, string category = DummyActionProvider.DefaultCategory)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        npc.GetDummyActionProvider()?.AddAction(name, (DummyActionContext _) => callback(), category);
        return npc;
    }

    /// <summary>指定したDummyアクションを削除します。</summary>
    public static bool RemoveDummyAction(this Npc npc, string name, string category = DummyActionProvider.DefaultCategory)
        => npc.GetDummyActionProvider(create: false)?.RemoveAction(name, category) ?? false;

    /// <summary>指定したカテゴリ配下のDummyアクションを全て削除します。</summary>
    public static bool RemoveDummyCategory(this Npc npc, string category)
        => npc.GetDummyActionProvider(create: false)?.RemoveCategory(category) ?? false;

    /// <summary>このDummyに登録されたカスタムアクションを全て削除します。</summary>
    public static void ClearDummyActions(this Npc npc)
        => npc.GetDummyActionProvider(create: false)?.Clear();
}
