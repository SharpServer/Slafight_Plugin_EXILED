using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.Extensions;

using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// そのラウンド 1 回だけ走る特殊ルールです。<see cref="Scope"/> に載せた処理はラウンド終了で止まります。
/// </summary>
public abstract class GameMode
{
    private static bool hooked;

    /// <summary>
    /// 現在走っているゲームモードです。無ければ null。
    /// </summary>
    public static GameMode Current { get; private set; }

    /// <summary>
    /// 表示名です。
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 説明です。ロビー表示などに使います。
    /// </summary>
    public virtual string Description => string.Empty;

    /// <summary>
    /// 抽選対象になる最小人数です。
    /// </summary>
    public virtual int MinimumPlayers => 0;

    /// <summary>
    /// 抽選の重みです。0 なら抽選に出ません (手動起動のみ)。
    /// </summary>
    public virtual int Weight => 1;

    /// <summary>
    /// このモードが今のサーバー状態で選べるかどうかです。
    /// 季節限定などの条件をここに書きます。
    /// </summary>
    public virtual bool IsAvailable => Player.List.Count(player => player.IsSafePlayer()) >= MinimumPlayers;

    /// <summary>
    /// このモードが動いている間、核を使えるかどうかです。
    /// </summary>
    public virtual bool AllowsWarhead => true;

    // 移植元には AllowsBreachAnnouncement / AllowsGateLockdown もありましたが、
    // ここには持ち込んでいません。<b>コンテンツが無いからではなく、
    // それが止めるはずの挙動 (ラウンド開始時の収容違反アナウンスとゲート封鎖) が
    // このコードベースに存在しない</b>ためです。読み手のいない宣言は、
    // false にしても何も起きない罠になります。
    // 導入演出を作るときに、その実装と一緒に足してください。

    /// <summary>
    /// このモード中に勝利表示を差し替える文言です。null なら陣営のものを使います。
    /// </summary>
    public virtual string VictoryText(CustomTeam winner) => null;

    /// <summary>
    /// 起動済みで、まだ打ち切られていないかどうか。
    /// </summary>
    public bool IsRunning => ReferenceEquals(Current, this);

    /// <summary>
    /// このモードがもう有効でないかどうか。差し替え時とラウンド終了時に true になります。
    /// </summary>
    public bool IsCanceled => !IsRunning;

    /// <summary>
    /// このモードに紐づくコルーチン置き場です。ラウンド終了で止まります。
    /// </summary>
    protected RoundScope Scope => RoundScope.Current;

    /// <summary>
    /// 宣言されているゲームモードをすべて返します。呼ぶたびに新しいインスタンスです。
    /// </summary>
    public static IReadOnlyList<GameMode> All()
    {
        List<GameMode> modes = [];

        foreach (Type type in TypeParser.FindTypes<GameMode>())
        {
            try
            {
                modes.Add((GameMode)Activator.CreateInstance(type));
            }
            catch (Exception exception)
            {
                Log.Error($"[Slafight] ゲームモード {type.FullName} を生成できませんでした: {exception}");
            }
        }

        return modes;
    }

    /// <summary>
    /// 起動可能なモードから重み付き抽選で 1 つ選びます。候補が無ければ null。
    /// </summary>
    public static GameMode Roll(Random random = null)
    {
        List<GameMode> candidates =
        [
            .. All()
                .Where(mode => mode.Weight > 0 && mode.IsAvailable)
        ];

        if (candidates.Count == 0) return null;

        int total = candidates.Sum(mode => mode.Weight);
        int roll = (random ?? new Random()).Next(total);

        foreach (GameMode mode in candidates)
        {
            roll -= mode.Weight;

            if (roll < 0) return mode;
        }

        return candidates[candidates.Count - 1];
    }

    /// <summary>
    /// 現在のモードを打ち切ります。
    /// </summary>
    public static void StopCurrent()
    {
        if (Current is not { } mode) return;

        Current = null;

        try
        {
            mode.OnStopped();
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] {mode.GetType().Name} の OnStopped で例外が発生しました: {exception}");
        }
    }

    /// <summary>
    /// 現在のモードが指定アセンブリ由来なら停止します。
    /// </summary>
    public static void StopAssembly(Assembly assembly)
    {
        if (assembly is not null && Current?.GetType().Assembly == assembly)
            StopCurrent();
    }

    /// <summary>
    /// 現在のモードを停止し、静的イベント購読を解除します。
    /// </summary>
    public static void Shutdown()
    {
        StopCurrent();

        if (!hooked) return;

        ServerHandlers.RestartingRound -= StopCurrent;
        hooked = false;
    }

    /// <summary>
    /// このモードを起動します。走っているモードがあれば先に打ち切ります。
    /// </summary>
    public bool Start()
    {
        StopCurrent();

        Current = this;
        Hook();

        try
        {
            OnStarted();

            return true;
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] {GetType().Name} の起動に失敗しました: {exception}");
            StopCurrent();

            return false;
        }
    }

    /// <summary>
    /// 起動時に呼ばれます。役職配布や演出をここで行います。
    /// </summary>
    protected abstract void OnStarted();

    /// <summary>
    /// 打ち切り時・ラウンド終了時に呼ばれます。
    /// <see cref="Scope"/> に載せたものは自動で止まるので、ここに書く必要はありません。
    /// </summary>
    protected virtual void OnStopped()
    {
    }

    private static void Hook()
    {
        if (hooked) return;

        hooked = true;

        ServerHandlers.RestartingRound += StopCurrent;
    }
}
