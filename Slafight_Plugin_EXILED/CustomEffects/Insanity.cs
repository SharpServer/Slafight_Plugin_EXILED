#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using CustomPlayerEffects;
using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using MEC;
using Mirror;
using RemoteAdmin.Interfaces;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.CustomMaps.Features.Entities;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using ExiledPlayer = Exiled.API.Features.Player;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;
using Random = System.Random;

namespace Slafight_Plugin_EXILED.CustomEffects;

/// <summary>
/// 発狂状態。付与時に「進行パターン（<see cref="TripPattern"/>）」を 1 つ抽選し、
/// そのパターンが持つフェーズ列を <see cref="StatusEffectBase.Duration"/> をかけて進んでいく幻覚エフェクト。
/// <para>
/// 画面は 3 層構成。<br/>
/// ・ノイズ層 … 行を消さずに溜め込み、行数が増えるほどフォントを縮めて画面全体を埋める。
///   行ごとに <c>&lt;pos&gt;</c> を打てるので、ブロック / 散乱 / 斜め / 横スクロールのレイアウトを切り替えられる。<br/>
/// ・メッセージ層 … 巨大な一文。位置・装飾・文字化けがフェーズごとに変わる。<br/>
/// ・暴走テキスト層 … 独立した Hint を複数持ち、各々が速度ベクトルを持って画面内を斜めに走り、
///   壁で跳ね返るたびに文言と色を変える。
/// </para>
/// <para>
/// 一定確率で 1 tick だけ「バースト」が挿入される（<see cref="BurstKind"/>）。
/// さらに毎 tick、確率で短命の視覚エフェクトと霧種別が差し込まれるので、
/// フェーズが変わらない間もエフェクトの切り替わりが止まらない。
/// </para>
/// <para>
/// 鎮痛剤 / アドレナリン / SCP-500 / メディキットを飲むと <see cref="ICustomHealableEffect.OnHeal"/> 経由で
/// 「一瞬治ったように見せてから、より速く・より悪い進行パターンで再発する」。
/// 服用するほど <see cref="OverdoseLevel"/> が上がり、進行が加速する。
/// </para>
/// <para>
/// <see cref="StatusEffectBase.Intensity"/> は「発狂の強さ」として使う（255 = 全開）。
/// 画面揺れ・バースト率・文字化け率・ノイズ行数・暴走テキスト数がこの比率でスケールする。
/// 段階の進行そのものは残り時間で決まるため、Intensity では変わらない。
/// </para>
/// <para>
/// 演出パラメータ（<see cref="Patterns"/> / 各文言バンク / <see cref="DeathReasons"/> など）は
/// すべて public static。プラグイン側から差し替えてカスタマイズできる。
/// </para>
/// </summary>
public partial class Insanity : CustomEffectBase, ICustomDisplayName, ICustomHealableEffect
{
    // ===== カスタマイズ可能なパラメータ =====

    /// <summary>Duration を指定せずに付与したとき（永続）の 1 サイクル秒数。</summary>
    public static float PermanentCycleSeconds { get; set; } = 60f;

    /// <summary>ノイズ層が最終的に到達する行数（Intensity 255 のとき）。</summary>
    public static int MaxNoiseLines { get; set; } = 56;

    /// <summary>1 tick で追加できる行数の上限。</summary>
    public static int GrowPerTick { get; set; } = 5;

    /// <summary>ノイズ層が使える縦幅の目安（HintServiceMeow 座標）。</summary>
    public static float NoiseAreaHeight { get; set; } = 980f;

    /// <summary>Hint 1 枚あたりのタグ込み最大文字数。暴走時の保険。</summary>
    public static int MaxNoiseChars { get; set; } = 12000;

    /// <summary>暴走テキスト層が確保する Hint の数（= 同時表示の上限）。</summary>
    public static int MaxRoamers { get; set; } = 14;

    /// <summary>暴走テキストが跳ね返る左右の壁（中央からの距離）。</summary>
    public static float RoamHalfWidth { get; set; } = 520f;

    /// <summary>暴走テキストが跳ね返る上の壁。</summary>
    public static float RoamTop { get; set; } = 90f;

    /// <summary>暴走テキストが跳ね返る下の壁。</summary>
    public static float RoamBottom { get; set; } = 1010f;

    /// <summary>発狂中プレイヤーを SCP-513 の付きまとい対象にするか。</summary>
    public static bool StalkWithScp513 { get; set; } = true;

    /// <summary>ベースで掛け続ける負荷エフェクト。</summary>
    public static bool ApplyBaseEffects { get; set; } = true;

    /// <summary>医療アイテム服用で悪化させるか。false にすると服用は完全に無視される（治りもしない）。</summary>
    public static bool AggravateOnMedicalItem { get; set; } = true;

    /// <summary>服用による悪化の上限回数。</summary>
    public static int MaxOverdoseLevel { get; set; } = 5;

    /// <summary>tick 間隔の下限。速度倍率が掛かってもこれより速くはならない。</summary>
    public static float MinInterval { get; set; } = 0.08f;

    /// <summary>
    /// 抽選せずに必ずこのパターンを使う。null なら <see cref="Patterns"/> から重み付き抽選。
    /// デバッグ・イベント用。
    /// </summary>
    public static string? ForcedPatternName { get; set; }

    // ===== 定数 =====

    /// <summary>元テキストを刻む単位。行はこの断片を繋いで作る。</summary>
    private const int FragmentLength = 24;

    private const float NoiseCenterY = 545f;
    private const float MessageBaseY = 520f;

    /// <summary>付随エフェクトを貼り直す間隔（秒）。フリッカーや医療アイテムで剥がれた分をここで戻す。</summary>
    private const float SustainRefreshSeconds = 1.4f;

    /// <summary>文書本文をタグ除去・断片化してキャッシュしたノイズ素材。初回使用時に 1 度だけ構築する。</summary>
    private static string[]? _fragments;

    private CoroutineHandle _tripHandle;
    private TripPattern? _pattern;
    private float _speedScale = 1f;
    private ItemType? _pendingOverdose;

    public bool CanBeDisplayed => true;
    public string DisplayName => "発狂状態";
    public override EffectClassification Classification => EffectClassification.Negative;

    /// <summary>これまでに医療アイテムを飲んで悪化した回数。</summary>
    public int OverdoseLevel { get; private set; }

    /// <summary>現在走っている進行パターン名。未開始なら空文字。</summary>
    public string PatternName => _pattern?.Name ?? string.Empty;

    /// <summary>Intensity を 0..1 に正規化した「発狂の強さ」。</summary>
    private float Severity => Mathf.Clamp01(Intensity / 255f);

    public override void Enabled()
    {
        base.Enabled();

        if (!NetworkServer.active || Player is null)
            return;

        _pattern = PickPattern();
        _speedScale = 1f;
        OverdoseLevel = 0;
        _pendingOverdose = null;

        if (StalkWithScp513)
            Scp513.AddTarget(Player);

        StopTrip();
        _tripHandle = Timing.RunCoroutine(TripCoroutine(Player));
    }

    public override void Disabled()
    {
        Cleanup(Player);
        base.Disabled();
    }

    public override void OnDestroy()
    {
        Cleanup(Player);
        base.OnDestroy();
    }

    // ===== 医療アイテム（悪化トリガ） =====

    /// <summary>
    /// この 4 種を「発狂状態に効く薬」として名乗り出ることで、
    /// <see cref="PlayerEffectsController.UseMedicalItem"/> から <see cref="OnHeal"/> を呼んでもらう。
    /// </summary>
    public bool IsHealable(ItemType item)
    {
        return AggravateOnMedicalItem && OverdoseProfiles.ContainsKey(item);
    }

    /// <summary>
    /// <see cref="ICustomHealableEffect"/> なので本家は解除処理を行わない。
    /// ここでは解除する代わりに悪化を予約する。
    /// <para>
    /// 呼び出し元は <c>AllEffects</c> を foreach 中なので、この場でエフェクトを触ると
    /// 同じループの後続要素に踏まれる。実処理はコルーチン側の次 tick に回す。
    /// </para>
    /// </summary>
    public void OnHeal(ItemType item)
    {
        if (!AggravateOnMedicalItem || !IsEnabled)
            return;

        _pendingOverdose = item;
    }

    // ===== ライフサイクル =====

    private void Cleanup(ExiledPlayer? player)
    {
        StopTrip();

        if (!NetworkServer.active)
            return;

        player ??= Hub == null ? null : ExiledPlayer.Get(Hub);
        if (player is null)
            return;

        if (StalkWithScp513)
            Scp513.RemoveTarget(player);

        RemoveEffects(player);
        RemoveHints(player);
    }

    private void StopTrip()
    {
        if (_tripHandle.IsRunning)
            Timing.KillCoroutines(_tripHandle);

        _tripHandle = default;
    }

    private static TripPattern PickPattern()
    {
        TripPattern[] patterns = Patterns;

        if (patterns.Length == 0)
            return BuildDefaultPatterns()[0];

        if (!string.IsNullOrEmpty(ForcedPatternName))
        {
            foreach (TripPattern candidate in patterns)
            {
                if (string.Equals(candidate.Name, ForcedPatternName, StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }
        }

        float total = 0f;
        foreach (TripPattern candidate in patterns)
            total += Mathf.Max(0f, candidate.Weight);

        if (total <= 0f)
            return patterns[UnityEngine.Random.Range(0, patterns.Length)];

        float roll = UnityEngine.Random.Range(0f, total);

        foreach (TripPattern candidate in patterns)
        {
            roll -= Mathf.Max(0f, candidate.Weight);
            if (roll <= 0f)
                return candidate;
        }

        return patterns[patterns.Length - 1];
    }

    private static bool TryGetPattern(string name, out TripPattern pattern)
    {
        foreach (TripPattern candidate in Patterns)
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                pattern = candidate;
                return true;
            }
        }

        pattern = null!;
        return false;
    }

    // ===== 付随エフェクト =====

    /// <summary>
    /// 発狂中ずっと掛かり続ける負荷エフェクト。フェーズ変更時と定期リフレッシュで貼り直す。
    /// </summary>
    private static void ApplySustainedEffects(ExiledPlayer player, float remaining)
    {
        if (!ApplyBaseEffects)
            return;

        player.EnableEffect<Invigorated>(255, remaining);
        player.EnableEffect<Concussed>(255, remaining);
        player.EnableEffect<Blurred>(255, remaining);
        player.EnableEffect<AmnesiaVision>(255, remaining);
        player.EnableEffect<Asphyxiated>(5, remaining);

        // BecomingFlamingo は IHolidayEffect なので、Christmas/AprilFools が強制有効な
        // サーバーか Development/Nightly ビルドでしか PlayerEffectsController に登録されない。
        // Release + Holiday なしでは無視される（戻り値 false）。
        // 通常ビルドでのフラミンゴ表現は FogType.BecomingFlamingo 側で行う。
        player.EnableEffect<BecomingFlamingo>(1, remaining);
    }

    private static void RemoveEffects(ExiledPlayer? player)
    {
        // 死亡後・ロール変更後でも解除する必要があるため IsValid では絞らない。
        if (player?.ReferenceHub == null)
            return;

        player.DisableEffect<Invigorated>();
        player.DisableEffect<Concussed>();
        player.DisableEffect<Blurred>();
        player.DisableEffect<AmnesiaVision>();
        player.DisableEffect<Asphyxiated>();
        player.DisableEffect<BecomingFlamingo>();
        player.DisableEffect<FogControl>();
        player.DisableEffect<VisualTraumatized>();
        player.DisableEffect<VisualSinkhole>();
        player.DisableEffect<Deafened>();
        player.DisableEffect<Blindness>();
        player.DisableEffect<SoundtrackMute>();
        player.DisableEffect<Scp1576>();
        player.DisableEffect<AmnesiaItems>();
    }

    /// <summary>フェーズ突入時 / 定期リフレッシュで、そのフェーズが要求する状態へ揃え直す。</summary>
    private static void ApplyPhaseEffects(ExiledPlayer player, TripPhase phase, float remaining, bool onEnter)
    {
        if (player.ReferenceHub == null)
            return;

        ApplySustainedEffects(player, remaining);

        player.EnableEffect<FogControl>(phase.FogIntensity, remaining);

        // VisualTraumatized / VisualSinkhole は視覚だけを流用する自前エフェクト。
        // 本家の SCP-106 kill 判定や移動デバフは Patches 側で打ち消されている。
        if (phase.TraumatizedIntensity > 0)
            player.EnableEffect<VisualTraumatized>(phase.TraumatizedIntensity, remaining);
        else if (onEnter)
            player.DisableEffect<VisualTraumatized>();

        if (phase.SinkholeIntensity > 0)
            player.EnableEffect<VisualSinkhole>(phase.SinkholeIntensity, remaining);
        else if (onEnter)
            player.DisableEffect<VisualSinkhole>();

        if (phase.Deafen)
            player.EnableEffect<Deafened>(255, remaining);

        if (!onEnter)
            return;

        if (phase.FlashOnEnter)
            player.EnableEffect<Flashed>(0.7f);

        if (phase.BlindOnEnter)
            player.EnableEffect<Blindness>(255, 1.3f);
    }

    /// <summary>
    /// 毎 tick、確率で短命の視覚 / 聴覚エフェクトと霧種別を差し込む。
    /// フェーズが変わらない間もエフェクトの切り替わりを止めないための仕掛け。
    /// ここで足したものは <see cref="SustainRefreshSeconds"/> ごとのリフレッシュで元へ戻る。
    /// </summary>
    private static void TickEffectFlicker(ExiledPlayer player, TripPhase phase, float severity, Random rng)
    {
        if (player.ReferenceHub == null)
            return;

        if (phase.FogFlickerChance > 0f && rng.NextDouble() < phase.FogFlickerChance * severity)
            // Intensity 1 は FogType.None なので、必ず見た目が変わる 2 以上から引く。
            player.EnableEffect<FogControl>((byte)rng.Next(2, FogTypeCount + 1), 0f);

        if (phase.FlickerChance <= 0f || rng.NextDouble() >= phase.FlickerChance * severity)
            return;

        float Roll(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        switch (rng.Next(9))
        {
            case 0:
                player.EnableEffect<Flashed>(Roll(0.18f, 0.6f));
                break;
            case 1:
                player.EnableEffect<Blindness>(255, Roll(0.22f, 0.7f));
                break;
            case 2:
                player.EnableEffect<Deafened>(255, Roll(0.6f, 2.2f));
                break;
            case 3:
                player.EnableEffect<SoundtrackMute>(1, Roll(2f, 6f));
                break;
            case 4:
                // SCP-1576 の「遠くから声が聞こえる」歪み。
                player.EnableEffect<Scp1576>(1, Roll(1.5f, 4f));
                break;
            case 5:
                // 手に持っている物が見えなくなる。
                player.EnableEffect<AmnesiaItems>(1, Roll(0.8f, 2.4f));
                break;
            case 6:
                player.EnableEffect<VisualSinkhole>(255, Roll(0.4f, 1.4f));
                break;
            case 7:
                player.EnableEffect<VisualTraumatized>(255, Roll(0.5f, 1.8f));
                break;
            default:
                player.EnableEffect<Concussed>(255, Roll(1f, 3f));
                break;
        }
    }

    // ===== 描画 =====

    private IEnumerator<float> TripCoroutine(ExiledPlayer player)
    {
        // Duration は ForceIntensity → Enabled() の後に ServerChangeDuration で入るため、
        // 1 フレーム待ってから読む。
        yield return Timing.WaitForOneFrame;

        if (!IsValid(player) || !IsEnabled)
            yield break;

        var display = player.GetPlayerDisplay();
        if (display is null)
            yield break;

        RemoveHints(player);

        Hint noise = new()
        {
            Id = NoiseHintId(player),
            Alignment = HintAlignment.Center,
            YCoordinateAlign = HintVerticalAlign.Middle,
            XCoordinate = 0f,
            YCoordinate = NoiseCenterY,
            FontSize = 26,
            Text = string.Empty,
            SyncSpeed = HintSyncSpeed.Fastest,
            BlocksDynamicHints = false,
        };

        Hint message = new()
        {
            Id = MessageHintId(player),
            Alignment = HintAlignment.Center,
            YCoordinateAlign = HintVerticalAlign.Middle,
            XCoordinate = 0f,
            YCoordinate = MessageBaseY,
            FontSize = 30,
            Text = string.Empty,
            SyncSpeed = HintSyncSpeed.Fastest,
            BlocksDynamicHints = false,
        };

        display.AddHint(noise);
        display.AddHint(message);

        Random rng = new(unchecked(player.Id * 7919 + Environment.TickCount));
        NoiseCanvas canvas = new(GetFragments(), rng);
        RoamerField roamers = new(display, player, rng);
        MessageContext context = new(player);
        StringBuilder messageBuilder = new(512);

        float elapsed = 0f;
        int phaseIndex = -1;
        float messageTimer = 0f;
        float sustainTimer = 0f;
        float scroll = 0f;
        string currentMessage = string.Empty;
        int currentMessageFontSize = 30;
        string[]? overrideBank = null;
        float overrideTimer = 0f;

        while (IsEnabled)
        {
            if (!IsValid(player))
                break;

            // --- 服用による悪化（偽の治癒 → 再発） ---
            if (_pendingOverdose is ItemType consumed)
            {
                _pendingOverdose = null;
                OverdoseProfile profile = ResolveOverdoseProfile(consumed);

                noise.Hide = true;
                message.Hide = true;
                roamers.HideAll();
                canvas.Clear();
                RemoveEffects(player);

                yield return Timing.WaitForSeconds(profile.CalmSeconds);

                if (!IsValid(player) || !IsEnabled)
                    break;

                ApplyOverdose(profile);

                overrideBank = profile.Messages.Length > 0 ? profile.Messages : null;
                overrideTimer = profile.MessageSeconds;

                elapsed = 0f;
                phaseIndex = -1;
                messageTimer = 0f;
                sustainTimer = 0f;
                continue;
            }

            float severity = Severity;
            TripPattern pattern = _pattern ??= PickPattern();
            float progress = ResolveProgress(pattern, elapsed);

            int nextPhaseIndex = ResolvePhaseIndex(pattern, progress);
            TripPhase phase = pattern.Phases[nextPhaseIndex];
            float interval = Mathf.Max(MinInterval, phase.Interval * pattern.SpeedScale * _speedScale);

            if (nextPhaseIndex != phaseIndex)
            {
                phaseIndex = nextPhaseIndex;
                ApplyPhaseEffects(player, phase, ResolveRemaining(interval), onEnter: true);
                sustainTimer = SustainRefreshSeconds;
                messageTimer = 0f;

                if (phase.ClearNoise)
                    canvas.Clear();
            }
            else
            {
                sustainTimer -= interval;

                if (sustainTimer <= 0f)
                {
                    ApplyPhaseEffects(player, phase, ResolveRemaining(interval), onEnter: false);
                    sustainTimer = SustainRefreshSeconds;
                }
            }

            TickEffectFlicker(player, phase, severity, rng);

            BurstKind? burst = rng.NextDouble() < phase.BurstChance * severity ? PickBurst(phase, rng) : null;

            // --- ノイズ層 ---
            scroll += phase.ScrollSpeed * interval;

            if (phase.ClearNoise && burst is null)
            {
                noise.Hide = true;
            }
            else
            {
                // 経過とともに行数の目標値が加速的に増える（後半ほど一気に埋まる）。
                float fill = Mathf.Clamp01(progress / 0.8f);
                float maxLines = Mathf.Max(6f, MaxNoiseLines * severity * phase.FillRatio);
                int targetLines = Mathf.RoundToInt(Mathf.Lerp(3f, maxLines, fill * fill));

                int fontSize = ResolveNoiseFontSize(Mathf.Max(canvas.LineCount, targetLines));
                NoiseStyle style = new(
                    ResolveCharsPerLine(fontSize, phase.LineWidthScale),
                    phase.ColorRunLength,
                    phase.CorruptChance * severity,
                    phase.DimNoise);

                canvas.GrowTo(targetLines, GrowPerTick, style);
                canvas.Churn(phase.ChurnLines, style);

                NoiseFrame frame = new(phase.Layout, scroll, phase.NoiseSpread, phase.Shake * severity);
                ApplyNoiseBurst(noise, canvas, burst, fontSize, style, frame, phase, severity, rng);
            }

            // --- 暴走テキスト層 ---
            if (overrideTimer > 0f)
                overrideTimer -= interval;

            string[]? activeOverride = overrideTimer > 0f ? overrideBank : null;

            roamers.Tick(phase, severity, interval, burst is BurstKind.Swarm, context, activeOverride);

            // --- メッセージ層 ---
            messageTimer -= interval;

            string[] bank = activeOverride ?? phase.Messages;

            if (burst is not null and not BurstKind.Blackout)
            {
                currentMessage = BuildMessage(phase, BurstMessages, rng, context, burst: true, severity, messageBuilder);
                currentMessageFontSize = ClampMessageFontSize(Mathf.RoundToInt(phase.MessageFontSize * 1.9f));
                messageTimer = phase.MessageInterval;
            }
            else if (messageTimer <= 0f)
            {
                currentMessage = BuildMessage(phase, bank, rng, context, burst: false, severity, messageBuilder);
                currentMessageFontSize = ClampMessageFontSize(phase.MessageFontSize + rng.Next(-8, 17));
                messageTimer = phase.MessageInterval;
            }

            if (burst is BurstKind.Blackout || currentMessage.Length == 0)
            {
                message.Hide = true;
            }
            else
            {
                float wander = phase.MessageWander * severity;

                message.Hide = false;
                message.FontSize = currentMessageFontSize;
                message.Text = currentMessage;
                message.XCoordinate = Jitter(rng, phase.Shake * severity * 1.4f + wander);
                message.YCoordinate = Mathf.Clamp(
                    MessageBaseY + Jitter(rng, phase.Shake * severity * 1.4f + wander * 0.9f),
                    RoamTop,
                    RoamBottom);
            }

            elapsed += interval;
            yield return Timing.WaitForSeconds(interval);
        }

        RemoveHints(player);
        _tripHandle = default;
    }

    /// <summary>
    /// 服用ぶんの悪化を適用する。進行を最初から走らせ直すため、
    /// 「治ったと思ったらもう一度、しかも速く始まる」形になる。
    /// </summary>
    private void ApplyOverdose(OverdoseProfile profile)
    {
        OverdoseLevel = Mathf.Min(OverdoseLevel + 1, Mathf.Max(1, MaxOverdoseLevel));
        _speedScale = Mathf.Max(0.3f, _speedScale * profile.SpeedMultiplier);

        if (!string.IsNullOrEmpty(profile.PatternName) &&
            TryGetPattern(profile.PatternName!, out TripPattern pattern))
        {
            _pattern = pattern;
        }

        ForceIntensity(255);

        if (Duration > 0f)
        {
            // Duration と TimeLeft の両方が新しい値になるので、進捗は 0 に戻る。
            ServerChangeDuration(Mathf.Max(TimeLeft, 0f) + profile.ExtraDuration);
        }
    }

    private static OverdoseProfile ResolveOverdoseProfile(ItemType item)
    {
        return OverdoseProfiles.TryGetValue(item, out OverdoseProfile profile)
            ? profile
            : DefaultOverdoseProfile;
    }

    /// <summary>
    /// 段階進行に使う 0..1 の進捗。時間指定ありなら残り時間から、
    /// 永続付与なら <see cref="PermanentCycleSeconds"/> で周回させる。
    /// <see cref="TripPattern.Cycles"/> が 2 以上ならフェーズ列を複数回まわす。
    /// </summary>
    private float ResolveProgress(TripPattern pattern, float elapsed)
    {
        float duration = Duration;
        int cycles = Mathf.Max(1, pattern.Cycles);

        if (duration > 0f)
        {
            float progress = Mathf.Clamp01(1f - (TimeLeft / duration));
            return cycles > 1 ? Mathf.Repeat(progress * cycles, 1f) : progress;
        }

        float cycleSeconds = Mathf.Max(1f, PermanentCycleSeconds) / cycles;
        return Mathf.Repeat(elapsed, cycleSeconds) / cycleSeconds;
    }

    /// <summary>進捗に対応するフェーズ番号。<see cref="TripPhase.StartRatio"/> の降順で最初に該当したもの。</summary>
    private static int ResolvePhaseIndex(TripPattern pattern, float progress)
    {
        TripPhase[] phases = pattern.Phases;

        for (int i = phases.Length - 1; i >= 0; i--)
        {
            if (progress >= phases[i].StartRatio)
                return i;
        }

        return 0;
    }

    /// <summary>付随エフェクトに渡す残り時間。永続付与なら 0（= 無期限、解除時にまとめて落とす）。</summary>
    private float ResolveRemaining(float minimum)
    {
        if (Duration <= 0f)
            return 0f;

        return Mathf.Max(TimeLeft, minimum);
    }

    /// <summary>
    /// バースト種別に応じてノイズ層の見た目を 1 tick だけ差し替える。
    /// </summary>
    private static void ApplyNoiseBurst(
        Hint noise,
        NoiseCanvas canvas,
        BurstKind? burst,
        int fontSize,
        in NoiseStyle style,
        in NoiseFrame frame,
        TripPhase phase,
        float severity,
        Random rng)
    {
        noise.XCoordinate = frame.Layout == NoiseLayout.Block ? Jitter(rng, frame.Shake) : 0f;
        noise.YCoordinate = NoiseCenterY + Jitter(rng, frame.Shake);

        switch (burst)
        {
            // 一部の行だけを巨大化して抜き出す。「急に文字がでかくなる」用。
            case BurstKind.Zoom:
            {
                int count = Mathf.Clamp(canvas.LineCount / 5, 2, 8);
                int start = rng.Next(0, Mathf.Max(1, canvas.LineCount - count + 1));

                noise.Hide = false;
                noise.XCoordinate = 0f;
                noise.FontSize = Mathf.RoundToInt(fontSize * 2.8f);
                noise.Text = canvas.RenderSlice(start, count, MaxNoiseChars, NoiseFrame.Plain);
                return;
            }

            // 極小フォントで画面いっぱいに一瞬だけ叩き込む。「ばあっと敷き詰まる」用。
            case BurstKind.Flood:
            {
                NoiseStyle floodStyle = new(
                    84,
                    phase.ColorRunLength + 12,
                    (phase.CorruptChance + 0.2f) * severity,
                    false);

                noise.Hide = false;
                noise.XCoordinate = 0f;
                noise.FontSize = 9;
                noise.Text = canvas.RenderFresh(88, floodStyle, MaxNoiseChars, NoiseFrame.Plain);
                return;
            }

            // 表示中の内容をまるごと文字化けさせる（キャッシュは壊さない）。
            case BurstKind.Corrupt:
            {
                NoiseStyle corruptStyle = new(style.CharsPerLine, 10, 0.94f, false);

                noise.Hide = false;
                noise.FontSize = fontSize;
                noise.Text = canvas.RenderFresh(Mathf.Max(canvas.LineCount, 4), corruptStyle, MaxNoiseChars, frame);
                return;
            }

            // 斜めの帯が画面を横切る。行を短くして大きくずらす。
            case BurstKind.Sweep:
            {
                NoiseStyle sweepStyle = new(14, 6, (phase.CorruptChance + 0.15f) * severity, false);
                NoiseFrame sweepFrame = new(NoiseLayout.Diagonal, frame.Scroll * 4f, 620f, 0f, stepPerLine: 96f);

                noise.Hide = false;
                noise.XCoordinate = 0f;
                noise.FontSize = Mathf.Clamp(fontSize + 8, 18, 40);
                noise.Text = canvas.RenderFresh(20, sweepStyle, MaxNoiseChars, sweepFrame);
                return;
            }

            // ブロック文字の壁。一瞬だけ画面が塗り潰される。
            case BurstKind.Wall:
            {
                noise.Hide = false;
                noise.XCoordinate = 0f;
                noise.FontSize = 14;
                noise.Text = canvas.RenderWall(46, 62, 8, MaxNoiseChars);
                return;
            }

            // 行が左右に割れる。画面が真っ二つにズレたように見える。
            case BurstKind.Split:
            {
                NoiseFrame splitFrame = new(NoiseLayout.Column, 0f, 460f, frame.Shake);

                noise.Hide = false;
                noise.XCoordinate = 0f;
                noise.FontSize = fontSize;
                noise.Text = canvas.RenderAll(MaxNoiseChars, splitFrame);
                return;
            }

            // 1 tick だけ完全に消す。次の tick で戻るので「途切れる」感じになる。
            case BurstKind.Blackout:
                noise.Hide = true;
                return;

            // Swarm は暴走テキスト層だけを暴れさせる。ノイズ層は通常描画。
            default:
                noise.Hide = false;
                noise.FontSize = fontSize;
                noise.Text = canvas.RenderAll(MaxNoiseChars, frame);
                return;
        }
    }

    private static BurstKind PickBurst(TripPhase phase, Random rng)
    {
        BurstKind[] pool = phase.BurstPool is { Length: > 0 } ? phase.BurstPool : DefaultBurstPool;
        return pool[rng.Next(pool.Length)];
    }

    private static string BuildMessage(
        TripPhase phase,
        string[] bank,
        Random rng,
        MessageContext context,
        bool burst,
        float severity,
        StringBuilder sb)
    {
        if (bank.Length == 0)
            return string.Empty;

        if (!burst && rng.NextDouble() < phase.MessageBlankChance)
            return string.Empty;

        string body = context.Resolve(bank[rng.Next(bank.Length)], rng);
        if (body.Length == 0)
            return string.Empty;

        MessageStyle style = burst
            ? MessageStyle.Plain
            : PickMessageStyle(phase, rng);

        body = ApplyMessageStyle(body, style, rng);

        float corruptChance = phase.MessageCorruptChance * severity * (burst ? 2.5f : 1f);

        sb.Clear();
        sb.Append("<b>");

        if (style == MessageStyle.Marked)
            sb.Append("<mark=#c1000030>");

        if (style == MessageStyle.Spaced)
            sb.Append("<cspace=1em>");

        if (style == MessageStyle.Tilted)
            sb.Append("<rotate=").Append(rng.Next(2) == 0 ? -12 : 12).Append('>');

        if (phase.MessageColor is null)
        {
            GlitchText.GlitchWriter writer = new(sb, phase.MessageColorRunLength, corruptChance, rng);
            writer.Feed(body);
            writer.End();
        }
        else
        {
            sb.Append("<color=").Append(phase.MessageColor).Append('>');

            foreach (char c in body)
            {
                sb.Append(corruptChance > 0f && rng.NextDouble() < corruptChance
                    ? GlitchText.RandomGlyph(rng)
                    : c);
            }

            sb.Append("</color>");
        }

        if (style == MessageStyle.Tilted)
            sb.Append("</rotate>");

        if (style == MessageStyle.Spaced)
            sb.Append("</cspace>");

        if (style == MessageStyle.Marked)
            sb.Append("</mark>");

        sb.Append("</b>");
        return sb.ToString();
    }

    private static MessageStyle PickMessageStyle(TripPhase phase, Random rng)
    {
        if (rng.NextDouble() >= phase.MessageStyleChance)
            return MessageStyle.Plain;

        // 縦積み・反復は行数を増やすので、巨大フォントのフェーズでは画面外へ飛ぶ。
        int max = phase.MessageFontSize >= 90 ? 4 : 6;

        return (MessageStyle)rng.Next(1, max);
    }

    /// <summary>本文そのものを崩す装飾。タグではなく文字列側を触るぶん。</summary>
    private static string ApplyMessageStyle(string body, MessageStyle style, Random rng)
    {
        switch (style)
        {
            // 1 文字ずつ改行して縦に積む。
            case MessageStyle.Vertical:
            {
                if (body.Length > 14)
                    return body;

                StringBuilder sb = new(body.Length * 2);

                for (int i = 0; i < body.Length; i++)
                {
                    if (i > 0)
                        sb.Append('\n');

                    sb.Append(body[i]);
                }

                return sb.ToString();
            }

            // 同じ文言を何度も重ねる。
            case MessageStyle.Repeated:
            {
                if (body.Length > 12)
                    return body;

                int times = rng.Next(2, 5);
                StringBuilder sb = new(body.Length * times + times);

                for (int i = 0; i < times; i++)
                {
                    if (i > 0)
                        sb.Append(rng.Next(2) == 0 ? '\n' : ' ');

                    sb.Append(body);
                }

                return sb.ToString();
            }

            default:
                return body;
        }
    }

    /// <summary>
    /// メッセージ層のフォントサイズ上限。バースト時の 1.9 倍が乗っても
    /// 一文字が画面を突き抜けない範囲に収める。
    /// </summary>
    public static int MaxMessageFontSize { get; set; } = 210;

    private static int ClampMessageFontSize(int fontSize) =>
        Mathf.Clamp(fontSize, 12, Mathf.Max(12, MaxMessageFontSize));

    /// <summary>行数が増えるほどフォントを縮め、画面の縦幅に収まるようにする。</summary>
    private static int ResolveNoiseFontSize(int lineCount)
    {
        if (lineCount <= 0)
            return 26;

        return Mathf.Clamp(Mathf.RoundToInt(NoiseAreaHeight / (lineCount * 1.25f)), 9, 26);
    }

    /// <summary>フォントが小さいほど 1 行に詰め込む文字数を増やし、横方向も埋める。</summary>
    private static int ResolveCharsPerLine(int fontSize, float widthScale)
    {
        int full = Mathf.Clamp(Mathf.RoundToInt(1500f / fontSize), 24, 72);
        return Mathf.Clamp(Mathf.RoundToInt(full * Mathf.Clamp(widthScale, 0.15f, 1f)), 6, 72);
    }

    private static float Jitter(Random rng, float amplitude)
    {
        if (amplitude <= 0f)
            return 0f;

        return (float)((rng.NextDouble() * 2d - 1d) * amplitude);
    }

    private static string ResolveNickname(ExiledPlayer player)
    {
        string nickname = (player.Nickname ?? string.Empty).RemoveUnityRichTextTag();
        if (nickname.Length > 16)
            nickname = nickname.Substring(0, 16);

        return nickname.OrDefault("■■■■");
    }

    /// <summary>
    /// 文書本文からリッチテキストタグを除いて <see cref="FragmentLength"/> 文字ずつに刻んだノイズ素材を作る。
    /// <see cref="SystemFragments"/> も混ぜて「機械のログ」感を足す。
    /// </summary>
    private static string[] GetFragments()
    {
        if (_fragments is not null)
            return _fragments;

        List<string> fragments = [];

        foreach (DocumentType type in DocumentDictionary.DefinedTypes)
        {
            string plain = DocumentDictionary.Get(type).RemoveUnityRichTextTag();

            foreach (string rawLine in plain.Split('\n'))
            {
                string line = rawLine.Trim('\r', ' ', '\t');
                if (line.Length == 0)
                    continue;

                for (int i = 0; i < line.Length; i += FragmentLength)
                    fragments.Add(line.Substring(i, Math.Min(FragmentLength, line.Length - i)));
            }
        }

        // 文書だけだと語彙が固まるので、システムログ風の断片を厚めに混ぜる。
        int repeat = Mathf.Max(1, fragments.Count / Mathf.Max(1, SystemFragments.Length * 6));

        for (int i = 0; i < repeat; i++)
            fragments.AddRange(SystemFragments);

        // 素材が 1 つも取れないと行生成が破綻するため、最低限のフォールバックを入れておく。
        if (fragments.Count == 0)
            fragments.Add("■■■■■■■■");

        _fragments = fragments.ToArray();
        return _fragments;
    }

    // ===== Hint =====

    private static void RemoveHints(ExiledPlayer? player)
    {
        if (player?.ReferenceHub == null)
            return;

        var display = player.GetPlayerDisplay();
        if (display is null)
            return;

        RemoveHint(display, NoiseHintId(player));
        RemoveHint(display, MessageHintId(player));

        // MaxRoamers を実行中に減らされても取りこぼさないよう、余分に舐める。
        int count = Mathf.Max(MaxRoamers, 32);

        for (int i = 0; i < count; i++)
            RemoveHint(display, RoamerHintId(player, i));
    }

    private static void RemoveHint(HintServiceMeow.Core.Utilities.PlayerDisplay display, string id)
    {
        if (display.GetHint(id) is Hint hint)
            display.RemoveHint(hint);
    }

    private static string NoiseHintId(ExiledPlayer player) => $"{player.NetId}_Insanity_Noise";

    private static string MessageHintId(ExiledPlayer player) => $"{player.NetId}_Insanity_Message";

    private static string RoamerHintId(ExiledPlayer player, int index) => $"{player.NetId}_Insanity_Roam{index}";

    private static bool IsValid(ExiledPlayer? player)
    {
        return player is not null
               && player.ReferenceHub != null
               && !player.IsDead
               && !Round.IsLobby
               && !Round.IsEnded;
    }
}
