using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// プレイヤーが任意のタイミングで使う能力です。プレイヤー 1 人につき 1 インスタンス。
/// </summary>
/// <remarks>クールダウンと使用回数は基底が持ちます。入力側から <see cref="TryUse"/> を呼んでください。</remarks>
public abstract class AbilityBase
{
    private static readonly Dictionary<uint, List<AbilityBase>> Granted = new Dictionary<uint, List<AbilityBase>>();

    /// <summary>
    /// プレイヤーごとに、いま選んでいる能力の位置です。付与順がそのまま枠順です。
    /// </summary>
    private static readonly Dictionary<uint, int> ActiveIndexes = new Dictionary<uint, int>();

    private float readyAt;

    private string renamed;

    private int selectedOptionIndex;

    /// <summary>
    /// この能力の持ち主です。
    /// </summary>
    public Player Player { get; private set; }

    /// <summary>
    /// この能力そのものの名前です。
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// 画面に出す名前です。既定は <see cref="Name"/>。<see cref="Rename"/> で差し替えられます。
    /// </summary>
    public string DisplayName => string.IsNullOrEmpty(renamed) ? Name : renamed;

    /// <summary>
    /// 説明です。
    /// </summary>
    public virtual string Description => string.Empty;

    /// <summary>
    /// 使用後の待ち時間 (秒) です。
    /// </summary>
    public virtual float Cooldown => 10f;

    /// <summary>
    /// 使用できる回数です。-1 なら無制限。
    /// </summary>
    public virtual int MaxUses => -1;

    /// <summary>
    /// これまでに使った回数です。
    /// </summary>
    public int UsedCount { get; private set; }

    /// <summary>
    /// 残りクールダウン (秒) です。使えるなら 0。
    /// </summary>
    public float RemainingCooldown => Mathf.Max(0f, readyAt - Time.time);

    /// <summary>
    /// 残り使用回数です。無制限なら -1。
    /// </summary>
    public int RemainingUses => MaxUses < 0 ? -1 : Mathf.Max(0, MaxUses - UsedCount);

    /// <summary>
    /// 切り替えられる選択肢です。空なら選択肢を持たない普通の能力です。
    /// </summary>
    public virtual IReadOnlyList<AbilityOption> Options => [];

    /// <summary>
    /// 2 つ以上の選択肢を持っているか。表示層が切替の案内を出すかどうかに使います。
    /// </summary>
    public bool HasSelectableOptions => Options.Count > 1;

    /// <summary>
    /// いま選ばれている選択肢の位置です。持っている数に収まるよう丸めて返します。
    /// </summary>
    public int SelectedOptionIndex =>
        Options.Count == 0 ? 0 : Mathf.Clamp(selectedOptionIndex, 0, Options.Count - 1);

    /// <summary>
    /// いま選ばれている選択肢です。選択肢を持たなければ null。
    /// </summary>
    public AbilityOption SelectedOption => Options.Count == 0 ? null : Options[SelectedOptionIndex];

    /// <summary>
    /// 今すぐ使える状態かどうか。
    /// </summary>
    public bool IsReady => CanUse(out _);

    /// <summary>
    /// このプレイヤーに紐づくコルーチン置き場です。
    /// </summary>
    protected PlayerScope Scope => PlayerScope.Of(Player);

    /// <summary>
    /// 能力を付与します。既に同じ能力を持っていればそれを返します。
    /// </summary>
    public static T Give<T>(Player player) where T : AbilityBase, new() => (T)Give(typeof(T), player);

    /// <summary>
    /// 型引数を使えない経路 (コマンド・マップデータなど) 向けの付与です。
    /// 既に同じ能力を持っていればそれを返します。
    /// </summary>
    public static AbilityBase Give(Type type, Player player)
    {
        if (!player.IsSafePlayer()) return null;

        if (type is null || !typeof(AbilityBase).IsAssignableFrom(type) || type.IsAbstract) return null;

        if (Of(player).FirstOrDefault(candidate => candidate.GetType() == type) is { } existing) return existing;

        uint netId = player.GetNetId();

        if (netId == 0) return null;

        AbilityBase ability = (AbilityBase)Activator.CreateInstance(type);
        ability.Player = player;

        if (!Granted.TryGetValue(netId, out List<AbilityBase> abilities))
        {
            abilities = [];
            Granted[netId] = abilities;

            // 寿命は PlayerScope に任せる。独自のイベントフックは増やさない。
            PlayerScope.Of(player).OnDispose(_ =>
            {
                Granted.Remove(netId);
                ActiveIndexes.Remove(netId);
            });
        }

        abilities.Add(ability);

        ability.Invoke(ability.OnGranted, nameof(OnGranted));

        return ability;
    }

    /// <summary>
    /// このプレイヤーが持っている能力の一覧です。
    /// </summary>
    public static IReadOnlyList<AbilityBase> Of(Player player)
    {
        if (player is not null && Granted.TryGetValue(player.GetNetId(), out List<AbilityBase> abilities))
            return abilities;

        return [];
    }

    /// <summary>
    /// このプレイヤーが持っている T の能力です。無ければ null。
    /// </summary>
    public static T Get<T>(Player player) where T : AbilityBase => Of(player).OfType<T>().FirstOrDefault();

    /// <summary>
    /// このプレイヤーがいま選んでいる能力です。1 つも持っていなければ null。
    /// </summary>
    public static AbilityBase Active(Player player)
    {
        IReadOnlyList<AbilityBase> abilities = Of(player);

        if (abilities.Count == 0) return null;

        return abilities[ActiveIndexOf(player)];
    }

    /// <summary>
    /// いま選んでいる位置です。持っている数に収まるよう丸めて返します。
    /// </summary>
    public static int ActiveIndexOf(Player player)
    {
        IReadOnlyList<AbilityBase> abilities = Of(player);

        if (abilities.Count == 0) return 0;

        if (player is null || !ActiveIndexes.TryGetValue(player.GetNetId(), out int index))
            return 0;

        // 取り上げられて数が減っている場合があるので、読むたびに丸める。
        return Mathf.Clamp(index, 0, abilities.Count - 1);
    }

    /// <summary>
    /// 選んでいる能力を次に送ります。末尾まで行ったら先頭に戻ります。
    /// </summary>
    /// <returns>切り替えたら true。持っている能力が 1 つ以下なら false。</returns>
    public static bool SelectNext(Player player)
    {
        IReadOnlyList<AbilityBase> abilities = Of(player);

        if (abilities.Count <= 1) return false;

        return Select(player, (ActiveIndexOf(player) + 1) % abilities.Count);
    }

    /// <summary>
    /// 位置を指定して選び直します。
    /// </summary>
    public static bool Select(Player player, int index)
    {
        if (player is null) return false;

        IReadOnlyList<AbilityBase> abilities = Of(player);

        if (abilities.Count == 0 || index < 0 || index >= abilities.Count) return false;

        uint netId = player.GetNetId();

        if (netId == 0) return false;

        ActiveIndexes[netId] = index;

        return true;
    }

    /// <summary>
    /// 能力を 1 つ取り上げます。
    /// </summary>
    public static void Revoke<T>(Player player) where T : AbilityBase
    {
        if (Get<T>(player) is not { } ability) return;
        if (!Granted.TryGetValue(player.GetNetId(), out List<AbilityBase> abilities)) return;

        abilities.Remove(ability);
        ability.Invoke(ability.OnRevoked, nameof(OnRevoked));
    }

    /// <summary>
    /// 能力をすべて取り上げます。
    /// </summary>
    public static void RevokeAll(Player player)
    {
        if (player is null) return;

        uint netId = player.GetNetId();

        if (!Granted.TryGetValue(netId, out List<AbilityBase> abilities)) return;

        Granted.Remove(netId);

        foreach (AbilityBase ability in abilities)
        {
            ability.Invoke(ability.OnRevoked, nameof(OnRevoked));
        }
    }

    /// <summary>
    /// この 1 個の呼び名を差し替えます。null / 空を渡すと <see cref="Name"/> に戻ります。
    /// </summary>
    public void Rename(string name) => renamed = name;

    /// <summary>
    /// 選択肢を 1 つ送ります。端まで行ったら反対側へ回ります。
    /// </summary>
    /// <returns>切り替えたら true。選択肢が 1 つ以下なら false。</returns>
    public bool TrySwitchOption(AbilityOptionDirection direction)
    {
        int count = Options.Count;

        if (count <= 1) return false;

        selectedOptionIndex = ((SelectedOptionIndex + (int)direction) % count + count) % count;

        return true;
    }

    /// <summary>
    /// 位置を指定して選択肢を選び直します。
    /// </summary>
    public bool SelectOption(int index)
    {
        if (index < 0 || index >= Options.Count) return false;

        selectedOptionIndex = index;

        return true;
    }

    /// <summary>
    /// 能力を使います。使えなかった場合は理由を返します。
    /// </summary>
    public bool TryUse(out string failureReason)
    {
        if (!CanUse(out failureReason)) return false;

        UsedCount++;
        readyAt = Time.time + Cooldown;

        Invoke(OnUsed, nameof(OnUsed));

        failureReason = null;

        return true;
    }

    /// <summary>
    /// 今使えるかどうかを判定します。
    /// 追加の条件 (立っている場所・所持アイテムなど) は override して足してください。
    /// </summary>
    protected virtual bool CanUse(out string failureReason)
    {
        if (!Player.IsSafePlayer() || !Player.IsAlive || RoleHold.IsHeld(Player))
        {
            failureReason = "この状態では使えません。";

            return false;
        }

        if (RemainingUses == 0)
        {
            failureReason = "使用回数が残っていません。";

            return false;
        }

        if (RemainingCooldown > 0f)
        {
            failureReason = $"あと {RemainingCooldown:F1} 秒待ってください。";

            return false;
        }

        // 選択肢だけの条件は選択肢自身が持つ。
        if (SelectedOption is { } option && !option.CanUse(Player, out failureReason))
            return false;

        failureReason = null;

        return true;
    }

    /// <summary>
    /// 能力の効果です。
    /// </summary>
    protected abstract void OnUsed();

    /// <summary>
    /// 付与された直後に呼ばれます。常時発動型の処理は <see cref="Scope"/> に載せてください。
    /// </summary>
    protected virtual void OnGranted()
    {
    }

    /// <summary>
    /// 取り上げられたときに呼ばれます。<see cref="Scope"/> は生きたままなので、ここで畳んでください。
    /// </summary>
    protected virtual void OnRevoked()
    {
    }

    private void Invoke(Action action, string name)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            Log.Error($"[Slafight] {GetType().Name}.{name} で例外が発生しました: {exception}");
        }
    }
}
