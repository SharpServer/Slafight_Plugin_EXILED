using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using MEC;
using NetworkManagerUtils.Dummies;
using PlayerRoles.FirstPersonControl;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// ダミーで実行できるアクション 1 つです。本体のもの (歩く・ジャンプ・射撃・能力など) とカスタムの区別はありません。
/// </summary>
public readonly struct DummyActionEntry(string module, string name, Action invoke)
{
    /// <summary>RA の Module 欄 (カテゴリ) です。</summary>
    public string Module { get; } = module;

    /// <summary>アクション名です。</summary>
    public string Name { get; } = name;

    /// <summary><c>Module/Name</c> 形式の識別子です。空白は RA と同じく <c>_</c> になります。</summary>
    public string Path => DummyActions.ToPath(Module, Name);

    public void Invoke() => invoke();
}

/// <summary>
/// ダミーのアクションが実行された直後の情報です。
/// </summary>
public sealed class DummyActionInvokedEventArgs(Npc dummy, string module, string name, Player sender) : EventArgs
{
    public Npc Dummy { get; } = dummy;

    public string Module { get; } = module;

    public string Name { get; } = name;

    public string Path => DummyActions.ToPath(Module, Name);

    /// <summary>RA から押した管理者です。コードからの実行では null。</summary>
    public Player Sender { get; } = sender;
}

/// <summary>
/// ダミーのアクションを <c>Module/Name</c> で探して実行し、全ダミー共通のカスタムアクションを登録します。
/// </summary>
/// <remarks>
/// パスは大文字小文字を区別せず、<c>*</c> を任意の文字列として使えます (例: <c>*(ANY)/Shoot-&gt;Click</c>)。
/// 名前だけを渡すと、全モジュールから最初に一致したものを実行します。
/// </remarks>
public static class DummyActions
{
    /// <summary>
    /// 姿勢を変える組み込みアクションのモジュール名です。本体には姿勢を変えるアクションが無いので補います。
    /// </summary>
    public const string PoseModule = "Pose";

    private static readonly List<GlobalAction> Globals = [];

    /// <summary>
    /// RA から押されたか、<see cref="TryInvoke"/> で実行された直後に起きます。
    /// </summary>
    public static event Action<DummyActionInvokedEventArgs> Invoked;

    internal static IReadOnlyList<GlobalAction> GlobalActions => Globals;

    /// <summary>
    /// このダミーで今実行できるアクションの一覧です。所持品や役職が変わると変わります。
    /// </summary>
    public static IReadOnlyList<DummyActionEntry> List(Npc dummy)
    {
        if (dummy?.ReferenceHub is not { IsDummy: true } hub)
            return [];

        DummyActionsLifecycle.Attach(hub);

        List<DummyActionEntry> entries = [];
        string module = string.Empty;

        foreach (DummyAction action in DummyActionCollector.ServerGetActions(hub))
        {
            if (action.Action == null)
            {
                module = action.Name;
                continue;
            }

            entries.Add(new DummyActionEntry(module, action.Name, action.Action));
        }

        return entries;
    }

    /// <summary>
    /// パスに一致するアクションを 1 つ実行します。
    /// </summary>
    public static bool TryInvoke(Npc dummy, string path, out string error)
        => TryInvoke(dummy, path, null, out error);

    /// <summary>
    /// パスに一致するアクションを 1 つ実行します。<paramref name="sender"/> は <see cref="Invoked"/> にそのまま渡ります。
    /// </summary>
    public static bool TryInvoke(Npc dummy, string path, Player sender, out string error)
    {
        if (dummy?.ReferenceHub is not { IsDummy: true })
        {
            error = "対象がダミーではありません。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "アクションが空です。";
            return false;
        }

        IReadOnlyList<DummyActionEntry> entries = List(dummy);
        DummyActionEntry? match = Find(entries, path);
        if (match is not { } entry)
        {
            error = $"{dummy.Nickname} にアクション '{path}' がありません。";
            return false;
        }

        try
        {
            entry.Invoke();
        }
        catch (Exception exception)
        {
            error = $"アクション '{entry.Path}' で例外が発生しました: {exception.Message}";
            Log.Error($"[DummyActions] {error}\n{exception}");
            return false;
        }

        RaiseInvoked(dummy, entry.Module, entry.Name, sender);
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// 全ダミーに出るカスタムアクションを登録します。破棄すると消えます。
    /// </summary>
    /// <param name="predicate">そのダミーに出すかどうか。null なら常に出します。</param>
    public static IDisposable Register(
        string module,
        string name,
        Action<DummyActionContext> callback,
        Func<Npc, bool> predicate = null)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        var action = new GlobalAction(
            DummyActionProvider.Sanitize(module, nameof(module)),
            DummyActionProvider.Sanitize(name, nameof(name)),
            callback,
            predicate);

        Globals.RemoveAll(existing => existing.Module == action.Module && existing.Name == action.Name);
        Globals.Add(action);
        DummyActionProvider.MarkAllDirty();

        return new Registration(action);
    }

    /// <summary>
    /// <c>Module/Name</c> 形式の識別子にします。RA と同じく空白は <c>_</c> になります。
    /// </summary>
    public static string ToPath(string module, string name)
        => $"{Normalize(module)}/{Normalize(name)}";

    /// <summary>
    /// 一致するものを返します。完全一致を優先し、無ければワイルドカード・名前だけの指定を試します。
    /// </summary>
    public static DummyActionEntry? Find(IReadOnlyList<DummyActionEntry> entries, string path)
    {
        string wanted = Normalize(path);
        bool hasModule = wanted.Contains('/');

        foreach (DummyActionEntry entry in entries)
        {
            if (string.Equals(hasModule ? entry.Path : Normalize(entry.Name), wanted, StringComparison.OrdinalIgnoreCase))
                return entry;
        }

        if (wanted.IndexOf('*') < 0)
            return null;

        var pattern = new Regex(
            "^" + Regex.Escape(wanted).Replace("\\*", ".*") + "$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        foreach (DummyActionEntry entry in entries)
        {
            if (pattern.IsMatch(hasModule ? entry.Path : Normalize(entry.Name)))
                return entry;
        }

        return null;
    }

    internal static void RaiseInvoked(Npc dummy, string module, string name, Player sender)
    {
        if (Invoked == null)
            return;

        var args = new DummyActionInvokedEventArgs(dummy, module, name, sender);
        foreach (Action<DummyActionInvokedEventArgs> handler in Invoked.GetInvocationList())
        {
            try
            {
                handler(args);
            }
            catch (Exception exception)
            {
                Log.Error($"[DummyActions] Invoked の購読者で例外が発生しました: {exception}");
            }
        }
    }

    /// <summary>
    /// 姿勢の組み込みアクション (しゃがむ・忍び足・歩く・走る) を登録します。役職が持たない姿勢は出ません。
    /// </summary>
    /// <remarks>人間はしゃがめない (CrouchSpeed が 0) ので、しゃがみは SCP など一部の役職にしか出ません。</remarks>
    internal static IReadOnlyList<IDisposable> RegisterBuiltIns()
        =>
        [
            RegisterPose("Crouch", PlayerMovementState.Crouching),
            RegisterPose("Sneak", PlayerMovementState.Sneaking),
            RegisterPose("Walk", PlayerMovementState.Walking),
            RegisterPose("Sprint", PlayerMovementState.Sprinting),
        ];

    /// <summary>
    /// 姿勢を表すアクションのパスです。記録した姿勢をダミーで再現するときに使います。
    /// </summary>
    public static string PosePath(PlayerMovementState state) => ToPath(PoseModule, state switch
    {
        PlayerMovementState.Crouching => "Crouch",
        PlayerMovementState.Sneaking => "Sneak",
        PlayerMovementState.Sprinting => "Sprint",
        _ => "Walk",
    });

    private static IDisposable RegisterPose(string name, PlayerMovementState state)
        => Register(PoseModule, name, context =>
        {
            if (context.Target?.Role is FpcRole fpc)
                fpc.MoveState = state;
        }, npc => npc.Role is FpcRole fpc && fpc.FirstPersonController.FpcModule.ValidateMovementState(state) == state);

    private static string Normalize(string value)
        => (value ?? string.Empty).Trim().Replace(' ', '_');

    internal sealed class GlobalAction(string module, string name, Action<DummyActionContext> callback, Func<Npc, bool> predicate)
    {
        public string Module { get; } = module;

        public string Name { get; } = name;

        public Action<DummyActionContext> Callback { get; } = callback;

        public bool AppliesTo(Npc npc)
        {
            try
            {
                // 演出用の内部 Npc にも出したいので、IsSafePlayer では絞らない。
                return npc?.ReferenceHub != null && (predicate?.Invoke(npc) ?? true);
            }
            catch (Exception exception)
            {
                Log.Debug($"[DummyActions] {Module}/{Name} の表示条件で例外が発生しました: {exception.Message}");
                return false;
            }
        }
    }

    private sealed class Registration(GlobalAction action) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            if (Globals.Remove(action))
                DummyActionProvider.MarkAllDirty();
        }
    }
}

/// <summary>
/// 全ダミーに <see cref="DummyActionProvider"/> を付け、組み込みアクションを登録します。
/// </summary>
public sealed class DummyActionsLifecycle : Slafight_Plugin_EXILED.API.Core.Features.EventHandlerBase
{
    private IReadOnlyList<IDisposable> builtIns = [];

    /// <inheritdoc />
    public override void RegisterEvents()
    {
        ReferenceHub.OnPlayerAdded += OnHubAdded;
        builtIns = DummyActions.RegisterBuiltIns();

        foreach (ReferenceHub hub in ReferenceHub.AllHubs.ToArray())
            Attach(hub);
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        ReferenceHub.OnPlayerAdded -= OnHubAdded;

        foreach (IDisposable registration in builtIns)
            registration.Dispose();

        builtIns = [];
    }

    internal static void Attach(ReferenceHub hub)
    {
        if (hub == null || !hub.IsDummy || hub.GetComponent<DummyActionProvider>() != null)
            return;

        hub.gameObject.AddComponent<DummyActionProvider>();

        // プロバイダー配列はキャッシュ生成時に固定されるので、作り直させる。
        DummyActionCollector.CollectionCache.Remove(hub);
    }

    private static void OnHubAdded(ReferenceHub hub)
    {
        Attach(hub);

        // ダミーの種別が決まるのが 1 フレーム遅れることがあるので、もう一度見る。
        Timing.CallDelayed(Timing.WaitForOneFrame, () => Attach(hub));
    }
}
