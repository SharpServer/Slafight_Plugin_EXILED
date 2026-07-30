#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomEffects;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Random = System.Random;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

/// <summary>
/// 服用すると <see cref="Insanity"/>（発狂状態）に陥る幻覚アイテム。
/// 演出・付随エフェクト・死因テキストはすべて <see cref="Insanity"/> 側が持つ。
/// <para>
/// 実際に飲むまでに、色が段階的に強まる警告を <see cref="WarningLines"/> 本ぶん通過する必要がある。
/// 使用キーを押すたびに 1 段進み、<see cref="ConfirmationWindowSeconds"/> 秒以内に次を押さなければ
/// 最初からやり直しになる。最後の 1 段は「肉体的および精神的苦痛を受けることへの同意」確認で、
/// これを押し切って初めて使用動作が始まる。
/// </para>
/// <para>
/// 使用開始要求は <c>UsingItem</c> の時点で弾いているため、クールダウンも使用状態も消費されない
/// （<c>UsableItemsController.ServerReceivedStatus</c> は <c>IsAllowed</c> が false だと
/// <c>CurrentUsable</c> を設定しない）。そのため連打でそのまま段を進められる。
/// </para>
/// </summary>
public class LsdPill : CItemUsable
{
    /// <summary>発狂状態の持続時間（秒）。</summary>
    protected virtual float InsanityDuration => 60f;

    /// <summary>発狂の強さ（1-255）。画面揺れ・バースト率・文字化け率がこの比率でスケールする。</summary>
    protected virtual byte InsanityIntensity => 255;

    /// <summary>次の段へ進むまでの猶予（秒）。過ぎると警告は 1 段目に戻る。</summary>
    protected virtual float ConfirmationWindowSeconds => 5f;

    public override string DisplayName => "L-SD2剤";

    public override string Description =>
        "「これはなあに？」";

    protected override string UniqueKey => "LsdPill";
    protected override ItemType BaseItem => ItemType.Adrenaline;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.gray;

    /// <summary>プレイヤーごとの警告進行状況。ReferenceHub ではなく netId をキーにする。</summary>
    private readonly Dictionary<uint, ConfirmationState> _confirmations = new();

    private readonly Random _rng = new();
    private readonly StringBuilder _builder = new(512);

    /// <summary>
    /// 使用キーを押すたびに 1 段進む。全段を押し切るまでは使用開始を拒否する。
    /// </summary>
    protected override bool CanStartUse(UsingItemEventArgs ev)
    {
        if (ev.Player is null || ev.Item is null)
            return false;

        uint key = ev.Player.NetId;
        float now = Time.timeSinceLevelLoad;

        // 別の個体に持ち替えた / 猶予を過ぎた場合は最初からやり直し。
        if (!_confirmations.TryGetValue(key, out ConfirmationState state)
            || state.Serial != ev.Item.Serial
            || now - state.LastInputTime > ConfirmationWindowSeconds)
        {
            state = new ConfirmationState { Serial = ev.Item.Serial };
            _confirmations[key] = state;
        }

        state.LastInputTime = now;

        if (state.Step >= WarningLines.Length)
        {
            // 同意まで押し切った。以降は通常の使用シーケンスへ。
            _confirmations.Remove(key);
            return true;
        }

        ev.Player.ShowHint(BuildWarningHint(state.Step), ConfirmationWindowSeconds);
        state.Step++;

        return false;
    }

    protected override void OnUsedEffect(UsingItemCompletedEventArgs ev)
    {
        ev.Player?.EnableEffect<Insanity>(InsanityIntensity, InsanityDuration);

        base.OnUsedEffect(ev);
    }

    protected override void OnWaitingForPlayers()
    {
        base.OnWaitingForPlayers();

        _confirmations.Clear();

        // 発狂状態は死亡・ロール変更で自動解除されるが、ラウンドをまたいだ取りこぼしがないよう
        // 待機中に明示的に落としておく（Insanity.Disabled が付随エフェクトと Hint を片付ける）。
        foreach (Player player in Player.List)
            player.DisableEffect<Insanity>();
    }

    // ===== 警告表示 =====

    /// <summary>
    /// 1 段ぶんの警告 Hint を組み立てる。段が進むほど色が強く、文字が大きく、文字化けが混ざる。
    /// </summary>
    private string BuildWarningHint(int step)
    {
        step = Mathf.Clamp(step, 0, WarningLines.Length - 1);

        bool isFinal = step == WarningLines.Length - 1;
        string color = WarningColors[step];
        int fontSize = WarningFontSizes[step];
        float corrupt = WarningCorruptChances[step];

        _builder.Clear();

        // 見出し（最終段だけは警告ではなく同意確認）。
        _builder
            .Append("<size=").Append(fontSize - 6).Append("><b><color=").Append(color).Append('>')
            .Append(isFinal
                ? "【 同 意 確 認 】"
                : $"【警告 {step + 1} / {WarningLines.Length - 1}】")
            .Append("</color></b></size>\n");

        // 本文。
        _builder
            .Append("<size=").Append(fontSize).Append("><b><color=").Append(color).Append('>');

        AppendCorrupted(_builder, WarningLines[step], corrupt);

        _builder.Append("</color></b></size>\n");

        // 進行ゲージ。
        _builder
            .Append("<size=24>")
            .Append(BuildGauge(step + 1, color))
            .Append("</size>\n");

        // 操作案内。
        _builder
            .Append("<size=21><color=#9a9a9a>")
            .Append(isFinal
                ? $"同意する場合は {ConfirmationWindowSeconds:0} 秒以内にもう一度 使用キー を押してください"
                : $"{ConfirmationWindowSeconds:0} 秒以内にもう一度 使用キー を押すと次へ進みます（押さなければ最初に戻ります）")
            .Append("</color></size>");

        return _builder.ToString();
    }

    private static string BuildGauge(int filled, string color)
    {
        int total = WarningLines.Length;
        filled = Mathf.Clamp(filled, 0, total);

        StringBuilder gauge = new(total * 12 + 32);

        gauge.Append("<color=").Append(color).Append('>');

        for (int i = 0; i < filled; i++)
            gauge.Append('■');

        gauge.Append("</color><color=#4a4a4a>");

        for (int i = filled; i < total; i++)
            gauge.Append('□');

        gauge.Append("</color>");

        return gauge.ToString();
    }

    /// <summary>
    /// 一定確率で文字を差し替える。色は呼び出し側で固定したいので、
    /// <see cref="GlitchText"/> の色替えは使わずグリフだけ借りる。
    /// </summary>
    private void AppendCorrupted(StringBuilder sb, string text, float corruptChance)
    {
        if (corruptChance <= 0f)
        {
            sb.Append(text);
            return;
        }

        foreach (char c in text)
        {
            sb.Append(c != '\n' && _rng.NextDouble() < corruptChance
                ? GlitchText.RandomGlyph(_rng)
                : c);
        }
    }

    /// <summary>プレイヤー 1 人ぶんの警告進行状況。</summary>
    private sealed class ConfirmationState
    {
        /// <summary>進行中の個体。持ち替えたらリセットする。</summary>
        public ushort Serial;

        /// <summary>次に表示する警告の番号。</summary>
        public int Step;

        /// <summary>最後に使用キーが押された時刻（<see cref="Time.timeSinceLevelLoad"/>）。</summary>
        public float LastInputTime;
    }

    // ===== 警告文言 =====

    /// <summary>
    /// 段階ごとの警告文。最後の 1 本は同意確認として扱われる。
    /// </summary>
    public static string[] WarningLines { get; set; } =
    [
        "L-SD2剤 — 未承認薬物\nこれは治療薬ではありません",
        "本剤は いかなる臨床試験も 通過していません",
        "服用者の 全例で 重度の幻覚が 報告されています",
        "視覚・聴覚・平衡感覚が 元に戻る保証は ありません",
        "服用後に 鎮痛剤 / アドレナリン / SCP-500 を用いた場合\n症状は 例外なく 悪化します",
        "被験者 47 名のうち\n自傷以外の死因は 記録されていません",
        "服用後に 中断する手段は 存在しません",
        "まだ 引き返せます\n……本当に？",
        "誰も あなたに これを飲めとは 言っていません",
        "あなたはこれから\n肉体的および精神的苦痛を受けます\n\n同意しますか？",
    ];

    /// <summary>段階ごとの色。灰 → 黄 → 橙 → 赤 → 血赤。</summary>
    public static string[] WarningColors { get; set; } =
    [
        "#b9b9b9",
        "#d6d08c",
        "#e8d24a",
        "#f0a83a",
        "#f07b2a",
        "#ef4a2a",
        "#e01f1f",
        "#c00f16",
        "#9d040f",
        "#ff0033",
    ];

    /// <summary>段階ごとの本文フォントサイズ。</summary>
    public static int[] WarningFontSizes { get; set; } =
    [
        26, 27, 28, 30, 32, 34, 37, 40, 44, 52,
    ];

    /// <summary>段階ごとの文字化け率。終盤だけ薄く混ざる。</summary>
    public static float[] WarningCorruptChances { get; set; } =
    [
        0f, 0f, 0f, 0f, 0f, 0.015f, 0.035f, 0.07f, 0.13f, 0.06f,
    ];
}
