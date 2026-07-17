using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// 汎用ボス HP バー。Broadcast を一定間隔で更新し続けることで「常駐するバー」を表現する。
/// 任意のボス／イベントから使い回せる。
///
/// <example>
/// <code>
/// var bar = new BossBar { Title = "DANTE", TitleColor = "#ff1a1a", BarColor = "#39ff14", MaxValue = 5000 };
/// bar.Show();
/// // 毎フレーム/毎 tick 値だけ更新（再ブロードキャストは BossBar 管理側で行う）
/// bar.Value = currentHp;
/// bar.Subtitle = "第二幕";
/// bar.StateText = invulnerable ? "★ 無敵 ★" : null; // 設定するとバー行の代わりに表示
/// // プレイヤーのHPをそのまま表示したい場合
/// new BossBar { Title = "SERGEY" }.TrackPlayer(player, show: true);
/// // 終了時
/// bar.Hide();
/// </code>
/// </example>
/// </summary>
public sealed class BossBar
{
    private static readonly object SyncRoot = new();
    private static readonly List<BossBar> ShownBars = new();
    private static CoroutineHandle _refreshHandle;
    private static long _nextShowOrder;

    private long _showOrder;

    /// <summary>見出し（ボス名）。</summary>
    public string Title { get; set; } = "BOSS";

    /// <summary>見出しの色（rich text の color 値）。</summary>
    public string TitleColor { get; set; } = "#ff1a1a";

    /// <summary>第 2 見出し（フェーズ名など）。null/空なら非表示。</summary>
    public string? Subtitle { get; set; }

    /// <summary>最大値。</summary>
    public float MaxValue { get; set; } = 1f;

    /// <summary>現在値。</summary>
    public float Value { get; set; } = 1f;

    /// <summary>
    /// 自動追跡対象。<see cref="AutoTrackPlayer"/> が true のとき、描画前に Health/MaxHealth を同期する。
    /// </summary>
    public Player? TrackedPlayer { get; set; }

    /// <summary><see cref="TrackedPlayer"/> の Health/MaxHealth を自動でバーへ反映するか。</summary>
    public bool AutoTrackPlayer { get; set; }

    /// <summary>追跡対象が切断・消滅・死亡で無効になったとき、自動でバーを非表示にするか。</summary>
    public bool HideWhenTrackedPlayerUnavailable { get; set; } = true;

    /// <summary>追跡対象の死亡を無効扱いにするか。false なら死亡後も最後に読める値で表示を続ける。</summary>
    public bool TreatTrackedPlayerDeathAsUnavailable { get; set; } = true;

    /// <summary>バーの色（rich text の color 値）。</summary>
    public string BarColor { get; set; } = "#ff3333";

    /// <summary>バーの分割数。</summary>
    public int Segments { get; set; } = 20;

    /// <summary>数値（current / max）を併記するか。</summary>
    public bool ShowNumbers { get; set; } = true;

    /// <summary>
    /// 設定すると、バー行の代わりにこの文字列をそのまま表示する（無敵中など）。null でバー表示に戻る。
    /// </summary>
    public string? StateText { get; set; }

    /// <summary>再ブロードキャスト間隔（秒）。</summary>
    public float RefreshInterval { get; set; } = 1f;

    /// <summary>各ブロードキャストの表示時間（秒）。RefreshInterval より少し長めにすると途切れない。</summary>
    public ushort BroadcastDuration { get; set; } = 2;

    /// <summary>
    /// 表示優先度。小さい値ほど上に表示され、同じ値なら <see cref="Show"/> された順に下へ積まれる。
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>表示対象フィルタ。null なら全非ホストプレイヤー。</summary>
    public Func<Player, bool>? Viewers { get; set; }

    /// <summary>表示中か。</summary>
    public bool IsShown { get; private set; }

    /// <summary>対象プレイヤーの Health/MaxHealth を自動追跡する。</summary>
    public BossBar TrackPlayer(Player player, bool show = false, bool hideWhenUnavailable = true)
    {
        TrackedPlayer = player;
        AutoTrackPlayer = true;
        HideWhenTrackedPlayerUnavailable = hideWhenUnavailable;
        TryUpdateTrackedPlayer();

        if (show)
            Show();

        return this;
    }

    /// <summary>プレイヤー自動追跡を解除する。現在の Value/MaxValue は維持される。</summary>
    public BossBar StopTrackingPlayer()
    {
        AutoTrackPlayer = false;
        TrackedPlayer = null;
        return this;
    }

    /// <summary>表示を開始する。複数の BossBar は共有マネージャーで 1 つの Broadcast に統合される。</summary>
    public BossBar Show()
    {
        lock (SyncRoot)
        {
            if (!IsShown)
            {
                IsShown = true;
                _showOrder = ++_nextShowOrder;
                ShownBars.Add(this);
            }

            EnsureRefreshLoop();
        }

        RefreshAll();
        return this;
    }

    /// <summary>
    /// 表示（更新ループ）を停止する。既定では最後のブロードキャストが自然消滅するのを待つだけで、
    /// 他のブロードキャスト（勝利演出など）を巻き込まない。<paramref name="clearBroadcasts"/> が true の
    /// ときだけ対象プレイヤーのブロードキャストを即時クリアする。
    /// </summary>
    public void Hide(bool clearBroadcasts = false)
        => HideInternal(clearBroadcasts, refreshRemainingBars: true);

    private void HideInternal(bool clearBroadcasts, bool refreshRemainingBars)
    {
        bool hasRemainingBars;
        lock (SyncRoot)
        {
            if (IsShown)
            {
                IsShown = false;
                ShownBars.Remove(this);
            }

            hasRemainingBars = ShownBars.Count > 0;
            if (!hasRemainingBars && _refreshHandle.IsRunning)
            {
                Timing.KillCoroutines(_refreshHandle);
                _refreshHandle = default;
            }
        }

        if (clearBroadcasts)
        {
            foreach (Player player in GetViewers())
            {
                try { player.ClearBroadcasts(); } catch { /* ignore */ }
            }
        }

        if (refreshRemainingBars && hasRemainingBars)
            RefreshAll();
    }

    /// <summary>表示中の全 BossBar を停止する。</summary>
    public static void HideAll(bool clearBroadcasts = false)
    {
        lock (SyncRoot)
        {
            foreach (BossBar bar in ShownBars)
                bar.IsShown = false;

            ShownBars.Clear();
            if (_refreshHandle.IsRunning)
                Timing.KillCoroutines(_refreshHandle);

            _refreshHandle = default;
        }

        if (!clearBroadcasts)
            return;

        foreach (Player player in Player.List.Where(p => p is not null && p.IsNotHost()))
        {
            try { player.ClearBroadcasts(); } catch { /* ignore */ }
        }
    }

    /// <summary>現在の状態からバー文字列を生成する（文字列だけ欲しい場合にも使える）。</summary>
    public string Render()
    {
        TryUpdateTrackedPlayer();

        string header = $"<size=24><b><color={TitleColor}>{Title}</color></b>";
        if (!string.IsNullOrEmpty(Subtitle))
            header += $"  <color=#bbbbbb>─ {Subtitle} ─</color>";
        header += "</size>";

        string second;
        if (!string.IsNullOrEmpty(StateText))
        {
            second = StateText!;
        }
        else
        {
            float visibleValue = Mathf.Max(0f, Value);
            second = $"<size=26><color={BarColor}>{RenderBar(visibleValue, MaxValue, Segments)}</color></size>";
            if (ShowNumbers)
                second += $"  <size=18>{Mathf.CeilToInt(visibleValue)} / {Mathf.CeilToInt(MaxValue)}</size>";
        }

        return header + "\n" + second;
    }

    /// <summary>値と最大値からバー部分（█/░）だけを生成する静的ヘルパー。</summary>
    public static string RenderBar(float value, float max, int segments = 20)
    {
        segments = Math.Max(1, segments);
        float ratio = max > 0f ? Mathf.Clamp01(value / max) : 0f;
        int filled = Mathf.Clamp(Mathf.RoundToInt(ratio * segments), 0, segments);
        return new string('█', filled) + new string('░', segments - filled);
    }

    private static void EnsureRefreshLoop()
    {
        if (!_refreshHandle.IsRunning)
            _refreshHandle = Timing.RunCoroutine(RefreshLoop());
    }

    private static IEnumerator<float> RefreshLoop()
    {
        for (;;)
        {
            List<BossBar> bars = SnapshotShownBars();
            if (bars.Count == 0)
            {
                _refreshHandle = default;
                yield break;
            }

            RefreshAll(bars);
            yield return Timing.WaitForSeconds(GetRefreshInterval(bars));
        }
    }

    private static void RefreshAll()
        => RefreshAll(SnapshotShownBars());

    private static void RefreshAll(IReadOnlyList<BossBar> bars)
    {
        if (bars.Count == 0)
            return;

        List<BossBar> activeBars = bars.Where(bar => bar.TryUpdateTrackedPlayer()).ToList();
        if (activeBars.Count == 0)
            return;

        foreach (Player player in Player.List.Where(p => p is not null && p.IsNotHost()))
        {
            List<BossBar> visibleBars = activeBars.Where(bar => bar.CanView(player)).ToList();
            if (visibleBars.Count == 0)
                continue;

            string message = string.Join("\n", visibleBars.Select(bar => bar.Render()));
            ushort duration = visibleBars.Max(bar => bar.BroadcastDuration);

            try { player.Broadcast(duration, message, global::Broadcast.BroadcastFlags.Normal, true); }
            catch { /* ignore */ }
        }
    }

    private static List<BossBar> SnapshotShownBars()
    {
        lock (SyncRoot)
        {
            return ShownBars
                .Where(bar => bar.IsShown)
                .OrderBy(bar => bar.DisplayOrder)
                .ThenBy(bar => bar._showOrder)
                .ToList();
        }
    }

    private static float GetRefreshInterval(IEnumerable<BossBar> bars)
        => Mathf.Max(0.1f, bars.Min(bar => bar.RefreshInterval));

    private bool TryUpdateTrackedPlayer()
    {
        if (!AutoTrackPlayer)
            return true;

        Player? player = TrackedPlayer;
        if (!IsTrackedPlayerAvailable(player))
        {
            if (HideWhenTrackedPlayerUnavailable)
                HideInternal(clearBroadcasts: false, refreshRemainingBars: false);

            return !HideWhenTrackedPlayerUnavailable;
        }

        MaxValue = Mathf.Max(1f, player!.MaxHealth);
        Value = Mathf.Clamp(player.Health, 0f, MaxValue);
        return true;
    }

    private bool IsTrackedPlayerAvailable(Player? player)
    {
        try
        {
            if (player is null || player.ReferenceHub is null || !player.IsConnected)
                return false;

            return !TreatTrackedPlayerDeathAsUnavailable || player.IsAlive;
        }
        catch
        {
            return false;
        }
    }

    private bool CanView(Player player)
    {
        try { return player is not null && player.IsNotHost() && (Viewers is null || Viewers(player)); }
        catch { return false; }
    }

    private IEnumerable<Player> GetViewers()
        => Player.List.Where(CanView);
}
