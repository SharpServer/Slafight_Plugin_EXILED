using System;
using System.Collections.Generic;
using Exiled.API.Features;
using NetworkManagerUtils.Dummies;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Dummy (NPC) の RemoteAdmin アクションを動的に追加する <see cref="IRootDummyActionProvider"/> 実装です。
/// </summary>
/// <remarks>
/// Hub の GameObject に常駐するので、ロール変更を跨いでアクションが残ります。
/// 全ダミーに付き、<see cref="DummyActions.Register"/> の共通アクションもここから出ます。
/// </remarks>
[DisallowMultipleComponent]
public sealed class DummyActionProvider : MonoBehaviour, IRootDummyActionProvider
{
    /// <summary>
    /// カテゴリ未指定時に使用される既定のカテゴリ名です。本体・他プラグインのモジュール名との衝突を避ける接頭辞を持ちます。
    /// </summary>
    public const string DefaultCategory = "Slafight";

    // RemoteAdminのワイヤープロトコル予約文字。','は区切り、"___"はカテゴリ、"***"はDummy境界を表す。
    private const string GroupPrefix = "___";
    private const string DummyIdPrefix = "***";

    private sealed class ActionEntry
    {
        public string Name = string.Empty;
        public Action<DummyActionContext> Callback = _ => { };
    }

    private sealed class Category
    {
        public string Name = DefaultCategory;
        public readonly List<ActionEntry> Actions = [];
    }

    private static readonly HashSet<DummyActionProvider> Live = [];

    // 挿入順を保持するためListで管理する。
    private readonly List<Category> _categories = [];

    /// <inheritdoc/>
    public bool DummyActionsDirty { get; set; } = true;

    /// <summary>
    /// このコンポーネントが属するDummyの<see cref="Npc"/>を取得します。解決できない場合は<c>null</c>。
    /// </summary>
    public Npc? OwnerNpc
    {
        get
        {
            var hub = GetComponent<ReferenceHub>();
            return hub == null ? null : Npc.Get(hub);
        }
    }

    /// <summary>
    /// 全ダミーのアクション一覧を作り直させます。共通アクションの登録・解除時に使います。
    /// </summary>
    internal static void MarkAllDirty()
    {
        foreach (var provider in Live)
        {
            if (provider != null)
                provider.DummyActionsDirty = true;
        }
    }

    private void Awake() => Live.Add(this);

    private void OnDestroy() => Live.Remove(this);

    /// <inheritdoc/>
    public void PopulateDummyActions(Action<DummyAction> actionAdder, Action<string> categoryAdder)
    {
        var owner = OwnerNpc;
        string? currentModule = null;
        foreach (var global in DummyActions.GlobalActions)
        {
            if (owner == null || !global.AppliesTo(owner))
                continue;

            if (currentModule != global.Module)
            {
                categoryAdder(global.Module);
                currentModule = global.Module;
            }

            var captured = global;
            actionAdder(new DummyAction(global.Name, () => Invoke(captured.Name, captured.Callback)));
        }

        foreach (var category in _categories)
        {
            if (category.Actions.Count == 0)
                continue;

            categoryAdder(category.Name);
            foreach (var entry in category.Actions)
            {
                var captured = entry;
                actionAdder(new DummyAction(entry.Name, () => Invoke(captured.Name, captured.Callback)));
            }
        }

        DummyActionsDirty = false;
    }

    /// <summary>
    /// アクション押下時に実行されます。現在のRemoteAdmin実行コンテキスト(押した管理者・選択Dummy)を解決し、
    /// 対象Dummyと合わせてコールバックへ渡します。
    /// </summary>
    private void Invoke(string name, Action<DummyActionContext> callback)
    {
        var target = OwnerNpc;
        if (target == null)
        {
            Log.Warn($"[DummyActionProvider] アクション '{name}' の対象Npcを解決できなかったため実行をスキップしました。");
            return;
        }

        DummyActionContext context;
        if (DummyActionInvocation.Active)
        {
            var selected = DummyActionInvocation.SelectedDummies;
            if (selected == null || selected.Count == 0)
                selected = [target];

            context = new DummyActionContext(DummyActionInvocation.Sender, target, selected, isRemoteAdminInvocation: true);
        }
        else
        {
            context = new DummyActionContext(null, target, [target], isRemoteAdminInvocation: false);
        }

        try
        {
            callback(context);
        }
        catch (Exception ex)
        {
            Log.Error($"[DummyActionProvider] アクション '{name}' の実行中に例外が発生しました: {ex}");
        }
    }

    /// <summary>
    /// アクションを追加します。同じカテゴリ・名前が既に存在する場合はコールバックを置き換えます。
    /// </summary>
    /// <param name="name">アクション名。</param>
    /// <param name="callback">押下時に実行される処理。押した管理者や選択Dummyを含む<see cref="DummyActionContext"/>を受け取ります。</param>
    /// <param name="category">所属カテゴリ名(RemoteAdminのModule)。</param>
    public DummyActionProvider AddAction(string name, Action<DummyActionContext> callback, string category = DefaultCategory)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        name = Sanitize(name, nameof(name));
        category = Sanitize(category, nameof(category));

        var cat = GetOrAddCategory(category);
        var existing = cat.Actions.Find(a => a.Name == name);
        if (existing != null)
        {
            existing.Callback = callback;
        }
        else
        {
            cat.Actions.Add(new ActionEntry { Name = name, Callback = callback });
        }

        DummyActionsDirty = true;
        return this;
    }

    /// <summary>
    /// アクションを追加します。コールバックは対象Dummyのみを受け取ります。
    /// </summary>
    public DummyActionProvider AddAction(string name, Action<Npc?> callback, string category = DefaultCategory)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        return AddAction(name, ctx => callback(ctx.Target), category);
    }

    /// <summary>指定したカテゴリ・名前のアクションを削除します。</summary>
    /// <returns>削除した場合は<c>true</c>。</returns>
    public bool RemoveAction(string name, string category = DefaultCategory)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        name = Sanitize(name, nameof(name));
        category = Sanitize(category, nameof(category));

        var cat = _categories.Find(c => c.Name == category);
        if (cat == null)
            return false;

        int removed = cat.Actions.RemoveAll(a => a.Name == name);
        if (removed == 0)
            return false;

        if (cat.Actions.Count == 0)
            _categories.Remove(cat);

        DummyActionsDirty = true;
        return true;
    }

    /// <summary>指定したカテゴリ配下の全アクションを削除します。</summary>
    /// <returns>削除した場合は<c>true</c>。</returns>
    public bool RemoveCategory(string category)
    {
        category = Sanitize(category, nameof(category));
        if (_categories.RemoveAll(c => c.Name == category) == 0)
            return false;

        DummyActionsDirty = true;
        return true;
    }

    /// <summary>登録された全てのカスタムアクションを削除します。</summary>
    public void Clear()
    {
        if (_categories.Count == 0)
            return;

        _categories.Clear();
        DummyActionsDirty = true;
    }

    private Category GetOrAddCategory(string name)
    {
        var cat = _categories.Find(c => c.Name == name);
        if (cat != null)
            return cat;

        cat = new Category { Name = name };
        _categories.Add(cat);
        return cat;
    }

    /// <summary>
    /// RemoteAdminのワイヤープロトコルを壊さないよう、表示名から予約文字を取り除きます。
    /// ','は区切り文字、"___"/"***"はそれぞれカテゴリ・Dummy境界の接頭辞として解釈されるため除去します。
    /// </summary>
    internal static string Sanitize(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("名前を空にはできません。", paramName);

        var sanitized = value.Replace(',', ' ').Trim();

        bool trimmed = false;
        while (sanitized.StartsWith(GroupPrefix, StringComparison.Ordinal) ||
               sanitized.StartsWith(DummyIdPrefix, StringComparison.Ordinal))
        {
            sanitized = sanitized.Substring(3).TrimStart();
            trimmed = true;
        }

        if (string.IsNullOrWhiteSpace(sanitized))
            throw new ArgumentException("予約文字を除去すると名前が空になります。", paramName);

        if (trimmed || sanitized != value)
            Log.Warn($"[DummyActionProvider] 予約文字を含む名前 '{value}' を '{sanitized}' に置換しました。");

        return sanitized;
    }
}
