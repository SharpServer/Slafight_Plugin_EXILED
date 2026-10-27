using System;
using System.Collections.Generic;
using System.Linq;
using AdminToys;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using Exiled.Events.EventArgs.Player;
using MEC;
using Mirror;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;
using Server = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Features;

// =========================================================
// 表示方針 enum
// =========================================================

/// <summary>
/// 所有者の観戦者に対する表示方針。
/// デフォルトは Show（所有者が設定されていれば観戦者にも表示）。
/// </summary>
public enum SpectatorVisibility
{
    Show,
    Hide,
}

// =========================================================
// NetworkShowState
// =========================================================

/// <summary>
/// 1 つの NetworkIdentity の表示状態です。変更後は <see cref="NetworkVisibilityManager.ApplyShowState"/> を呼ぶこと。
/// </summary>
public class NetworkShowState
{
    // ---- 所有者 ----

    /// <summary>所有者のプレイヤーID。null の場合は所有者なし。</summary>
    public int? OwnerId { get; set; }

    /// <summary>所有者本人に表示するか。</summary>
    public bool ShowToOwner { get; set; } = true;

    /// <summary>所有者の観戦者に対する表示方針。デフォルトは Show。</summary>
    public SpectatorVisibility SpectatorVisibility { get; set; } = SpectatorVisibility.Show;

    // ---- 個別指定 ----

    /// <summary>強制的に表示するプレイヤーIDのセット。ExplicitHide より低優先。</summary>
    public HashSet<int> ExplicitShow { get; } = [];

    /// <summary>強制的に非表示にするプレイヤーIDのセット。最高優先。</summary>
    public HashSet<int> ExplicitHide { get; } = [];

    /// <summary>null でなければ、この条件を満たすプレイヤーだけに表示する。</summary>
    public Predicate<Player>? VisibilityPredicate { get; set; }

    // ---- 優先順位に従って表示すべきか判定 ----

    /// <summary>
    /// 指定プレイヤーがこのオブジェクトを見るべきかを返す。
    /// 優先順位: ExplicitHide > ExplicitShow > ShowToOwner > SpectatorVisibility > Hide
    /// </summary>
    /// <param name="player">判定対象のプレイヤー。</param>
    /// <param name="allPlayers">未使用。</param>
    /// <param name="overrideIsSpectatingOwner">観戦状態の上書き。null なら実際の状態を見る。</param>
    public bool ShouldShow(
        Player player,
        IEnumerable<Player> allPlayers,
        bool? overrideIsSpectatingOwner = null)
    {
        if (player == null) return false;

        // 1. ExplicitHide
        if (ExplicitHide.Contains(player.Id)) return false;

        // 2. ExplicitShow
        if (ExplicitShow.Contains(player.Id)) return true;

        // 3. 任意条件
        if (VisibilityPredicate != null)
        {
            try
            {
                return VisibilityPredicate(player);
            }
            catch (Exception ex)
            {
                Log.Warn($"[NetworkVisibility] VisibilityPredicate 失敗: {ex.Message}");
                return false;
            }
        }

        // 4. 所有者本人
        if (OwnerId.HasValue && player.Id == OwnerId.Value) return ShowToOwner;

        // 5. 観戦者
        if (OwnerId.HasValue)
        {
            bool isSpectatingOwner = overrideIsSpectatingOwner ??
                (!player.IsAlive &&
                 player.CurrentSpectatingPlayers.Any(s => s?.Id == OwnerId.Value));

            if (isSpectatingOwner)
                return SpectatorVisibility == SpectatorVisibility.Show;
        }

        // 6. デフォルト Hide
        return false;
    }
}

// =========================================================
// NetworkVisibilityExtensions
// =========================================================

/// <summary>
/// NetworkIdentity を持つオブジェクトの表示を特定プレイヤーだけに限定するヘルパー群。
/// </summary>
public static class NetworkVisibilityManager
{
    // =========================================================
    // 内部ストア
    // =========================================================

    private static readonly Dictionary<uint, NetworkShowState>   _states         = new();
    private static readonly Dictionary<uint, NetworkIdentity>    _identityCache  = new();
    private static bool _registered;

    // Verified / Spawned は「1人につき管理オブジェクト全走査」だった。
    // ラウンド開始は全員が同じフレームでスポーンするため、人数分の全走査が積み上がる。
    // 1フレーム分をまとめて 1 回の走査で流す。
    private static readonly HashSet<int>  _pendingRefreshIds = [];
    private static readonly List<Player>  _refreshBuffer     = [];
    private static bool _refreshScheduled;

    // =========================================================
    // Show 拒否フック
    // =========================================================

    /// <summary>
    /// Show を最優先で拒否する追加判定。true を返した (identity, player) の組は Show されない。
    /// </summary>
    public static Func<NetworkIdentity, Player, bool>? ShowVeto { get; set; }

    private static bool IsShowVetoed(NetworkIdentity identity, Player player)
    {
        var veto = ShowVeto;
        if (veto == null) return false;

        try { return veto(identity, player); }
        catch (Exception ex)
        {
            Log.Warn($"[NetworkVisibility] ShowVeto 失敗: {ex.Message}");
            return false;
        }
    }

    // =========================================================
    // Register / Unregister
    // =========================================================

    public static void Register()
    {
        if (_registered)
            return;

        Server.RoundStarted            += OnRoundStarted;
        Exiled.Events.Handlers.Player.Verified                += OnVerified;
        Exiled.Events.Handlers.Player.Spawned                 += OnSpawned;
        Exiled.Events.Handlers.Player.ChangingSpectatedPlayer += OnChangingSpectatedPlayer;
        _registered = true;
    }

    public static void Unregister()
    {
        if (_registered)
        {
            Server.RoundStarted            -= OnRoundStarted;
            Exiled.Events.Handlers.Player.Verified                -= OnVerified;
            Exiled.Events.Handlers.Player.Spawned                 -= OnSpawned;
            Exiled.Events.Handlers.Player.ChangingSpectatedPlayer -= OnChangingSpectatedPlayer;
            _registered = false;
        }

        ShowVeto = null;
        _states.Clear();
        _identityCache.Clear();
        _pendingRefreshIds.Clear();
        _refreshBuffer.Clear();
    }

    // =========================================================
    // イベントハンドラ
    // =========================================================

    private static void OnRoundStarted()
    {
        _states.Clear();
        _identityCache.Clear();
        _pendingRefreshIds.Clear();
        _refreshBuffer.Clear();
    }

    private static void OnVerified(VerifiedEventArgs? ev)
    {
        if (ev?.Player == null) return;
        QueueRefresh(ev.Player);
    }

    /// <summary>
    /// スポーン後、Mirror の再送信で崩れた管理オブジェクトの表示状態を補正する。
    /// </summary>
    private static void OnSpawned(SpawnedEventArgs? ev)
    {
        if (ev?.Player == null) return;
        QueueRefresh(ev.Player);
    }

    /// <summary>
    /// 対象プレイヤーの再評価を次フレームにまとめる。
    /// スポーン直後は Mirror の再送信が走るため、補正は必ず1フレーム後に行う。
    /// </summary>
    private static void QueueRefresh(Player player)
    {
        if (player == null || !player.IsConnected)
            return;

        // 管理オブジェクトが1つも無ければ送るものが無い（ラウンド開始直後がこれ）。
        if (_identityCache.Count == 0)
            return;

        _pendingRefreshIds.Add(player.Id);

        if (_refreshScheduled)
            return;

        _refreshScheduled = true;
        Timing.CallDelayed(0f, FlushPendingRefresh);
    }

    private static void FlushPendingRefresh()
    {
        _refreshScheduled = false;

        if (_pendingRefreshIds.Count == 0 || _identityCache.Count == 0)
        {
            _pendingRefreshIds.Clear();
            return;
        }

        _refreshBuffer.Clear();
        foreach (var playerId in _pendingRefreshIds)
        {
            var player = Player.Get(playerId);
            if (player != null && player.IsConnected)
                _refreshBuffer.Add(player);
        }

        _pendingRefreshIds.Clear();

        if (_refreshBuffer.Count == 0)
            return;

        // 走査は1回。state の引き直しも identity ごとに1回で済む。
        foreach (var (netId, identity) in _identityCache)
        {
            if (identity == null) continue;
            if (!_states.TryGetValue(netId, out var state)) continue;

            foreach (var player in _refreshBuffer)
                SendVisibility(identity, player, state.ShouldShow(player, Player.List));
        }

        _refreshBuffer.Clear();
    }

    private static void OnChangingSpectatedPlayer(ChangingSpectatedPlayerEventArgs? ev)
    {
        if (ev?.Player == null) return;

        foreach (var (netId, identity) in _identityCache)
        {
            if (identity == null) continue;
            if (!_states.TryGetValue(netId, out var state)) continue;
            if (!state.OwnerId.HasValue) continue;

            bool wasSpectatingOwner =
                ev.OldTarget != null && ev.OldTarget.Id == state.OwnerId.Value;
            bool willSpectateOwner =
                ev.NewTarget != null && ev.NewTarget.Id == state.OwnerId.Value;

            // 観戦対象が変わった場合のみ更新
            if (wasSpectatingOwner == willSpectateOwner) continue;

            bool shouldShow = state.ShouldShow(
                ev.Player,
                Player.List,
                overrideIsSpectatingOwner: willSpectateOwner);

            SendVisibility(identity, ev.Player, shouldShow);
        }
    }

    // =========================================================
    // 低レベル送信
    // =========================================================

    /// <summary>指定プレイヤーにオブジェクトを表示させる。</summary>
    public static void ShowNetworkIdentity(this Player? player, NetworkIdentity identity)
    {
        if (player?.Connection == null || identity == null) return;
        if (IsShowVetoed(identity, player)) return;
        try { NetworkServer.ShowForConnection(identity, player.Connection); }
        catch (Exception ex) { Log.Warn($"[NetworkVisibility] ShowNetworkIdentity 失敗: {ex.Message}"); }
    }

    /// <summary>指定プレイヤーからオブジェクトを非表示にする。</summary>
    public static void HideNetworkIdentity(this Player? player, NetworkIdentity identity)
    {
        if (player?.Connection == null || identity == null) return;
        try { NetworkServer.HideForConnection(identity, player.Connection); }
        catch (Exception ex) { Log.Warn($"[NetworkVisibility] HideNetworkIdentity 失敗: {ex.Message}"); }
    }

    private static void SendVisibility(NetworkIdentity identity, Player player, bool show)
    {
        if (show) player.ShowNetworkIdentity(identity);
        else      player.HideNetworkIdentity(identity);
    }

    /// <summary>管理中の全オブジェクトを指定プレイヤー向けに再評価する。</summary>
    public static void RefreshPlayer(Player? player)
    {
        if (player == null || !player.IsConnected) return;

        foreach (var (netId, identity) in _identityCache)
        {
            if (identity == null) continue;
            if (!_states.TryGetValue(netId, out var state)) continue;
            SendVisibility(identity, player, state.ShouldShow(player, Player.List));
        }
    }

    // =========================================================
    // 内部 Refresh
    // =========================================================

    /// <summary>対象 netId を全プレイヤーに対して State に従い再送信する。</summary>
    public static void ApplyShowState(this NetworkIdentity identity)
    {
        if (identity == null) return;
        if (!_states.TryGetValue(identity.netId, out var state)) return;

        foreach (var player in Player.List)
        {
            if (player == null || !player.IsConnected) continue;
            SendVisibility(identity, player, state.ShouldShow(player, Player.List));        }
    }

    private static void ApplyShowState(uint netId)
    {
        if (!_identityCache.TryGetValue(netId, out var identity) || identity == null) return;
        identity.ApplyShowState();
    }

    // =========================================================
    // State 取得
    // =========================================================

    /// <summary>
    /// 現在の NetworkShowState を取得する。
    /// 管理されていない場合は null を返す。
    /// </summary>
    public static NetworkShowState? GetShowState(this NetworkIdentity identity)
        => identity != null && _states.TryGetValue(identity.netId, out var s) ? s : null;

    public static NetworkShowState? GetShowState(this Primitive? primitive)
        => primitive?.Base?.netIdentity?.GetShowState();

    public static NetworkShowState? GetShowState(this Light? light)
        => light?.Base?.netIdentity?.GetShowState();

    public static NetworkShowState? GetShowState(this AdminToyBase? toy)
        => toy?.netIdentity?.GetShowState();

    // =========================================================
    // InitShowState（登録 & 全員Hide送信）
    // =========================================================

    /// <summary>
    /// NetworkIdentity を表示管理に登録する。
    /// Spawn 直後に呼ぶこと。初期 State は全員 Hide。
    /// </summary>
    public static void InitShowState(this NetworkIdentity identity, NetworkShowState? initialState = null)
    {
        if (identity == null) return;
        uint netId = identity.netId;

        _states[netId]        = initialState ?? new NetworkShowState();
        _identityCache[netId] = identity;

        identity.ApplyShowState();
    }

    /// <summary>表示管理から除外する。オブジェクト破棄前に呼ぶこと。</summary>
    public static void RemoveShowState(this NetworkIdentity identity)
    {
        if (identity == null) return;
        _states.Remove(identity.netId);
        _identityCache.Remove(identity.netId);
    }

    // =========================================================
    // UpdateShowState（State 丸替え / ミューテータ）
    // =========================================================

    /// <summary>
    /// State を新しいものと丸ごと置き換え、全員に再送信する。
    /// </summary>
    public static void UpdateShowState(this NetworkIdentity identity, NetworkShowState? newState)
    {
        if (identity == null || newState == null) return;
        uint netId = identity.netId;

        _states[netId]        = newState;
        _identityCache[netId] = identity;

        identity.ApplyShowState();
    }

    /// <summary>
    /// ミューテータで State を部分更新し、全員に再送信する。<br/>
    /// 未登録の場合は新規 State を作成して登録する。
    /// </summary>
    public static void UpdateShowState(this NetworkIdentity identity, Action<NetworkShowState>? mutate)
    {
        if (identity == null || mutate == null) return;
        uint netId = identity.netId;

        if (!_states.TryGetValue(netId, out var state))
        {
            state                 = new NetworkShowState();
            _states[netId]        = state;
            _identityCache[netId] = identity;
        }

        mutate(state);
        identity.ApplyShowState();
    }

    // =========================================================
    // Owner 系 API
    // =========================================================

    /// <summary>所有者を設定する。</summary>
    public static void SetOwner(this NetworkIdentity? identity, Player? owner, bool showToOwner = true)
        => identity?.UpdateShowState(s =>
        {
            s.OwnerId    = owner?.Id;
            s.ShowToOwner = showToOwner;
        });

    /// <summary>所有者を解除する。</summary>
    public static void ClearOwner(this NetworkIdentity? identity)
        => identity?.UpdateShowState(s => s.OwnerId = null);

    /// <summary>所有者本人への表示を切り替える。</summary>
    public static void SetShowToOwner(this NetworkIdentity? identity, bool show)
        => identity?.UpdateShowState(s => s.ShowToOwner = show);

    // =========================================================
    // SpectatorVisibility 系 API
    // =========================================================

    /// <summary>観戦者への表示方針を変更する。</summary>
    public static void SetSpectatorVisibility(this NetworkIdentity? identity, SpectatorVisibility vis)
        => identity?.UpdateShowState(s => s.SpectatorVisibility = vis);

    // =========================================================
    // ExplicitShow / ExplicitHide 系 API
    // =========================================================

    /// <summary>プレイヤーを強制表示リストに追加する。</summary>
    public static void AddExplicitShow(this NetworkIdentity? identity, Player player)
    {
        if (player == null) return;
        identity?.UpdateShowState(s =>
        {
            s.ExplicitHide.Remove(player.Id); // 競合解消
            s.ExplicitShow.Add(player.Id);
        });
    }

    /// <summary>プレイヤーを強制非表示リストに追加する。</summary>
    public static void AddExplicitHide(this NetworkIdentity? identity, Player player)
    {
        if (player == null) return;
        identity?.UpdateShowState(s =>
        {
            s.ExplicitShow.Remove(player.Id); // 競合解消
            s.ExplicitHide.Add(player.Id);
        });
    }

    /// <summary>プレイヤーを ExplicitShow/Hide 両方から除去する。</summary>
    public static void RemoveExplicit(this NetworkIdentity? identity, Player player)
    {
        if (player == null) return;
        identity?.UpdateShowState(s =>
        {
            s.ExplicitShow.Remove(player.Id);
            s.ExplicitHide.Remove(player.Id);
        });
    }

    /// <summary>ExplicitShow リストを全クリアする。</summary>
    public static void ClearExplicitShow(this NetworkIdentity? identity)
        => identity?.UpdateShowState(s => s.ExplicitShow.Clear());

    /// <summary>ExplicitHide リストを全クリアする。</summary>
    public static void ClearExplicitHide(this NetworkIdentity? identity)
        => identity?.UpdateShowState(s => s.ExplicitHide.Clear());

    /// <summary>ExplicitShow / ExplicitHide 両方を全クリアする。</summary>
    public static void ClearAllExplicit(this NetworkIdentity? identity)
        => identity?.UpdateShowState(s =>
        {
            s.ExplicitShow.Clear();
            s.ExplicitHide.Clear();
        });

    public static void SetVisibilityPredicate(
        this NetworkIdentity? identity,
        Predicate<Player>? predicate)
        => identity?.UpdateShowState(s => s.VisibilityPredicate = predicate);

    // =========================================================
    // Player 拡張（糖衣構文）
    // =========================================================

    /// <summary>このプレイヤーを identity の強制表示リストに追加する。</summary>
    public static void AddExplicitShow(this Player player, NetworkIdentity? identity)
        => identity?.AddExplicitShow(player);

    /// <summary>このプレイヤーを identity の強制非表示リストに追加する。</summary>
    public static void AddExplicitHide(this Player player, NetworkIdentity? identity)
        => identity?.AddExplicitHide(player);

    /// <summary>このプレイヤーを identity の Explicit 両リストから除去する。</summary>
    public static void RemoveExplicit(this Player player, NetworkIdentity? identity)
        => identity?.RemoveExplicit(player);

    // =========================================================
    // Primitive 向け糖衣構文
    // =========================================================

    public static void InitShowState(this Primitive? primitive, NetworkShowState? initialState = null)
        => primitive?.Base?.netIdentity?.InitShowState(initialState);

    public static void RemoveShowState(this Primitive? primitive)
        => primitive?.Base?.netIdentity?.RemoveShowState();

    public static void UpdateShowState(this Primitive? primitive, NetworkShowState newState)
        => primitive?.Base?.netIdentity?.UpdateShowState(newState);

    public static void UpdateShowState(this Primitive? primitive, Action<NetworkShowState> mutate)
        => primitive?.Base?.netIdentity?.UpdateShowState(mutate);

    public static void SetOwner(this Primitive? primitive, Player owner, bool showToOwner = true)
        => primitive?.Base?.netIdentity?.SetOwner(owner, showToOwner);

    public static void ClearOwner(this Primitive? primitive)
        => primitive?.Base?.netIdentity?.ClearOwner();

    public static void SetShowToOwner(this Primitive? primitive, bool show)
        => primitive?.Base?.netIdentity?.SetShowToOwner(show);

    public static void SetSpectatorVisibility(this Primitive? primitive, SpectatorVisibility vis)
        => primitive?.Base?.netIdentity?.SetSpectatorVisibility(vis);

    public static void AddExplicitShow(this Primitive? primitive, Player player)
        => primitive?.Base?.netIdentity?.AddExplicitShow(player);

    public static void AddExplicitHide(this Primitive? primitive, Player player)
        => primitive?.Base?.netIdentity?.AddExplicitHide(player);

    public static void RemoveExplicit(this Primitive? primitive, Player player)
        => primitive?.Base?.netIdentity?.RemoveExplicit(player);

    public static void ClearExplicitShow(this Primitive? primitive)
        => primitive?.Base?.netIdentity?.ClearExplicitShow();

    public static void ClearExplicitHide(this Primitive? primitive)
        => primitive?.Base?.netIdentity?.ClearExplicitHide();

    public static void ClearAllExplicit(this Primitive? primitive)
        => primitive?.Base?.netIdentity?.ClearAllExplicit();

    public static void ApplyShowState(this Primitive? primitive)
        => primitive?.Base?.netIdentity?.ApplyShowState();

    /// <summary>Primitive を安全に破棄する。</summary>
    public static void SafeDestroy(this Primitive? primitive)
    {
        if (primitive?.Base == null) return;
        primitive.RemoveShowState();
        try { NetworkServer.Destroy(primitive.Base.gameObject); }
        catch (Exception ex) { Log.Warn($"[NetworkVisibility] SafeDestroy(Primitive) 失敗: {ex.Message}"); }
    }

    /// <summary>Primitive をプレイヤーの Transform に追従させる。</summary>
    public static void AttachToPlayer(this Primitive? primitive, Player? player, Vector3 localOffset)
    {
        if (primitive?.Base == null || player?.Transform == null) return;
        try
        {
            var t = primitive.Base.gameObject.transform;
            t.SetParent(player.Transform);
            t.localPosition = localOffset;
            t.localRotation = Quaternion.identity;
        }
        catch (Exception ex) { Log.Warn($"[NetworkVisibility] AttachToPlayer 失敗: {ex.Message}"); }
    }

    // =========================================================
    // Light 向け糖衣構文
    // =========================================================

    public static void InitShowState(this Light? light, NetworkShowState? initialState = null)
        => light?.Base?.netIdentity?.InitShowState(initialState);

    public static void RemoveShowState(this Light? light)
        => light?.Base?.netIdentity?.RemoveShowState();

    public static void UpdateShowState(this Light? light, NetworkShowState newState)
        => light?.Base?.netIdentity?.UpdateShowState(newState);

    public static void UpdateShowState(this Light? light, Action<NetworkShowState> mutate)
        => light?.Base?.netIdentity?.UpdateShowState(mutate);

    public static void SetOwner(this Light? light, Player owner, bool showToOwner = true)
        => light?.Base?.netIdentity?.SetOwner(owner, showToOwner);

    public static void ClearOwner(this Light? light)
        => light?.Base?.netIdentity?.ClearOwner();

    public static void SetShowToOwner(this Light? light, bool show)
        => light?.Base?.netIdentity?.SetShowToOwner(show);

    public static void SetSpectatorVisibility(this Light? light, SpectatorVisibility vis)
        => light?.Base?.netIdentity?.SetSpectatorVisibility(vis);

    public static void AddExplicitShow(this Light? light, Player player)
        => light?.Base?.netIdentity?.AddExplicitShow(player);

    public static void AddExplicitHide(this Light? light, Player player)
        => light?.Base?.netIdentity?.AddExplicitHide(player);

    public static void RemoveExplicit(this Light? light, Player player)
        => light?.Base?.netIdentity?.RemoveExplicit(player);

    public static void ClearExplicitShow(this Light? light)
        => light?.Base?.netIdentity?.ClearExplicitShow();

    public static void ClearExplicitHide(this Light? light)
        => light?.Base?.netIdentity?.ClearExplicitHide();

    public static void ClearAllExplicit(this Light? light)
        => light?.Base?.netIdentity?.ClearAllExplicit();

    public static void ApplyShowState(this Light? light)
        => light?.Base?.netIdentity?.ApplyShowState();

    /// <summary>Light を安全に破棄する。</summary>
    public static void SafeDestroy(this Light? light)
    {
        if (light?.Base == null) return;
        light.RemoveShowState();
        try { NetworkServer.Destroy(light.Base.gameObject); }
        catch (Exception ex) { Log.Warn($"[NetworkVisibility] SafeDestroy(Light) 失敗: {ex.Message}"); }
    }

    // =========================================================
    // AdminToyBase 向け糖衣構文
    // =========================================================

    public static void InitShowState(this AdminToyBase? toy, NetworkShowState? initialState = null)
        => toy?.netIdentity?.InitShowState(initialState);

    public static void RemoveShowState(this AdminToyBase? toy)
        => toy?.netIdentity?.RemoveShowState();

    public static void UpdateShowState(this AdminToyBase? toy, NetworkShowState newState)
        => toy?.netIdentity?.UpdateShowState(newState);

    public static void UpdateShowState(this AdminToyBase? toy, Action<NetworkShowState> mutate)
        => toy?.netIdentity?.UpdateShowState(mutate);

    public static void SetOwner(this AdminToyBase? toy, Player owner, bool showToOwner = true)
        => toy?.netIdentity?.SetOwner(owner, showToOwner);

    public static void ClearOwner(this AdminToyBase? toy)
        => toy?.netIdentity?.ClearOwner();

    public static void SetShowToOwner(this AdminToyBase? toy, bool show)
        => toy?.netIdentity?.SetShowToOwner(show);

    public static void SetSpectatorVisibility(this AdminToyBase? toy, SpectatorVisibility vis)
        => toy?.netIdentity?.SetSpectatorVisibility(vis);

    public static void AddExplicitShow(this AdminToyBase? toy, Player player)
        => toy?.netIdentity?.AddExplicitShow(player);

    public static void AddExplicitHide(this AdminToyBase? toy, Player player)
        => toy?.netIdentity?.AddExplicitHide(player);

    public static void RemoveExplicit(this AdminToyBase? toy, Player player)
        => toy?.netIdentity?.RemoveExplicit(player);

    public static void ClearExplicitShow(this AdminToyBase? toy)
        => toy?.netIdentity?.ClearExplicitShow();

    public static void ClearExplicitHide(this AdminToyBase? toy)
        => toy?.netIdentity?.ClearExplicitHide();

    public static void ClearAllExplicit(this AdminToyBase? toy)
        => toy?.netIdentity?.ClearAllExplicit();

    public static void ApplyShowState(this AdminToyBase? toy)
        => toy?.netIdentity?.ApplyShowState();

    public static void SetVisibilityPredicate(
        this AdminToyBase? toy,
        Predicate<Player>? predicate)
        => toy?.netIdentity?.SetVisibilityPredicate(predicate);

    // =========================================================
    // IEnumerable<NetworkIdentity> 向け一括操作
    // =========================================================

    public static void InitShowState(this IEnumerable<NetworkIdentity> identities, NetworkShowState? initialState = null)
    {
        foreach (var identity in identities)
            identity?.InitShowState(initialState);
    }

    public static void RemoveShowState(this IEnumerable<NetworkIdentity> identities)
    {
        foreach (var identity in identities)
            identity?.RemoveShowState();
    }

    public static void UpdateShowState(this IEnumerable<NetworkIdentity> identities, Action<NetworkShowState> mutate)
    {
        foreach (var identity in identities)
            identity?.UpdateShowState(mutate);
    }

    public static void SetOwner(this IEnumerable<NetworkIdentity> identities, Player owner, bool showToOwner = true)
    {
        foreach (var identity in identities)
            identity?.SetOwner(owner, showToOwner);
    }

    public static void SetSpectatorVisibility(this IEnumerable<NetworkIdentity> identities, SpectatorVisibility vis)
    {
        foreach (var identity in identities)
            identity?.SetSpectatorVisibility(vis);
    }

    public static void AddExplicitShow(this IEnumerable<NetworkIdentity> identities, Player player)
    {
        foreach (var identity in identities)
            identity?.AddExplicitShow(player);
    }

    public static void AddExplicitHide(this IEnumerable<NetworkIdentity> identities, Player player)
    {
        foreach (var identity in identities)
            identity?.AddExplicitHide(player);
    }

    public static void ApplyShowState(this IEnumerable<NetworkIdentity> identities)
    {
        foreach (var identity in identities)
            identity?.ApplyShowState();
    }

    // =========================================================
    // ファクトリ
    // =========================================================

    /// <summary>
    /// 指定プレイヤーにだけ見えるブラックアウト Primitive を生成して返す。
    /// 不要になったら SafeDestroy() で破棄すること。
    /// </summary>
    public static Primitive? CreateBlackoutForPlayer(
        Player? owner,
        Vector3 position,
        Vector3? scale = null)
    {
        if (owner?.ReferenceHub == null) return null;

        try
        {
            var blackout = Primitive.Create(
                PrimitiveType.Cube,
                position,
                owner.Rotation.eulerAngles,
                scale ?? Vector3.one * 1.8f,
                true,
                Color.black);

            if (blackout?.Base == null) return null;
            blackout.Collidable = false;

            Timing.CallDelayed(0f, () =>
            {
                if (blackout?.Base == null) return;

                blackout.InitShowState(new NetworkShowState
                {
                    OwnerId             = owner.Id,
                    ShowToOwner         = true,
                    SpectatorVisibility = SpectatorVisibility.Show,
                });
            });

            return blackout;
        }
        catch (Exception ex)
        {
            Log.Error($"[NetworkVisibility] CreateBlackoutForPlayer 失敗: {ex.Message}");
            return null;
        }
    }
}

/// <summary>
/// <see cref="NetworkVisibilityManager"/> の起動と停止を受け持ちます。
/// </summary>
public sealed class NetworkVisibilityManagerLifecycle : Slafight_Plugin_EXILED.API.Core.Features.EventHandlerBase
{
    /// <inheritdoc />
    public override void RegisterEvents() => NetworkVisibilityManager.Register();

    /// <inheritdoc />
    public override void UnregisterEvents() => NetworkVisibilityManager.Unregister();
}
