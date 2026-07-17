using System.Collections.Generic;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Dummyアクションが押された際にコールバックへ渡される実行コンテキストです。
/// </summary>
/// <remarks>
/// RemoteAdminの<c>action</c>コマンド経由で実行された場合、押した管理者(<see cref="Sender"/>)と
/// その操作で選択されていたDummy一覧(<see cref="SelectedDummies"/>)を含みます。
/// コマンド以外から直接実行された場合は<see cref="IsRemoteAdminInvocation"/>が<c>false</c>になり、
/// <see cref="Sender"/>は<c>null</c>、<see cref="SelectedDummies"/>は<see cref="Target"/>のみを含みます。
/// </remarks>
public sealed class DummyActionContext
{
    /// <param name="sender">アクションを実行した管理者。サーバーコンソール等の場合は<c>null</c>。</param>
    /// <param name="target">このコールバックの対象となっているDummy。</param>
    /// <param name="selectedDummies">同じ操作で選択されていたDummyの一覧。</param>
    /// <param name="isRemoteAdminInvocation">RemoteAdminのactionコマンド経由の実行かどうか。</param>
    public DummyActionContext(Player? sender, Npc target, IReadOnlyList<Npc> selectedDummies, bool isRemoteAdminInvocation)
    {
        Sender = sender;
        Target = target;
        SelectedDummies = selectedDummies;
        IsRemoteAdminInvocation = isRemoteAdminInvocation;
    }

    /// <summary>アクションを実行した管理者。サーバーコンソールやコマンド外実行では<c>null</c>。</summary>
    public Player? Sender { get; }

    /// <summary>このコールバックの対象となっているDummy。</summary>
    public Npc Target { get; }

    /// <summary>
    /// この操作で選択されていたDummyの一覧です(本人を含む)。RemoteAdminで複数選択して押した場合に
    /// 同時対象となった全てのDummyが入ります。実プレイヤーは含まれません。
    /// </summary>
    public IReadOnlyList<Npc> SelectedDummies { get; }

    /// <summary>RemoteAdminのactionコマンド経由で実行された場合は<c>true</c>。</summary>
    public bool IsRemoteAdminInvocation { get; }
}

/// <summary>
/// RemoteAdminの<c>action</c>コマンド実行中に、押下した管理者と選択Dummyを一時的に保持するホルダーです。
/// <see cref="Slafight_Plugin_EXILED.Patches.DummyActionContextPatch"/>がコマンドの前後で設定・破棄します。
/// </summary>
internal static class DummyActionInvocation
{
    /// <summary>現在RemoteAdminのactionコマンド処理中かどうか。</summary>
    public static bool Active { get; private set; }

    /// <summary>現在のコマンドを実行した管理者。</summary>
    public static Player? Sender { get; private set; }

    /// <summary>現在のコマンドで選択されていたDummy一覧。</summary>
    public static IReadOnlyList<Npc> SelectedDummies { get; private set; } = [];

    public static void Set(Player? sender, IReadOnlyList<Npc> selectedDummies)
    {
        Sender = sender;
        SelectedDummies = selectedDummies;
        Active = true;
    }

    public static void Clear()
    {
        Active = false;
        Sender = null;
        SelectedDummies = [];
    }
}
