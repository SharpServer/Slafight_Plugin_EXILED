#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using CustomPlayerEffects;
using CustomRendering;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomEffects;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.CustomMaps.Features.Entities;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;
using Random = System.Random;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

/// <summary>
/// 服用から <see cref="Duration"/> 秒かけて 5 段階に悪化していく幻覚アイテム。
/// <para>
/// 画面は「蓄積するノイズ層」と「巨大メッセージ層」の 2 枚構成。ノイズ層は行を消さずに
/// 溜め込んでいき、行数が増えるほどフォントを縮めて画面を埋めていく。既に色付けした行は
/// 文字列としてキャッシュし、毎 tick 描き直すのは一部の行（<see cref="TripPhase.ChurnLines"/>）
/// だけなので、埋め尽くしても生成コストが線形に増えない。
/// </para>
/// <para>
/// 一定確率で 1 tick だけ「バースト」が挿入され、急拡大 / 画面全面の氾濫 / 全文字化け /
/// 暗転のいずれかが起こる。単調な繰り返しにならないための仕掛け。
/// </para>
/// </summary>
public class LsdPill : CItemUsable
{
    private const float Duration = 60f;

    /// <summary>ノイズ層が最終的に到達する行数。</summary>
    private const int MaxNoiseLines = 48;

    /// <summary>1 tick で追加できる行数の上限。</summary>
    private const int GrowPerTick = 3;

    /// <summary>ノイズ層が使える縦幅の目安（HintServiceMeow 座標）。</summary>
    private const float NoiseAreaHeight = 860f;

    /// <summary>Hint 1 枚あたりのタグ込み最大文字数。暴走時の保険。</summary>
    private const int MaxNoiseChars = 14000;

    /// <summary>元テキストを刻む単位。行はこの断片を繋いで作る。</summary>
    private const int FragmentLength = 24;

    /// <summary>メッセージ内でプレイヤー名に置換されるトークン。</summary>
    private const string NameToken = "{NAME}";

    private const float NoiseBaseY = 60f;
    private const float MessageBaseY = 540f;

    private static readonly Dictionary<int, CoroutineHandle> TripCoroutines = new();
    private static readonly Dictionary<int, int> Sessions = new();

    /// <summary>文書本文をタグ除去・断片化してキャッシュしたノイズ素材。初回使用時に 1 度だけ構築する。</summary>
    private static string[]? _fragments;

    public override string DisplayName => "L-SD2剤";

    public override string Description =>
        "「これはなあに？」";

    protected override string UniqueKey => "LsdPill";
    protected override ItemType BaseItem => ItemType.Adrenaline;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.gray;

    protected override void OnUsedEffect(UsingItemCompletedEventArgs ev)
    {
        Player player = ev.Player;
        if (player is null)
        {
            base.OnUsedEffect(ev);
            return;
        }

        int session = CreateNewSession(player);

        StopTripCoroutine(player, removeHints: true);

        ApplyEffects(player);
        Scp513.AddTarget(player);

        TripCoroutines[player.Id] = Timing.RunCoroutine(TripCoroutine(player, session));

        Timing.CallDelayed(Duration, () => EndSession(player, session));

        base.OnUsedEffect(ev);
    }

    protected override void OnWaitingForPlayers()
    {
        base.OnWaitingForPlayers();

        foreach (CoroutineHandle handle in TripCoroutines.Values)
            Timing.KillCoroutines(handle);

        TripCoroutines.Clear();
        Sessions.Clear();

        // BecomingFlamingo / FogControl / VisualSinkhole などは死亡・ロール変更で自動解除されないため、
        // ラウンドをまたいで残らないようここで明示的に落とす。
        foreach (Player player in Player.List)
        {
            RemoveEffects(player);
            RemoveHints(player);
        }
    }

    // ===== 効果 =====

    private static void ApplyEffects(Player player)
    {
        player.EnableEffect<Invigorated>(255, Duration);
        player.EnableEffect<Concussed>(255, Duration);
        player.EnableEffect<Blurred>(255, Duration);
        player.EnableEffect<AmnesiaVision>(255, Duration);
        player.EnableEffect<Asphyxiated>(5, Duration);

        // BecomingFlamingo は IHolidayEffect なので、Christmas/AprilFools が強制有効な
        // サーバーか Development/Nightly ビルドでしか PlayerEffectsController に登録されない。
        // Release + Holiday なしでは無視される（戻り値 false）。
        // 通常ビルドでのフラミンゴ表現は FogType.BecomingFlamingo 側で行う。
        player.EnableEffect<BecomingFlamingo>(1, Duration);
    }

    private static void RemoveEffects(Player? player)
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
    }

    private static void EnterPhaseEffects(Player player, TripPhase phase, float remaining)
    {
        if (player.ReferenceHub == null || remaining <= 0f)
            return;

        player.EnableEffect<FogControl>(phase.FogIntensity, remaining);

        // VisualTraumatized / VisualSinkhole は視覚だけを流用する自前エフェクト。
        // 本家の SCP-106 kill 判定や移動デバフは Patches 側で打ち消されている。
        if (phase.TraumatizedIntensity > 0)
            player.EnableEffect<VisualTraumatized>(phase.TraumatizedIntensity, remaining);
        else
            player.DisableEffect<VisualTraumatized>();

        if (phase.SinkholeIntensity > 0)
            player.EnableEffect<VisualSinkhole>(phase.SinkholeIntensity, remaining);
        else
            player.DisableEffect<VisualSinkhole>();

        if (phase.Deafen)
            player.EnableEffect<Deafened>(255, remaining);

        if (phase.FlashOnEnter)
            player.EnableEffect<Flashed>(0.7f);

        if (phase.BlindOnEnter)
            player.EnableEffect<Blindness>(255, 1.3f);
    }

    // ===== 描画 =====

    private static IEnumerator<float> TripCoroutine(Player player, int session)
    {
        if (!IsValid(player))
            yield break;

        var display = player.GetPlayerDisplay();
        if (display is null)
            yield break;

        RemoveHints(player);

        Hint noise = new()
        {
            Id = NoiseHintId(player),
            Alignment = HintAlignment.Center,
            YCoordinateAlign = HintVerticalAlign.Top,
            XCoordinate = 0f,
            YCoordinate = NoiseBaseY,
            FontSize = 26,
            Text = string.Empty,
            SyncSpeed = HintSyncSpeed.Fastest,
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
        };

        display.AddHint(noise);
        display.AddHint(message);

        Random rng = new(unchecked(player.Id * 7919 + Environment.TickCount));
        NoiseCanvas canvas = new(GetFragments(), rng);
        StringBuilder messageBuilder = new(512);
        string nickname = ResolveNickname(player);

        float elapsed = 0f;
        int phaseIndex = -1;
        float messageTimer = 0f;
        string currentMessage = string.Empty;
        int currentMessageFontSize = 30;

        while (elapsed < Duration)
        {
            if (!IsCurrentSession(player, session))
                break;

            if (!IsValid(player))
            {
                EndSession(player, session);
                yield break;
            }

            int nextPhaseIndex = ResolvePhaseIndex(elapsed);
            if (nextPhaseIndex != phaseIndex)
            {
                phaseIndex = nextPhaseIndex;
                EnterPhaseEffects(player, Phases[phaseIndex], Duration - elapsed);
                messageTimer = 0f;

                if (Phases[phaseIndex].ClearNoise)
                    canvas.Clear();
            }

            TripPhase phase = Phases[phaseIndex];
            BurstKind? burst = rng.NextDouble() < phase.BurstChance ? PickBurst(rng) : null;

            // --- ノイズ層 ---
            if (phase.ClearNoise)
            {
                noise.Hide = true;
            }
            else
            {
                // 経過とともに行数の目標値が加速的に増える（後半ほど一気に埋まる）。
                float fill = Mathf.Clamp01(elapsed / (Duration * 0.85f));
                int targetLines = Mathf.RoundToInt(Mathf.Lerp(2f, MaxNoiseLines, fill * fill));

                int fontSize = ResolveNoiseFontSize(Mathf.Max(canvas.LineCount, targetLines));
                NoiseStyle style = new(
                    ResolveCharsPerLine(fontSize),
                    phase.ColorRunLength,
                    phase.CorruptChance,
                    phase.DimNoise);

                canvas.GrowTo(targetLines, GrowPerTick, style);
                canvas.Churn(phase.ChurnLines, style);

                ApplyNoiseBurst(noise, canvas, burst, fontSize, style, phase, rng);

                if (!noise.Hide)
                {
                    noise.XCoordinate = Jitter(rng, phase.Shake);
                    noise.YCoordinate = NoiseBaseY + Jitter(rng, phase.Shake);
                }
            }

            // --- メッセージ層 ---
            messageTimer -= phase.Interval;

            if (burst is not null and not BurstKind.Blackout)
            {
                currentMessage = BuildMessage(phase, rng, nickname, burst: true, messageBuilder);
                currentMessageFontSize = Mathf.RoundToInt(phase.MessageFontSize * 1.8f);
                messageTimer = phase.MessageInterval;
            }
            else if (messageTimer <= 0f)
            {
                currentMessage = BuildMessage(phase, rng, nickname, burst: false, messageBuilder);
                currentMessageFontSize = phase.MessageFontSize + rng.Next(-6, 13);
                messageTimer = phase.MessageInterval;
            }

            if (burst is BurstKind.Blackout || currentMessage.Length == 0)
            {
                message.Hide = true;
            }
            else
            {
                message.Hide = false;
                message.FontSize = currentMessageFontSize;
                message.Text = currentMessage;
                message.XCoordinate = Jitter(rng, phase.Shake * 1.4f);
                message.YCoordinate = MessageBaseY + Jitter(rng, phase.Shake * 1.4f);
            }

            elapsed += phase.Interval;
            yield return Timing.WaitForSeconds(phase.Interval);
        }

        if (IsCurrentSession(player, session))
            RemoveHints(player);

        RemoveTripCoroutine(player);
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
        TripPhase phase,
        Random rng)
    {
        switch (burst)
        {
            // 一部の行だけを巨大化して抜き出す。「急に文字がでかくなる」用。
            case BurstKind.Zoom:
            {
                int count = Mathf.Clamp(canvas.LineCount / 5, 2, 8);
                int start = rng.Next(0, Mathf.Max(1, canvas.LineCount - count + 1));

                noise.Hide = false;
                noise.FontSize = Mathf.RoundToInt(fontSize * 2.6f);
                noise.Text = canvas.RenderSlice(start, count, MaxNoiseChars);
                return;
            }

            // 極小フォントで画面いっぱいに一瞬だけ叩き込む。「ばあっと敷き詰まる」用。
            case BurstKind.Flood:
            {
                NoiseStyle floodStyle = new(78, phase.ColorRunLength + 10, phase.CorruptChance + 0.2f, false);

                noise.Hide = false;
                noise.FontSize = 10;
                noise.Text = canvas.RenderFresh(72, floodStyle, MaxNoiseChars);
                return;
            }

            // 表示中の内容をまるごと文字化けさせる（キャッシュは壊さない）。
            case BurstKind.Corrupt:
            {
                NoiseStyle corruptStyle = new(style.CharsPerLine, 10, 0.92f, false);

                noise.Hide = false;
                noise.FontSize = fontSize;
                noise.Text = canvas.RenderFresh(Mathf.Max(canvas.LineCount, 4), corruptStyle, MaxNoiseChars);
                return;
            }

            // 1 tick だけ完全に消す。次の tick で戻るので「途切れる」感じになる。
            case BurstKind.Blackout:
                noise.Hide = true;
                return;

            default:
                noise.Hide = false;
                noise.FontSize = fontSize;
                noise.Text = canvas.RenderAll(MaxNoiseChars);
                return;
        }
    }

    private static BurstKind PickBurst(Random rng)
    {
        double roll = rng.NextDouble();

        if (roll < 0.32d) return BurstKind.Zoom;
        if (roll < 0.64d) return BurstKind.Flood;
        if (roll < 0.86d) return BurstKind.Corrupt;

        return BurstKind.Blackout;
    }

    private static string BuildMessage(TripPhase phase, Random rng, string nickname, bool burst, StringBuilder sb)
    {
        string[] bank = burst ? BurstMessages : phase.Messages;
        if (bank.Length == 0)
            return string.Empty;

        if (!burst && rng.NextDouble() < phase.MessageBlankChance)
            return string.Empty;

        string body = bank[rng.Next(bank.Length)];
        if (body.Length == 0)
            return string.Empty;

        if (body.IndexOf(NameToken, StringComparison.Ordinal) >= 0)
            body = body.Replace(NameToken, nickname);

        float corruptChance = phase.MessageCorruptChance * (burst ? 2.5f : 1f);

        sb.Clear();
        sb.Append("<b>");

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

        sb.Append("</b>");
        return sb.ToString();
    }

    /// <summary>行数が増えるほどフォントを縮め、画面の縦幅に収まるようにする。</summary>
    private static int ResolveNoiseFontSize(int lineCount)
    {
        if (lineCount <= 0)
            return 26;

        return Mathf.Clamp(Mathf.RoundToInt(NoiseAreaHeight / (lineCount * 1.25f)), 11, 26);
    }

    /// <summary>フォントが小さいほど 1 行に詰め込む文字数を増やし、横方向も埋める。</summary>
    private static int ResolveCharsPerLine(int fontSize)
    {
        return Mathf.Clamp(Mathf.RoundToInt(1400f / fontSize), 24, 64);
    }

    private static float Jitter(Random rng, float amplitude)
    {
        if (amplitude <= 0f)
            return 0f;

        return (float)((rng.NextDouble() * 2d - 1d) * amplitude);
    }

    private static string ResolveNickname(Player player)
    {
        string nickname = (player.Nickname ?? string.Empty).RemoveUnityRichTextTag();
        if (nickname.Length > 16)
            nickname = nickname.Substring(0, 16);

        return nickname.OrDefault("■■■■");
    }

    /// <summary>
    /// 文書本文からリッチテキストタグを除いて <see cref="FragmentLength"/> 文字ずつに刻んだノイズ素材を作る。
    /// 旧実装はここを毎 tick やっていた上に、タグ途中で改行を挿入して表示を壊していた。
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

        // 素材が 1 つも取れないと行生成が破綻するため、最低限のフォールバックを入れておく。
        if (fragments.Count == 0)
            fragments.Add("■■■■■■■■");

        _fragments = fragments.ToArray();
        return _fragments;
    }

    // ===== セッション管理 =====

    private static void EndSession(Player player, int session)
    {
        if (!IsCurrentSession(player, session))
            return;

        StopTripCoroutine(player, removeHints: true);

        Scp513.RemoveTarget(player);
        RemoveEffects(player);

        Sessions.Remove(player.Id);
    }

    private static int CreateNewSession(Player player)
    {
        if (!Sessions.TryGetValue(player.Id, out int session))
            session = 0;

        session++;
        Sessions[player.Id] = session;

        return session;
    }

    private static bool IsCurrentSession(Player player, int session)
    {
        return player is not null
               && Sessions.TryGetValue(player.Id, out int currentSession)
               && currentSession == session;
    }

    private static void StopTripCoroutine(Player player, bool removeHints)
    {
        if (player is null)
            return;

        if (TripCoroutines.TryGetValue(player.Id, out CoroutineHandle handle))
        {
            Timing.KillCoroutines(handle);
            TripCoroutines.Remove(player.Id);
        }

        if (removeHints)
            RemoveHints(player);
    }

    private static void RemoveTripCoroutine(Player player)
    {
        if (player is not null)
            TripCoroutines.Remove(player.Id);
    }

    private static void RemoveHints(Player? player)
    {
        if (player?.ReferenceHub == null)
            return;

        var display = player.GetPlayerDisplay();
        if (display is null)
            return;

        RemoveHint(display, NoiseHintId(player));
        RemoveHint(display, MessageHintId(player));
    }

    private static void RemoveHint(HintServiceMeow.Core.Utilities.PlayerDisplay display, string id)
    {
        if (display.GetHint(id) is Hint hint)
            display.RemoveHint(hint);
    }

    private static string NoiseHintId(Player player) => $"{player.NetId}_LsdPill_Noise";

    private static string MessageHintId(Player player) => $"{player.NetId}_LsdPill_Message";

    private static bool IsValid(Player? player)
    {
        return player is not null
               && player.ReferenceHub != null
               && !player.IsDead
               && !Round.IsLobby
               && !Round.IsEnded;
    }

    // ===== ノイズ層 =====

    private enum BurstKind
    {
        /// <summary>一部の行を巨大化。</summary>
        Zoom,

        /// <summary>極小フォントで画面全面を埋める。</summary>
        Flood,

        /// <summary>全文字化け。</summary>
        Corrupt,

        /// <summary>1 tick だけ消える。</summary>
        Blackout,
    }

    private readonly struct NoiseStyle
    {
        public NoiseStyle(int charsPerLine, int colorRunLength, float corruptChance, bool dim)
        {
            CharsPerLine = charsPerLine;
            ColorRunLength = colorRunLength;
            CorruptChance = corruptChance;
            Dim = dim;
        }

        public int CharsPerLine { get; }
        public int ColorRunLength { get; }
        public float CorruptChance { get; }
        public bool Dim { get; }
    }

    /// <summary>
    /// 色付け済みの行を溜め込んでおくバッファ。
    /// 「増えていく」表現のため行を消さずに積み、上限に達したらランダムな行を差し替える。
    /// 描画は積んだ文字列を連結するだけなので、行数が増えても色付けのやり直しは発生しない。
    /// </summary>
    private sealed class NoiseCanvas
    {
        private readonly List<string> _lines = [];
        private readonly string[] _fragments;
        private readonly Random _rng;
        private readonly StringBuilder _lineBuilder = new(256);
        private readonly StringBuilder _output = new(8192);

        public NoiseCanvas(string[] fragments, Random rng)
        {
            _fragments = fragments;
            _rng = rng;
        }

        public int LineCount => _lines.Count;

        public void Clear() => _lines.Clear();

        /// <summary>目標行数まで行を足す。1 tick あたりの追加数は <paramref name="maxPerTick"/> で頭打ち。</summary>
        public void GrowTo(int target, int maxPerTick, in NoiseStyle style)
        {
            int added = 0;

            while (_lines.Count < target && added < maxPerTick)
            {
                _lines.Add(BuildLine(style));
                added++;
            }
        }

        /// <summary>既存行のうち <paramref name="count"/> 本だけを描き直す（明滅・入れ替わり）。</summary>
        public void Churn(int count, in NoiseStyle style)
        {
            if (_lines.Count == 0)
                return;

            for (int i = 0; i < count; i++)
                _lines[_rng.Next(_lines.Count)] = BuildLine(style);
        }

        public string RenderAll(int maxChars) => RenderSlice(0, _lines.Count, maxChars);

        public string RenderSlice(int start, int count, int maxChars)
        {
            if (_lines.Count == 0)
                return string.Empty;

            start = Mathf.Clamp(start, 0, _lines.Count - 1);
            int end = Mathf.Min(start + count, _lines.Count);

            _output.Clear();

            for (int i = start; i < end; i++)
            {
                if (i > start)
                    _output.Append('\n');

                _output.Append(_lines[i]);

                if (_output.Length >= maxChars)
                    break;
            }

            return _output.ToString();
        }

        /// <summary>キャッシュを使わずその場で作り捨てる。バースト演出専用。</summary>
        public string RenderFresh(int lineCount, in NoiseStyle style, int maxChars)
        {
            _output.Clear();

            for (int i = 0; i < lineCount; i++)
            {
                if (i > 0)
                    _output.Append('\n');

                AppendLine(_output, style);

                if (_output.Length >= maxChars)
                    break;
            }

            return _output.ToString();
        }

        private string BuildLine(in NoiseStyle style)
        {
            _lineBuilder.Clear();
            AppendLine(_lineBuilder, style);
            return _lineBuilder.ToString();
        }

        /// <summary>断片を繋いで指定文字数ぶんの 1 行を書き出す。</summary>
        private void AppendLine(StringBuilder sb, in NoiseStyle style)
        {
            GlitchText.GlitchWriter writer =
                new(sb, style.ColorRunLength, style.CorruptChance, _rng, style.Dim);

            while (writer.VisibleLength < style.CharsPerLine)
            {
                string fragment = _fragments[_rng.Next(_fragments.Length)];
                int room = style.CharsPerLine - writer.VisibleLength;

                if (fragment.Length >= room)
                {
                    for (int i = 0; i < room; i++)
                        writer.Feed(fragment[i]);

                    break;
                }

                writer.Feed(fragment);

                if (writer.VisibleLength < style.CharsPerLine)
                    writer.Feed(' ');
            }

            writer.End();
        }
    }

    // ===== フェーズ定義 =====

    private static int ResolvePhaseIndex(float elapsed)
    {
        float ratio = elapsed / Duration;

        for (int i = Phases.Length - 1; i >= 0; i--)
        {
            if (ratio >= Phases[i].StartRatio)
                return i;
        }

        return 0;
    }

    /// <summary>1 フェーズ分の描画・効果パラメータ。</summary>
    private sealed class TripPhase
    {
        /// <summary><see cref="Duration"/> に対する開始比率。</summary>
        public float StartRatio;

        /// <summary>再描画間隔（秒）。そのまま Hint の送信頻度になる。</summary>
        public float Interval = 0.3f;

        /// <summary>1 tick で描き直す既存行の本数。</summary>
        public int ChurnLines;

        /// <summary>同色で塗る文字数。小さいほど派手だが送信量が増える。</summary>
        public int ColorRunLength = 20;

        /// <summary>1 文字あたりの文字化け確率。</summary>
        public float CorruptChance;

        /// <summary>彩度を落としたパレットを使うか。序盤の「じわじわ来る」表現用。</summary>
        public bool DimNoise;

        /// <summary>ノイズ層を出さずバッファも捨てる（最終フェーズ用）。</summary>
        public bool ClearNoise;

        /// <summary>Hint 座標のランダム振れ幅（画面揺れ表現）。</summary>
        public float Shake;

        /// <summary>1 tick あたりのバースト発生確率。</summary>
        public float BurstChance;

        public int MessageFontSize = 40;

        /// <summary>メッセージ差し替え間隔（秒）。<see cref="Interval"/> と同値なら毎 tick 差し替わる。</summary>
        public float MessageInterval = 1f;

        /// <summary>メッセージを空にする確率。明滅の速さを決める。</summary>
        public float MessageBlankChance;

        /// <summary>メッセージ本文の文字化け率。</summary>
        public float MessageCorruptChance;

        /// <summary>メッセージの色替えピッチ。大きいほど単色に近づく。</summary>
        public int MessageColorRunLength = 24;

        /// <summary>null ならランダム色。</summary>
        public string? MessageColor;

        /// <summary>フェーズ中の FogControl Intensity。</summary>
        public byte FogIntensity;

        /// <summary>VisualTraumatized の Intensity。0 で解除。</summary>
        public byte TraumatizedIntensity;

        /// <summary>VisualSinkhole の Intensity。0 で解除。</summary>
        public byte SinkholeIntensity;

        /// <summary>フェーズ中ずっと Deafened を掛けるか。</summary>
        public bool Deafen;

        /// <summary>フェーズ突入時に短い Flashed を差し込むか。</summary>
        public bool FlashOnEnter;

        /// <summary>フェーズ突入時に短い Blindness（暗転）を差し込むか。</summary>
        public bool BlindOnEnter;

        public string[] Messages = [];
    }

    // ===== 文言 =====

    /// <summary>予兆。まだ「気のせい」で済ませられる違和感。</summary>
    private static readonly string[] WhisperMessages =
    [
        "……なにか、におう",
        "…………",
        "いま、だれか しゃべった？",
        "{NAME}",
        "……あれ",
        "へやの かたちが ちがう",
        "まばたき、した？",
        "ゆびの かず",
    ];

    /// <summary>侵食。読んでいる文書と自分の現実が混ざり始める。</summary>
    private static readonly string[] CreepMessages =
    [
        "この文書を読んだ記録は残りません",
        "うしろの人数が さっきと違う",
        "あなたの職員番号を思い出せますか",
        "■■■博士の署名がある",
        "読むのを やめてください",
        "{NAME} という職員は在籍していません",
        "そこに書いてあるのは あなたの名前です",
        "まだ 半分も 読んでいない",
        "目を 離さないでください",
        "この部屋は 記録上 存在しません",
        "収容記録の日付が 明日になっている",
    ];

    /// <summary>崩壊開始。システム側が壊れていく。</summary>
    private static readonly string[] FractureMessages =
    [
        "SLAFIGHT.EXE (応答なし)",
        "MEMORY_ACCESS_VIOLATION AT 0x00000000",
        "整合性チェック: 失敗 (対象: {NAME})",
        "SCP-███ は収容されていません",
        "再起動を試みています ... 3 回目",
        "observer.dll を読み込めませんでした",
        "あなたの視点は現在 別の場所にあります",
        "█████████ を削除できません",
        "この記録は書き換えられました",
        "接続が確立されました (発信元: 不明)",
        "D-██████ の生存記録が見つかりません",
        "MEMORY LEAK: 意識",
        "HANDLE_NOT_CLOSED: {NAME}",
        "同じフレームを 412 回 描画しています",
    ];

    /// <summary>最大強度。短く、断定的で、こちらを名指しする。</summary>
    private static readonly string[] CollapseMessages =
    [
        "ミ テ イ ル",
        "うしろ",
        "{NAME}",
        "ソレハ アナタ デハ ナイ",
        "目 ヲ 開 ケ ル ナ",
        "█████",
        "ここは Site-02 ではない",
        "かえして",
        "ドウシテ キヅカナイノ",
        "モウ オソイ",
        "アナタハ 何回目 デスカ",
        "ワタシノ 名前ヲ 言エ",
        "ズット イタ",
    ];

    /// <summary>静寂。何事もなかったことにされる。</summary>
    private static readonly string[] SilenceMessages =
    [
        "…………",
        "…………",
        "収容違反は発生していません",
        "あなたは なにも 見ませんでした",
        "…………",
        "ご協力ありがとうございました",
    ];

    /// <summary>バースト時にだけ 1 tick 表示される、短く大きい文言。</summary>
    private static readonly string[] BurstMessages =
    [
        "ミツケタ",
        "オカエリ",
        "ヨンダ？",
        "ソコ",
        "{NAME}",
        "ミルナ",
        "■■■■■■",
        "ワタシヲ ミテ",
        "ネエ",
        "ウシロ",
    ];

    private static readonly TripPhase[] Phases =
    [
        // 0.00 - 0.12 : 予兆。数行だけ、彩度も低い。
        new TripPhase
        {
            StartRatio = 0f,
            Interval = 0.50f,
            ChurnLines = 1,
            ColorRunLength = 26,
            CorruptChance = 0.03f,
            DimNoise = true,
            Shake = 0f,
            BurstChance = 0.02f,
            MessageFontSize = 30,
            MessageInterval = 2.0f,
            MessageBlankChance = 0.55f,
            MessageCorruptChance = 0f,
            MessageColorRunLength = 40,
            FogIntensity = (byte)(FogType.Amnesia + 1),
            Messages = WhisperMessages,
        },

        // 0.12 - 0.35 : 侵食。文字が溜まり始め、SCP-106 の視界演出が薄く乗る。
        new TripPhase
        {
            StartRatio = 0.12f,
            Interval = 0.35f,
            ChurnLines = 2,
            ColorRunLength = 24,
            CorruptChance = 0.08f,
            Shake = 4f,
            BurstChance = 0.07f,
            MessageFontSize = 38,
            MessageInterval = 1.2f,
            MessageBlankChance = 0.40f,
            MessageCorruptChance = 0.02f,
            MessageColorRunLength = 24,
            FogIntensity = (byte)(FogType.Scp244 + 1),
            TraumatizedIntensity = 80,
            Messages = CreepMessages,
        },

        // 0.35 - 0.60 : 崩壊開始。沈み込みの足音が付き、バーストが目立ち始める。
        new TripPhase
        {
            StartRatio = 0.35f,
            Interval = 0.25f,
            ChurnLines = 4,
            ColorRunLength = 22,
            CorruptChance = 0.18f,
            Shake = 14f,
            BurstChance = 0.16f,
            MessageFontSize = 52,
            MessageInterval = 0.50f,
            MessageBlankChance = 0.30f,
            MessageCorruptChance = 0.08f,
            MessageColorRunLength = 12,
            FogIntensity = (byte)(FogType.Nuke + 1),
            TraumatizedIntensity = 160,
            SinkholeIntensity = 255,
            Messages = FractureMessages,
        },

        // 0.60 - 0.85 : 最大強度。画面が埋まりきり、3 tick に 1 回はバーストする。
        new TripPhase
        {
            StartRatio = 0.60f,
            Interval = 0.20f,
            ChurnLines = 8,
            ColorRunLength = 20,
            CorruptChance = 0.35f,
            Shake = 34f,
            BurstChance = 0.30f,
            MessageFontSize = 76,
            MessageInterval = 0.20f,
            MessageBlankChance = 0.35f,
            MessageCorruptChance = 0.18f,
            MessageColorRunLength = 5,
            FogIntensity = 255, // MaxIntensity(=FogType の数) に丸められ FogType.PocketDimension になる
            TraumatizedIntensity = 255,
            SinkholeIntensity = 255,
            FlashOnEnter = true,
            Messages = CollapseMessages,
        },

        // 0.85 - 1.00 : 静寂。暗転を挟んで全部消え、音も遠くなる。
        new TripPhase
        {
            StartRatio = 0.85f,
            Interval = 0.50f,
            ClearNoise = true,
            MessageFontSize = 44,
            MessageInterval = 2.2f,
            MessageBlankChance = 0.15f,
            MessageColor = "#9a9a9a",
            FogIntensity = (byte)(FogType.BecomingFlamingo + 1),
            Deafen = true,
            BlindOnEnter = true,
            Messages = SilenceMessages,
        },
    ];
}
