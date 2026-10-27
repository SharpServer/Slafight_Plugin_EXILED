using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using Slafight_Plugin_EXILED.Extensions;

using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// プレイヤーに紐づくコルーチンと後始末のスコープです。退出・ラウンド再開・<see cref="Dispose"/> で止まります。
/// </summary>
/// <remarks>キーは netId です。</remarks>
public sealed class PlayerScope : IDisposable
{
    private static readonly Dictionary<uint, PlayerScope> Scopes = new Dictionary<uint, PlayerScope>();

    private static bool hooked;

    private readonly List<CoroutineHandle> coroutines = [];
    private readonly List<Action<Player>> cleanups = [];

    private PlayerScope(Player player, uint netId)
    {
        Player = player;
        NetId = netId;
    }

    /// <summary>
    /// このスコープの持ち主です。
    /// </summary>
    public Player Player { get; }

    /// <summary>
    /// 持ち主を指す安定したキーです。
    /// </summary>
    public uint NetId { get; }

    /// <summary>
    /// 破棄済みかどうか。
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// プレイヤーのスコープを取得します。無ければ作ります。
    /// </summary>
    public static PlayerScope Of(Player player)
    {
        if (player is null)
            throw new ArgumentNullException(nameof(player));

        uint netId = player.GetNetId();

        if (netId == 0)
        {
            // netId が未割り当てのハブはキーにできない。呼び出し側の順序が誤っている。
            Log.Error($"[Slafight] netId が未割り当てのプレイヤーに PlayerScope を要求しました: {player.Nickname}");

            return new PlayerScope(player, 0);
        }

        if (Scopes.TryGetValue(netId, out PlayerScope existing) && !existing.IsDisposed)
            return existing;

        PlayerScope scope = new PlayerScope(player, netId);
        Scopes[netId] = scope;

        Hook();

        return scope;
    }

    /// <summary>
    /// スコープが既にあるときだけ返します。無ければ null (作りません)。
    /// </summary>
    public static PlayerScope Find(Player player)
    {
        if (player is null) return null;

        if (Scopes.TryGetValue(player.GetNetId(), out PlayerScope scope) && !scope.IsDisposed)
            return scope;

        return null;
    }

    /// <summary>
    /// プレイヤーのスコープを閉じます。無ければ何もしません。
    /// </summary>
    public static void Close(Player player) => Find(player)?.Dispose();

    /// <summary>
    /// すべてのスコープを閉じます。
    /// </summary>
    public static void CloseAll()
    {
        foreach (PlayerScope scope in new List<PlayerScope>(Scopes.Values))
        {
            scope.Dispose();
        }

        Scopes.Clear();
    }

    /// <summary>
    /// 全スコープを閉じ、静的イベント購読を解除します。
    /// </summary>
    public static void Shutdown()
    {
        CloseAll();

        if (!hooked) return;

        PlayerHandlers.Left -= OnLeft;
        ServerHandlers.RestartingRound -= CloseAll;
        hooked = false;
    }

    /// <summary>
    /// 一定間隔で実行し続けます。プレイヤーが居なくなった時点で自動的に止まります。
    /// </summary>
    public CoroutineHandle RunLoop(float interval, Action<Player> tick)
    {
        if (tick is null)
            throw new ArgumentNullException(nameof(tick));

        return Track(Timing.RunCoroutine(Loop(interval, tick)));
    }

    /// <summary>
    /// 指定秒後に一度だけ実行します。
    /// </summary>
    public CoroutineHandle Delay(float delay, Action<Player> action)
    {
        if (action is null)
            throw new ArgumentNullException(nameof(action));

        return Track(Timing.CallDelayed(delay, () =>
        {
            if (IsDisposed || !IsAlive) return;

            Invoke(action);
        }));
    }

    /// <summary>
    /// 自前で起こしたコルーチンをこのスコープに預けます。破棄時に一緒に止まります。
    /// </summary>
    public CoroutineHandle Track(CoroutineHandle handle)
    {
        if (IsDisposed)
        {
            Timing.KillCoroutines(handle);

            return handle;
        }

        coroutines.Add(handle);

        return handle;
    }

    /// <summary>
    /// スコープが閉じるときに実行する後始末を登録します。登録の逆順で実行されます。
    /// </summary>
    public void OnDispose(Action<Player> cleanup)
    {
        if (cleanup is null)
            throw new ArgumentNullException(nameof(cleanup));

        if (IsDisposed)
        {
            Invoke(cleanup);

            return;
        }

        cleanups.Add(cleanup);
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        IsDisposed = true;

        foreach (CoroutineHandle handle in coroutines)
        {
            Timing.KillCoroutines(handle);
        }

        coroutines.Clear();

        // 1 つ落ちても残りを巻き込まないよう、後ろから 1 件ずつ。
        for (int i = cleanups.Count - 1; i >= 0; i--)
        {
            Invoke(cleanups[i]);
        }

        cleanups.Clear();
        Scopes.Remove(NetId);
    }

    /// <summary>
    /// 遅延実行の時点でも、まだ同じプレイヤーが同じ枠に居るか。
    /// </summary>
    private bool IsAlive => Player.IsSafePlayer() && Player.GetNetId() == NetId;

    private IEnumerator<float> Loop(float interval, Action<Player> tick)
    {
        while (!IsDisposed && IsAlive)
        {
            yield return Timing.WaitForSeconds(interval);

            if (IsDisposed || !IsAlive) yield break;

            Invoke(tick);
        }
    }

    private void Invoke(Action<Player> action)
    {
        try
        {
            action(Player);
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] PlayerScope の処理で例外が発生しました: {exception}");
        }
    }

    private static void Hook()
    {
        if (hooked) return;

        hooked = true;

        PlayerHandlers.Left += OnLeft;
        ServerHandlers.RestartingRound += CloseAll;
    }

    private static void OnLeft(LeftEventArgs ev) => Close(ev.Player);
}
