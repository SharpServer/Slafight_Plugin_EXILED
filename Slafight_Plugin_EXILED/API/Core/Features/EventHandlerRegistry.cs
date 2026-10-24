using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Core.Features.Attributes;

using LabServerEvents = LabApi.Events.Handlers.ServerEvents;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// <see cref="EventHandlerBase"/> の派生クラスを自動で見つけて生成・購読させる仕組みです。
///
/// <see cref="Plugin.OnEnabled"/> が <see cref="Initialize"/> を呼ぶので、
/// 機能側は何も書く必要がありません。<b>ハンドラのクラスを作るだけで購読されます。</b>
///
/// 自動登録の対象になる条件:
/// <list type="bullet">
/// <item><see cref="EventHandlerBase"/> を継承した abstract でないクラス</item>
/// <item>public な引数なしコンストラクタを持つ</item>
/// <item><see cref="NoAutoRegisterAttribute"/> が付いていない</item>
/// </list>
///
/// <see cref="EventHandlerBase.Lifetime"/> が
/// <see cref="HandlerLifetime.Manual"/> なら起動時に 1 度だけ生成され、
/// <see cref="HandlerLifetime.Round"/> ならラウンド開始ごとに生成し直されます。
/// </summary>
public static class EventHandlerRegistry
{
    private static readonly List<Type> ManualTypes = [];
    private static readonly List<Type> RoundTypes = [];
    private static readonly List<EventHandlerBase> AutoHandlers = [];

    private static readonly List<Assembly> PendingAssemblies = [];

    private static bool initialized;
    private static bool scanned;
    private static bool pluginsEnabled;

    /// <summary>
    /// 自動登録によって生成され、現在生きているハンドラの一覧です。
    /// </summary>
    public static IReadOnlyList<EventHandlerBase> AutoRegistered => AutoHandlers;

    /// <summary>
    /// 自動登録を開始します。全プラグインの読み込み完了を待ってから走査します。
    /// </summary>
    /// <remarks>
    /// 走査の起点だけ LabApi の <c>ServerEvents.PluginsEnabled</c> を使います。
    /// EXILED には「全プラグインのロード完了」に相当するイベントがないためです。
    /// EXILED は LabAPI の上に乗っているので併用に問題はありません。
    /// </remarks>
    public static void Initialize()
    {
        if (initialized) return;

        initialized = true;

        LabServerEvents.PluginsEnabled += OnPluginsEnabled;
        ServerHandlers.RoundStarted += OnRoundStarted;
    }

    /// <summary>
    /// 自動登録を停止し、生成済みのハンドラをすべて破棄します。
    /// </summary>
    public static void Shutdown()
    {
        if (!initialized) return;

        initialized = false;

        LabServerEvents.PluginsEnabled -= OnPluginsEnabled;
        ServerHandlers.RoundStarted -= OnRoundStarted;

        EventHandlerBase.DisposeAll();

        AutoHandlers.Clear();
        ManualTypes.Clear();
        RoundTypes.Clear();
        PendingAssemblies.Clear();
        scanned = false;
        pluginsEnabled = false;
    }

    /// <summary>
    /// 読み込み済みアセンブリを走査し、対象クラスを Lifetime ごとに振り分けます。
    /// 通常は自動で呼ばれるため、明示的に呼ぶ必要はありません。
    /// </summary>
    public static void Scan()
    {
        if (scanned) return;

        scanned = true;

        // FindTypes は abstract / 開いたジェネリック / 引数なしコンストラクタ無しを既に除くが、
        // NoAutoRegister は見ないのでここで併せて絞る。
        Classify(TypeParser.FindTypes<EventHandlerBase>().Where(IsAutoRegisterable));

        foreach (Assembly assembly in PendingAssemblies)
        {
            Classify(GetHandlerTypes(assembly));
        }

        PendingAssemblies.Clear();

        WarnAboutSkipped();

        Log.Debug($"[Slafight] 自動登録対象を検出しました: 常駐 {ManualTypes.Count} 件 / ラウンド {RoundTypes.Count} 件");
    }

    /// <summary>
    /// 自動登録の条件を満たさなかった派生クラスを報告します。
    /// </summary>
    /// <remarks>
    /// public な引数なしコンストラクタが無い型は自動登録できません。
    /// 黙って捨てると<b>継承しただけで動かない</b>状態になり、原因が分からなくなります。
    /// 意図的に外すなら <see cref="NoAutoRegisterAttribute"/> を付けてください。
    /// </remarks>
    private static void WarnAboutSkipped()
    {
        foreach (Type type in TypeParser.FindAllTypes<EventHandlerBase>())
        {
            if (type.IsAbstract || type.IsGenericTypeDefinition) continue;
            if (type.IsDefined(typeof(NoAutoRegisterAttribute), false)) continue;
            if (type.GetConstructor(Type.EmptyTypes) is not null) continue;

            Log.Warn(
                $"[Slafight] {type.FullName} は EventHandlerBase を継承していますが、" +
                "public な引数なしコンストラクタが無いため自動登録されません。" +
                "意図的なら [NoAutoRegister] を付けてください。");
        }
    }

    /// <summary>
    /// 指定したアセンブリのハンドラを明示的に登録します。
    /// </summary>
    /// <remarks>
    /// 通常は不要です。<c>PluginsEnabled</c> の時点で EXILED がロードするプラグインも
    /// AppDomain に載っているため <see cref="Scan"/> が拾います。
    ///
    /// ただしそれはローダーの実行順に依存しているので、名指しで登録できる経路も用意しています。
    /// 二重に呼んでも同じ型が二度登録されることはありません。
    /// </remarks>
    public static void RegisterAssembly(Assembly assembly)
    {
        if (assembly is null) return;

        // Scan より先に呼ばれた場合は覚えておき、Scan のときに一緒に処理する。
        if (!scanned)
        {
            if (!PendingAssemblies.Contains(assembly))
            {
                PendingAssemblies.Add(assembly);
            }

            return;
        }

        int before = ManualTypes.Count + RoundTypes.Count;

        Classify(GetHandlerTypes(assembly));

        int added = ManualTypes.Count + RoundTypes.Count - before;

        if (added == 0) return;

        Log.Debug($"[Slafight] {assembly.GetName().Name} から {added} 件を追加で検出しました。");

        // 走査済みで、かつ常駐分を起こす段階を過ぎている場合だけこの場で起こす。
        // ラウンド分は次の RoundStarted に任せる。
        if (!pluginsEnabled) return;

        SpawnManualTypes();
    }

    /// <summary>
    /// 指定したアセンブリに属する自動登録対象と生成済みハンドラを取り除きます。
    /// </summary>
    public static void UnregisterAssembly(Assembly assembly)
    {
        if (assembly is null) return;

        PendingAssemblies.RemoveAll(candidate => candidate == assembly);
        ManualTypes.RemoveAll(type => type.Assembly == assembly);
        RoundTypes.RemoveAll(type => type.Assembly == assembly);

        EventHandlerBase.DisposeAssembly(assembly);
        AutoHandlers.RemoveAll(handler => handler.GetType().Assembly == assembly);
    }

    /// <summary>
    /// Lifetime を読んで振り分けます。既に登録済みの型は無視します。
    /// </summary>
    private static void Classify(IEnumerable<Type> types)
    {
        foreach (Type type in types)
        {
            if (ManualTypes.Contains(type) || RoundTypes.Contains(type)) continue;

            // Lifetime はインスタンスプロパティなので、判定用に一度だけ生成して読む。
            // ここでは Enable しないため、コンストラクタは軽く副作用のないものにしておくこと。
            if (!TryCreate(type, out EventHandlerBase probe)) continue;

            if (probe.Lifetime is HandlerLifetime.Round)
            {
                RoundTypes.Add(type);
            }
            else
            {
                ManualTypes.Add(type);
            }

            probe.Dispose();
        }
    }

    private static void OnPluginsEnabled()
    {
        pluginsEnabled = true;

        Scan();
        SpawnManualTypes();
    }

    /// <summary>
    /// 常駐ハンドラのうち、まだ生きていないものを起こします。
    /// </summary>
    private static void SpawnManualTypes()
    {
        foreach (Type type in ManualTypes)
        {
            if (AutoHandlers.Any(handler => handler.GetType() == type)) continue;

            Spawn(type);
        }
    }

    private static void OnRoundStarted()
    {
        // ラウンド再開時に EventHandlerBase 側で Dispose 済みのものを掃除してから作り直す。
        AutoHandlers.RemoveAll(handler => handler.IsDisposed);

        foreach (Type type in RoundTypes)
        {
            if (AutoHandlers.Any(handler => handler.GetType() == type)) continue;

            Spawn(type);
        }
    }

    private static void Spawn(Type type)
    {
        if (!TryCreate(type, out EventHandlerBase handler)) return;

        try
        {
            handler.Enable();
            AutoHandlers.Add(handler);
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] {type.FullName} の自動登録に失敗しました: {exception}");
            handler.Dispose();
        }
    }

    private static bool TryCreate(Type type, out EventHandlerBase handler)
    {
        try
        {
            handler = (EventHandlerBase)Activator.CreateInstance(type);

            return true;
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] {type.FullName} のインスタンス生成に失敗しました: {exception}");
            handler = null;

            return false;
        }
    }

    /// <summary>
    /// 1 つのアセンブリから自動登録対象の型だけを取り出します。
    /// </summary>
    private static IEnumerable<Type> GetHandlerTypes(Assembly assembly)
    {
        Type[] types;

        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = [.. exception.Types.Where(type => type is not null)];
            Log.Warn($"[Slafight] {assembly.GetName().Name} の一部の型を読み込めませんでした。読めた型だけを対象にします。");
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] {assembly.GetName().Name} の走査に失敗しました: {exception}");

            return [];
        }

        return types.Where(IsAutoRegisterable).OrderBy(type => type.FullName, StringComparer.Ordinal);
    }

    private static bool IsAutoRegisterable(Type type)
    {
        if (!typeof(EventHandlerBase).IsAssignableFrom(type)) return false;
        if (type.IsAbstract || type.IsGenericTypeDefinition) return false;
        if (type.IsDefined(typeof(NoAutoRegisterAttribute), false)) return false;

        return type.GetConstructor(Type.EmptyTypes) is not null;
    }
}
