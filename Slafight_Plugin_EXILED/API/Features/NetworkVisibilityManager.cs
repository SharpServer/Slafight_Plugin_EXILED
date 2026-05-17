using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using Exiled.Events.EventArgs.Player;
using MEC;
using Mirror;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

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
/// 1つの NetworkIdentity に紐づく表示状態を保持するクラス。
/// 外部から直接変更後、<see cref="NetworkVisibilityManager.ApplyShowState"/> を呼ぶか、
/// UpdateShowState 系 API 経由で変更すること。
/// </summary>
public class NetworkShowState
{
    // ---- 所有者 ----

    /// <summary>所有者のプレイヤーID。null の場合は所有者なし。</summary>
    public int? OwnerId { get; set; } = null;

    /// <summary>所有者本人に表示するか。</summary>
    public bool ShowToOwner { get; set; } = true;

    /// <summary>所有者の観戦者に対する表示方針。デフォルトは Show。</summary>
    public SpectatorVisibility SpectatorVisibility { get; set; } = SpectatorVisibility.Show;

    // ---- 個別指定 ----

    /// <summary>強制的に表示するプレイヤーIDのセット。ExplicitHide より低優先。</summary>
    public HashSet<int> ExplicitShow { get; } = new();

    /// <summary>強制的に非表示にするプレイヤーIDのセット。最高優先。</summary>
    public HashSet<int> ExplicitHide { get; } = new();

    // ---- 優先順位に従って表示すべきか判定 ----

    /// <summary>
    /// 指定プレイヤーがこのオブジェクトを見るべきかを返す。<br/>
    /// 優先順位：ExplicitHide > ExplicitShow > ShowToOwner > SpectatorVisibility > デフォルト Hide
    /// </summary>
    /// <param name="player">判定対象のプレイヤー。</param>
    /// <param name="allPlayers">全プレイヤーリスト（未使用だが拡張用に保持）。</param>
    /// <param name="overrideIsSpectatingOwner">
    /// null のとき実際の観戦状態を参照する。
    /// ChangingSpectatedPlayer イベントで「変更後の状態」を先取りしたいときに渡す。
    /// </param>
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

        // 3. 所有者本人
        if (OwnerId.HasValue && player.Id == OwnerId.Value) return ShowToOwner;

        // 4. 観戦者
        if (OwnerId.HasValue)
        {
            bool isSpectatingOwner = overrideIsSpectatingOwner ??
                (!player.IsAlive &&
                 player.CurrentSpectatingPlayers.Any(s => s?.Id == OwnerId.Value));

            if (isSpectatingOwner)
                return SpectatorVisibility == SpectatorVisibility.Show;
        }

        // 5. デフォルト Hide
        return false;
    }
}

// =========================================================
// NetworkVisibilityExtensions
// =========================================================

/// <summary>
/// NetworkIdentity を持つ任意のオブジェクト（Primitive / Light / AdminToy / Schematic等）の
/// 表示を特定プレイヤーだけに限定するヘルパー群。
/// PublicizedされたMirrorを用いて実装。UnsafeBlockをAllowに設定すること。
/// </summary>
public static class NetworkVisibilityManager
{
    // =========================================================
    // 内部ストア
    // =========================================================

    private static readonly Dictionary<uint, NetworkShowState>   _states         = new();
    private static readonly Dictionary<uint, NetworkIdentity>    _identityCache  = new();

    // =========================================================
    // Register / Unregister
    // =========================================================

    public static void Register()
    {
        Exiled.Events.Handlers.Server.RoundStarted            += OnRoundStarted;
        Exiled.Events.Handlers.Player.Verified                += OnVerified;
        Exiled.Events.Handlers.Player.Spawned                 += OnSpawned;
        Exiled.Events.Handlers.Player.ChangingSpectatedPlayer += OnChangingSpectatedPlayer;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.RoundStarted            -= OnRoundStarted;
        Exiled.Events.Handlers.Player.Verified                -= OnVerified;
        Exiled.Events.Handlers.Player.Spawned                 -= OnSpawned;
        Exiled.Events.Handlers.Player.ChangingSpectatedPlayer -= OnChangingSpectatedPlayer;
        _states.Clear();
        _identityCache.Clear();
    }

    // =========================================================
    // イベントハンドラ
    // =========================================================

    private static void OnRoundStarted()
    {
        _states.Clear();
        _identityCache.Clear();
    }

    private static void OnVerified(VerifiedEventArgs ev)
    {
        if (ev?.Player == null) return;

        foreach (var (netId, identity) in _identityCache)
        {
            if (identity == null) continue;
            if (!_states.TryGetValue(netId, out var state)) continue;
            SendVisibility(identity, ev.Player, state.ShouldShow(ev.Player, Player.List));
        }
    }

    /// <summary>
    /// スポーン完了後に呼ばれる。ロール変更後に Mirror が全 NetworkIdentity を
    /// 再送信するため、管理オブジェクトの表示状態を上書き補正する。
    /// 観戦者が生者としてスポーンした場合も同様にリセットが必要。
    /// </summary>
    private static void OnSpawned(SpawnedEventArgs ev)
    {
        if (ev?.Player == null) return;

        // スポーン直後は Mirror の再送信が走るため、1フレーム後に補正する
        Timing.CallDelayed(0f, () =>
        {
            if (ev.Player == null || !ev.Player.IsConnected) return;

            foreach (var (netId, identity) in _identityCache)
            {
                if (identity == null) continue;
                if (!_states.TryGetValue(netId, out var state)) continue;
                SendVisibility(identity, ev.Player, state.ShouldShow(ev.Player, Player.List));
            }
        });
    }

    private static void OnChangingSpectatedPlayer(ChangingSpectatedPlayerEventArgs ev)
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
    public static void ShowNetworkIdentity(this Player player, NetworkIdentity identity)
    {
        if (player?.Connection == null || identity == null) return;
        try { NetworkServer.ShowForConnection(identity, player.Connection); }
        catch (Exception ex) { Log.Warn($"[NetworkVisibility] ShowNetworkIdentity 失敗: {ex.Message}"); }
    }

    /// <summary>指定プレイヤーからオブジェクトを非表示にする。</summary>
    public static void HideNetworkIdentity(this Player player, NetworkIdentity identity)
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

    public static NetworkShowState? GetShowState(this Primitive primitive)
        => primitive?.Base?.netIdentity?.GetShowState();

    public static NetworkShowState? GetShowState(this Light light)
        => light?.Base?.netIdentity?.GetShowState();

    public static NetworkShowState? GetShowState(this AdminToys.AdminToyBase toy)
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
    public static void UpdateShowState(this NetworkIdentity identity, NetworkShowState newState)
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
    public static void UpdateShowState(this NetworkIdentity identity, Action<NetworkShowState> mutate)
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
    public static void SetOwner(this NetworkIdentity identity, Player owner, bool showToOwner = true)
        => identity?.UpdateShowState(s =>
        {
            s.OwnerId    = owner?.Id;
            s.ShowToOwner = showToOwner;
        });

    /// <summary>所有者を解除する。</summary>
    public static void ClearOwner(this NetworkIdentity identity)
        => identity?.UpdateShowState(s => s.OwnerId = null);

    /// <summary>所有者本人への表示を切り替える。</summary>
    public static void SetShowToOwner(this NetworkIdentity identity, bool show)
        => identity?.UpdateShowState(s => s.ShowToOwner = show);

    // =========================================================
    // SpectatorVisibility 系 API
    // =========================================================

    /// <summary>観戦者への表示方針を変更する。</summary>
    public static void SetSpectatorVisibility(this NetworkIdentity identity, SpectatorVisibility vis)
        => identity?.UpdateShowState(s => s.SpectatorVisibility = vis);

    // =========================================================
    // ExplicitShow / ExplicitHide 系 API
    // =========================================================

    /// <summary>プレイヤーを強制表示リストに追加する。</summary>
    public static void AddExplicitShow(this NetworkIdentity identity, Player player)
    {
        if (player == null) return;
        identity?.UpdateShowState(s =>
        {
            s.ExplicitHide.Remove(player.Id); // 競合解消
            s.ExplicitShow.Add(player.Id);
        });
    }

    /// <summary>プレイヤーを強制非表示リストに追加する。</summary>
    public static void AddExplicitHide(this NetworkIdentity identity, Player player)
    {
        if (player == null) return;
        identity?.UpdateShowState(s =>
        {
            s.ExplicitShow.Remove(player.Id); // 競合解消
            s.ExplicitHide.Add(player.Id);
        });
    }

    /// <summary>プレイヤーを ExplicitShow/Hide 両方から除去する。</summary>
    public static void RemoveExplicit(this NetworkIdentity identity, Player player)
    {
        if (player == null) return;
        identity?.UpdateShowState(s =>
        {
            s.ExplicitShow.Remove(player.Id);
            s.ExplicitHide.Remove(player.Id);
        });
    }

    /// <summary>ExplicitShow リストを全クリアする。</summary>
    public static void ClearExplicitShow(this NetworkIdentity identity)
        => identity?.UpdateShowState(s => s.ExplicitShow.Clear());

    /// <summary>ExplicitHide リストを全クリアする。</summary>
    public static void ClearExplicitHide(this NetworkIdentity identity)
        => identity?.UpdateShowState(s => s.ExplicitHide.Clear());

    /// <summary>ExplicitShow / ExplicitHide 両方を全クリアする。</summary>
    public static void ClearAllExplicit(this NetworkIdentity identity)
        => identity?.UpdateShowState(s =>
        {
            s.ExplicitShow.Clear();
            s.ExplicitHide.Clear();
        });

    // =========================================================
    // Player 拡張（糖衣構文）
    // =========================================================

    /// <summary>このプレイヤーを identity の強制表示リストに追加する。</summary>
    public static void AddExplicitShow(this Player player, NetworkIdentity identity)
        => identity?.AddExplicitShow(player);

    /// <summary>このプレイヤーを identity の強制非表示リストに追加する。</summary>
    public static void AddExplicitHide(this Player player, NetworkIdentity identity)
        => identity?.AddExplicitHide(player);

    /// <summary>このプレイヤーを identity の Explicit 両リストから除去する。</summary>
    public static void RemoveExplicit(this Player player, NetworkIdentity identity)
        => identity?.RemoveExplicit(player);

    // =========================================================
    // Primitive 向け糖衣構文
    // =========================================================

    public static void InitShowState(this Primitive primitive, NetworkShowState? initialState = null)
        => primitive?.Base?.netIdentity?.InitShowState(initialState);

    public static void RemoveShowState(this Primitive primitive)
        => primitive?.Base?.netIdentity?.RemoveShowState();

    public static void UpdateShowState(this Primitive primitive, NetworkShowState newState)
        => primitive?.Base?.netIdentity?.UpdateShowState(newState);

    public static void UpdateShowState(this Primitive primitive, Action<NetworkShowState> mutate)
        => primitive?.Base?.netIdentity?.UpdateShowState(mutate);

    public static void SetOwner(this Primitive primitive, Player owner, bool showToOwner = true)
        => primitive?.Base?.netIdentity?.SetOwner(owner, showToOwner);

    public static void ClearOwner(this Primitive primitive)
        => primitive?.Base?.netIdentity?.ClearOwner();

    public static void SetShowToOwner(this Primitive primitive, bool show)
        => primitive?.Base?.netIdentity?.SetShowToOwner(show);

    public static void SetSpectatorVisibility(this Primitive primitive, SpectatorVisibility vis)
        => primitive?.Base?.netIdentity?.SetSpectatorVisibility(vis);

    public static void AddExplicitShow(this Primitive primitive, Player player)
        => primitive?.Base?.netIdentity?.AddExplicitShow(player);

    public static void AddExplicitHide(this Primitive primitive, Player player)
        => primitive?.Base?.netIdentity?.AddExplicitHide(player);

    public static void RemoveExplicit(this Primitive primitive, Player player)
        => primitive?.Base?.netIdentity?.RemoveExplicit(player);

    public static void ClearExplicitShow(this Primitive primitive)
        => primitive?.Base?.netIdentity?.ClearExplicitShow();

    public static void ClearExplicitHide(this Primitive primitive)
        => primitive?.Base?.netIdentity?.ClearExplicitHide();

    public static void ClearAllExplicit(this Primitive primitive)
        => primitive?.Base?.netIdentity?.ClearAllExplicit();

    public static void ApplyShowState(this Primitive primitive)
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
    public static void AttachToPlayer(this Primitive primitive, Player player, Vector3 localOffset)
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

    public static void InitShowState(this Light light, NetworkShowState? initialState = null)
        => light?.Base?.netIdentity?.InitShowState(initialState);

    public static void RemoveShowState(this Light light)
        => light?.Base?.netIdentity?.RemoveShowState();

    public static void UpdateShowState(this Light light, NetworkShowState newState)
        => light?.Base?.netIdentity?.UpdateShowState(newState);

    public static void UpdateShowState(this Light light, Action<NetworkShowState> mutate)
        => light?.Base?.netIdentity?.UpdateShowState(mutate);

    public static void SetOwner(this Light light, Player owner, bool showToOwner = true)
        => light?.Base?.netIdentity?.SetOwner(owner, showToOwner);

    public static void ClearOwner(this Light light)
        => light?.Base?.netIdentity?.ClearOwner();

    public static void SetShowToOwner(this Light light, bool show)
        => light?.Base?.netIdentity?.SetShowToOwner(show);

    public static void SetSpectatorVisibility(this Light light, SpectatorVisibility vis)
        => light?.Base?.netIdentity?.SetSpectatorVisibility(vis);

    public static void AddExplicitShow(this Light light, Player player)
        => light?.Base?.netIdentity?.AddExplicitShow(player);

    public static void AddExplicitHide(this Light light, Player player)
        => light?.Base?.netIdentity?.AddExplicitHide(player);

    public static void RemoveExplicit(this Light light, Player player)
        => light?.Base?.netIdentity?.RemoveExplicit(player);

    public static void ClearExplicitShow(this Light light)
        => light?.Base?.netIdentity?.ClearExplicitShow();

    public static void ClearExplicitHide(this Light light)
        => light?.Base?.netIdentity?.ClearExplicitHide();

    public static void ClearAllExplicit(this Light light)
        => light?.Base?.netIdentity?.ClearAllExplicit();

    public static void ApplyShowState(this Light light)
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

    public static void InitShowState(this AdminToys.AdminToyBase toy, NetworkShowState? initialState = null)
        => toy?.netIdentity?.InitShowState(initialState);

    public static void RemoveShowState(this AdminToys.AdminToyBase toy)
        => toy?.netIdentity?.RemoveShowState();

    public static void UpdateShowState(this AdminToys.AdminToyBase toy, NetworkShowState newState)
        => toy?.netIdentity?.UpdateShowState(newState);

    public static void UpdateShowState(this AdminToys.AdminToyBase toy, Action<NetworkShowState> mutate)
        => toy?.netIdentity?.UpdateShowState(mutate);

    public static void SetOwner(this AdminToys.AdminToyBase toy, Player owner, bool showToOwner = true)
        => toy?.netIdentity?.SetOwner(owner, showToOwner);

    public static void ClearOwner(this AdminToys.AdminToyBase toy)
        => toy?.netIdentity?.ClearOwner();

    public static void SetShowToOwner(this AdminToys.AdminToyBase toy, bool show)
        => toy?.netIdentity?.SetShowToOwner(show);

    public static void SetSpectatorVisibility(this AdminToys.AdminToyBase toy, SpectatorVisibility vis)
        => toy?.netIdentity?.SetSpectatorVisibility(vis);

    public static void AddExplicitShow(this AdminToys.AdminToyBase toy, Player player)
        => toy?.netIdentity?.AddExplicitShow(player);

    public static void AddExplicitHide(this AdminToys.AdminToyBase toy, Player player)
        => toy?.netIdentity?.AddExplicitHide(player);

    public static void RemoveExplicit(this AdminToys.AdminToyBase toy, Player player)
        => toy?.netIdentity?.RemoveExplicit(player);

    public static void ClearExplicitShow(this AdminToys.AdminToyBase toy)
        => toy?.netIdentity?.ClearExplicitShow();

    public static void ClearExplicitHide(this AdminToys.AdminToyBase toy)
        => toy?.netIdentity?.ClearExplicitHide();

    public static void ClearAllExplicit(this AdminToys.AdminToyBase toy)
        => toy?.netIdentity?.ClearAllExplicit();

    public static void ApplyShowState(this AdminToys.AdminToyBase toy)
        => toy?.netIdentity?.ApplyShowState();

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
        Player owner,
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
