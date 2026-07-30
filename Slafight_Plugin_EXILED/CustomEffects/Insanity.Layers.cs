#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Utilities;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using ExiledPlayer = Exiled.API.Features.Player;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;
using Random = System.Random;

namespace Slafight_Plugin_EXILED.CustomEffects;

/// <summary>
/// <see cref="Insanity"/> の描画レイヤー実装。ノイズ層のバッファとレイアウト、暴走テキスト層、
/// メッセージのトークン解決をここにまとめている。
/// </summary>
public partial class Insanity
{
    // ===== 種別 =====

    /// <summary>1 tick だけ挿入される演出。</summary>
    public enum BurstKind
    {
        /// <summary>一部の行を巨大化。</summary>
        Zoom,

        /// <summary>極小フォントで画面全面を埋める。</summary>
        Flood,

        /// <summary>全文字化け。</summary>
        Corrupt,

        /// <summary>1 tick だけ消える。</summary>
        Blackout,

        /// <summary>短い行が斜めの帯になって画面を横切る。</summary>
        Sweep,

        /// <summary>ブロック文字の壁で画面を塗り潰す。</summary>
        Wall,

        /// <summary>行が左右に割れて画面が真っ二つにズレる。</summary>
        Split,

        /// <summary>暴走テキストが一斉に増えて加速する。</summary>
        Swarm,
    }

    /// <summary>ノイズ層の行をどう配置するか。</summary>
    public enum NoiseLayout
    {
        /// <summary>全行を中央揃えで積む（従来の見た目）。</summary>
        Block,

        /// <summary>行ごとにランダムな横位置。画面全体に散る。</summary>
        Scatter,

        /// <summary>行ごとに横位置を階段状にずらす。スクロールさせると斜めに流れる。</summary>
        Diagonal,

        /// <summary>全行が同じ横位置で左右に走る。</summary>
        Ticker,

        /// <summary>奇数行と偶数行を左右に振り分ける。</summary>
        Column,
    }

    /// <summary>メッセージ層の装飾バリエーション。</summary>
    private enum MessageStyle
    {
        Plain,
        Marked,
        Spaced,
        Tilted,
        Vertical,
        Repeated,
    }

    // ===== ノイズ層のパラメータ =====

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

    /// <summary>1 tick ぶんのノイズ層レイアウト状態。</summary>
    private readonly struct NoiseFrame
    {
        public NoiseFrame(NoiseLayout layout, float scroll, float spread, float shake, float stepPerLine = 44f)
        {
            Layout = layout;
            Scroll = scroll;
            Spread = spread;
            Shake = shake;
            StepPerLine = stepPerLine;
        }

        /// <summary>装飾なしで中央に積むだけのフレーム。</summary>
        public static NoiseFrame Plain => new(NoiseLayout.Block, 0f, 0f, 0f);

        public NoiseLayout Layout { get; }

        /// <summary>Diagonal / Ticker の横オフセット。経過とともに動かすと流れて見える。</summary>
        public float Scroll { get; }

        /// <summary>横方向の振れ幅。</summary>
        public float Spread { get; }

        /// <summary>行ごとの追加ジッター。</summary>
        public float Shake { get; }

        /// <summary>Diagonal で 1 行進むごとにずらす量。</summary>
        public float StepPerLine { get; }

        /// <summary>行ごとに <c>&lt;pos&gt;</c> を打つレイアウトかどうか。</summary>
        public bool UsesPerLinePosition => Layout != NoiseLayout.Block;
    }

    /// <summary>
    /// 色付け済みの行を溜め込んでおくバッファ。
    /// 「増えていく」表現のため行を消さずに積み、描画時にレイアウトぶんの
    /// <c>&lt;pos&gt;</c> だけを前置する。色付けのやり直しは発生しない。
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

        public string RenderAll(int maxChars, in NoiseFrame frame) => RenderSlice(0, _lines.Count, maxChars, frame);

        public string RenderSlice(int start, int count, int maxChars, in NoiseFrame frame)
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

                AppendLinePosition(_output, i - start, frame);
                _output.Append(_lines[i]);

                if (_output.Length >= maxChars)
                    break;
            }

            return _output.ToString();
        }

        /// <summary>キャッシュを使わずその場で作り捨てる。バースト演出専用。</summary>
        public string RenderFresh(int lineCount, in NoiseStyle style, int maxChars, in NoiseFrame frame)
        {
            _output.Clear();

            for (int i = 0; i < lineCount; i++)
            {
                if (i > 0)
                    _output.Append('\n');

                AppendLinePosition(_output, i, frame);
                AppendLine(_output, style);

                if (_output.Length >= maxChars)
                    break;
            }

            return _output.ToString();
        }

        /// <summary>ブロック文字だけの壁。文字生成コストがほぼ無いので全面塗り潰しに向く。</summary>
        public string RenderWall(int lineCount, int charsPerLine, int colorRunLength, int maxChars)
        {
            _output.Clear();

            for (int i = 0; i < lineCount; i++)
            {
                if (i > 0)
                    _output.Append('\n');

                GlitchText.GlitchWriter writer = new(_output, colorRunLength, 0f, _rng);

                for (int c = 0; c < charsPerLine; c++)
                    writer.Feed(_rng.Next(4) == 0 ? '▓' : '█');

                writer.End();

                if (_output.Length >= maxChars)
                    break;
            }

            return _output.ToString();
        }

        /// <summary>
        /// レイアウトに応じた行頭の <c>&lt;pos&gt;</c> を書き出す。
        /// Block では何も書かない（Hint 側の中央揃えをそのまま使う）。
        /// </summary>
        private void AppendLinePosition(StringBuilder sb, int index, in NoiseFrame frame)
        {
            if (!frame.UsesPerLinePosition)
                return;

            float spread = Mathf.Max(1f, frame.Spread);

            float x = frame.Layout switch
            {
                NoiseLayout.Scatter => (float)((_rng.NextDouble() * 2d - 1d) * spread),
                NoiseLayout.Diagonal => Mathf.Repeat(index * frame.StepPerLine + frame.Scroll, spread * 2f) - spread,
                NoiseLayout.Ticker => Mathf.Repeat(frame.Scroll, spread * 2f) - spread,
                NoiseLayout.Column => ((index & 1) == 0 ? -spread : spread) * 0.55f,
                _ => 0f,
            };

            x += Jitter(_rng, frame.Shake);

            sb.Append("<pos=").Append(Mathf.RoundToInt(x)).Append('>');
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

    // ===== 暴走テキスト層 =====

    /// <summary>画面内を斜めに走り、壁で跳ね返る 1 個ぶんの文字列。</summary>
    private sealed class Roamer
    {
        public Hint Hint = null!;
        public float X;
        public float Y;
        public float DirX;
        public float DirY;
        public bool Active;
    }

    /// <summary>
    /// 暴走テキスト層。<see cref="MaxRoamers"/> 個の Hint を最初に確保しておき、
    /// フェーズが要求する数だけ表示する。位置は毎 tick 更新され、四辺で反射する。
    /// </summary>
    private sealed class RoamerField
    {
        private readonly Roamer[] _roamers;
        private readonly Random _rng;
        private readonly StringBuilder _sb = new(192);

        public RoamerField(PlayerDisplay display, ExiledPlayer player, Random rng)
        {
            _rng = rng;
            _roamers = new Roamer[Mathf.Max(0, MaxRoamers)];

            for (int i = 0; i < _roamers.Length; i++)
            {
                Hint hint = new()
                {
                    Id = RoamerHintId(player, i),
                    Alignment = HintAlignment.Center,
                    YCoordinateAlign = HintVerticalAlign.Middle,
                    XCoordinate = 0f,
                    YCoordinate = NoiseCenterY,
                    FontSize = 34,
                    Text = string.Empty,
                    Hide = true,
                    SyncSpeed = HintSyncSpeed.Fastest,
                    BlocksDynamicHints = false,
                };

                display.AddHint(hint);
                _roamers[i] = new Roamer { Hint = hint };
            }
        }

        public void HideAll()
        {
            foreach (Roamer roamer in _roamers)
            {
                roamer.Active = false;
                roamer.Hint.Hide = true;
            }
        }

        public void Tick(
            TripPhase phase,
            float severity,
            float deltaTime,
            bool swarm,
            MessageContext context,
            string[]? overrideBank)
        {
            if (_roamers.Length == 0)
                return;

            int target = Mathf.Clamp(
                Mathf.RoundToInt(phase.RoamerCount * severity) + (swarm ? 6 : 0),
                0,
                _roamers.Length);

            float speed = phase.RoamerSpeed * (0.55f + severity * 0.75f) * (swarm ? 1.9f : 1f);
            string[] bank = ResolveBank(phase, overrideBank);

            for (int i = 0; i < _roamers.Length; i++)
            {
                Roamer roamer = _roamers[i];

                if (i >= target)
                {
                    if (roamer.Active)
                    {
                        roamer.Active = false;
                        roamer.Hint.Hide = true;
                    }

                    continue;
                }

                if (!roamer.Active)
                {
                    Spawn(roamer, phase, context, bank);
                    roamer.Active = true;
                    roamer.Hint.Hide = false;
                }

                roamer.X += roamer.DirX * speed * deltaTime;
                roamer.Y += roamer.DirY * speed * deltaTime;

                bool bounced = false;

                if (roamer.X < -RoamHalfWidth)
                {
                    roamer.X = -RoamHalfWidth;
                    roamer.DirX = Mathf.Abs(roamer.DirX);
                    bounced = true;
                }
                else if (roamer.X > RoamHalfWidth)
                {
                    roamer.X = RoamHalfWidth;
                    roamer.DirX = -Mathf.Abs(roamer.DirX);
                    bounced = true;
                }

                if (roamer.Y < RoamTop)
                {
                    roamer.Y = RoamTop;
                    roamer.DirY = Mathf.Abs(roamer.DirY);
                    bounced = true;
                }
                else if (roamer.Y > RoamBottom)
                {
                    roamer.Y = RoamBottom;
                    roamer.DirY = -Mathf.Abs(roamer.DirY);
                    bounced = true;
                }

                // 跳ね返った瞬間と、たまに走行中にも文言が入れ替わる。
                if (bounced || _rng.NextDouble() < phase.RoamerRespawnChance)
                    Retext(roamer, phase, context, bank);

                roamer.Hint.XCoordinate = roamer.X;
                roamer.Hint.YCoordinate = roamer.Y;
            }
        }

        private static string[] ResolveBank(TripPhase phase, string[]? overrideBank)
        {
            if (overrideBank is { Length: > 0 })
                return overrideBank;

            if (phase.RoamerMessages is { Length: > 0 })
                return phase.RoamerMessages;

            return phase.Messages.Length > 0 ? phase.Messages : BurstMessages;
        }

        private void Spawn(Roamer roamer, TripPhase phase, MessageContext context, string[] bank)
        {
            roamer.X = (float)((_rng.NextDouble() * 2d - 1d) * RoamHalfWidth);
            roamer.Y = RoamTop + (float)(_rng.NextDouble() * (RoamBottom - RoamTop));

            // 真横・真縦だと単調なので、斜めに寄せた向きだけを選ぶ。
            float angle = (float)(_rng.NextDouble() * Math.PI * 2d);
            float dirX = Mathf.Cos(angle);
            float dirY = Mathf.Sin(angle);

            if (Mathf.Abs(dirX) < 0.35f)
                dirX = dirX < 0f ? -0.35f : 0.35f;

            if (Mathf.Abs(dirY) < 0.30f)
                dirY = dirY < 0f ? -0.30f : 0.30f;

            roamer.DirX = dirX;
            roamer.DirY = dirY;

            Retext(roamer, phase, context, bank);
        }

        private void Retext(Roamer roamer, TripPhase phase, MessageContext context, string[] bank)
        {
            if (bank.Length == 0)
                return;

            string body = context.Resolve(bank[_rng.Next(bank.Length)], _rng);

            if (body.Length == 0)
                body = "■■■";

            if (body.Length > 24)
                body = body.Substring(0, 24);

            _sb.Clear();
            _sb.Append("<b>");

            GlitchText.GlitchWriter writer = new(
                _sb,
                Mathf.Max(2, phase.RoamerColorRunLength),
                phase.MessageCorruptChance * 1.5f,
                _rng);

            writer.Feed(body);
            writer.End();

            _sb.Append("</b>");

            roamer.Hint.Text = _sb.ToString();
            roamer.Hint.FontSize = Mathf.Clamp(
                phase.RoamerFontSize + _rng.Next(-8, 15),
                14,
                90);
        }
    }

    // ===== メッセージのトークン解決 =====

    /// <summary>
    /// 文言に埋め込まれたトークンを、そのプレイヤーの「いま」で置き換える。
    /// 自分の名前・他人の名前・部屋・体力・手持ち・経過時間が混ざるので、
    /// 同じ文言でも読むたびに刺さり方が変わる。
    /// </summary>
    private sealed class MessageContext
    {
        private readonly ExiledPlayer _player;

        public MessageContext(ExiledPlayer player)
        {
            _player = player;
            Nickname = ResolveNickname(player);
        }

        public string Nickname { get; }

        public string Resolve(string body, Random rng)
        {
            if (string.IsNullOrEmpty(body) || body.IndexOf('{') < 0)
                return body;

            if (Contains(body, NameToken))
                body = body.Replace(NameToken, Nickname);

            if (Contains(body, OtherToken))
                body = body.Replace(OtherToken, ResolveOtherNickname(rng));

            if (Contains(body, RoomToken))
                body = body.Replace(RoomToken, ResolveRoom());

            if (Contains(body, ZoneToken))
                body = body.Replace(ZoneToken, ResolveZone());

            if (Contains(body, HealthToken))
                body = body.Replace(HealthToken, Mathf.RoundToInt(_player.Health).ToString());

            if (Contains(body, ItemToken))
                body = body.Replace(ItemToken, ResolveItem());

            if (Contains(body, TimeToken))
                body = body.Replace(TimeToken, Round.ElapsedTime.ToString(@"mm\:ss"));

            if (Contains(body, NumberToken))
                body = body.Replace(NumberToken, rng.Next(2, 999).ToString());

            return body;
        }

        private static bool Contains(string body, string token) =>
            body.IndexOf(token, StringComparison.Ordinal) >= 0;

        private string ResolveOtherNickname(Random rng)
        {
            List<ExiledPlayer> candidates = [];

            foreach (ExiledPlayer other in ExiledPlayer.List)
            {
                if (other is null || other == _player || !other.IsSafePlayer())
                    continue;

                candidates.Add(other);
            }

            return candidates.Count == 0
                ? "■■■■"
                : ResolveNickname(candidates[rng.Next(candidates.Count)]);
        }

        private string ResolveRoom()
        {
            return _player.CurrentRoom is { } room
                ? room.Type.TranslateRoomName()
                : "■■■";
        }

        private string ResolveZone()
        {
            return _player.CurrentRoom is { } room
                ? room.Zone.TranslateZoneName()
                : "■■■";
        }

        private string ResolveItem()
        {
            return _player.CurrentItem is { } item
                ? item.Type.ToString()
                : "なにも持っていない手";
        }
    }
}
