using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using LabApi.Events.Arguments.PlayerEvents;
using MEC;
using Mirror;
using PlayerRoles.FirstPersonControl;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

using LabPlayerEvents = LabApi.Events.Handlers.PlayerEvents;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// <see cref="PlayerWear"/> の登録と、装着者の透明化への連動を受け持ちます。着せる操作は <see cref="WearExtensions"/> から行います。
/// </summary>
public sealed class WearManager : EventHandlerBase
{
    private const float SyncInterval = 0.5f;

    /// <summary>
    /// Primitive の Collider が作られるのを待つ時間です。間に合わなければ定期同期が拾います。
    /// </summary>
    private const float HitboxBindDelay = 0.1f;

    private static readonly Dictionary<uint, Dictionary<string, PlayerWear>> Wears = [];

    /// <summary>
    /// netId → 隠した装着者の名札です。
    /// </summary>
    private static readonly Dictionary<uint, WearNameplate> Nameplates = [];

    /// <summary>
    /// netId → 透明化で隠している閲覧者です。Mirror の再送信で Show されないよう ShowVeto が参照します。
    /// </summary>
    private static readonly Dictionary<uint, HashSet<int>> HiddenByInvisibility = [];

    private static readonly HashSet<int> PendingViewers = [];
    private static readonly HashSet<int> NoViewers = [];

    /// <summary>
    /// 観戦者まで隠すと観戦先の位置が届かなくなるので、生存者だけから隠します。
    /// </summary>
    private static readonly Func<Player, bool> HidesBodyFrom = viewer => !viewer.IsDead;

    private static CoroutineHandle syncLoop;
    private static bool resyncScheduled;

    /// <inheritdoc />
    public override void RegisterEvents()
    {
        LabPlayerEvents.ChangedRole += OnChangedRole;
        PlayerHandlers.Left += OnLeft;
        PlayerHandlers.Verified += OnVerified;
        PlayerHandlers.Spawned += OnSpawned;
        PlayerHandlers.ReceivingEffect += OnReceivingEffect;
        ServerHandlers.RestartingRound += RemoveAll;

        NetworkVisibilityManager.ShowVeto = IsHiddenByInvisibility;
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        LabPlayerEvents.ChangedRole -= OnChangedRole;
        PlayerHandlers.Left -= OnLeft;
        PlayerHandlers.Verified -= OnVerified;
        PlayerHandlers.Spawned -= OnSpawned;
        PlayerHandlers.ReceivingEffect -= OnReceivingEffect;
        ServerHandlers.RestartingRound -= RemoveAll;

        if (NetworkVisibilityManager.ShowVeto == IsHiddenByInvisibility)
            NetworkVisibilityManager.ShowVeto = null;

        RemoveAll();
    }

    /// <summary>
    /// オブジェクトをプレイヤーに着せます。受け付けられなければ null。
    /// </summary>
    internal static PlayerWear Attach(Player player, GameObject gameObject, Action destroy, WearOptions options)
    {
        if (!player.IsSafePlayer() || gameObject == null || destroy is null) return null;

        uint netId = player.GetNetId();

        if (netId == 0) return null;

        options ??= WearOptions.Default;
        string slot = NormalizeSlot(options.Slot);

        PlayerWear wear = new PlayerWear(player, slot, gameObject, destroy, options);

        // 同じ物の着せ直しでは、今から着せる実体を壊さない。
        if (Wears.TryGetValue(netId, out Dictionary<string, PlayerWear> slots) &&
            slots.TryGetValue(slot, out PlayerWear previous))
        {
            Forget(previous, destroyObject: previous.GameObject != gameObject);
        }

        foreach (WearFollower stale in gameObject.GetComponents<WearFollower>())
        {
            UnityEngine.Object.Destroy(stale);
        }

        // Forget が空の辞書を外すので、ここで取り直す。
        if (!Wears.TryGetValue(netId, out slots))
            Wears[netId] = slots = new Dictionary<string, PlayerWear>(StringComparer.OrdinalIgnoreCase);

        slots[slot] = wear;

        wear.Follow();
        wear.RefreshVisibility();
        wear.BindHitboxes();
        SyncOwner(wear);

        // 役職解除・退出・ラウンド再開はスコープの閉鎖でまとめて拾う。
        PlayerScope.Of(player).OnDispose(_ => wear.Remove());
        EnsureSyncLoop();

        return wear;
    }

    /// <summary>
    /// 指定スロットの装着物を取得します。
    /// </summary>
    internal static bool TryGet(Player player, string slot, out PlayerWear wear)
    {
        wear = null;

        if (player is null || !Wears.TryGetValue(player.GetNetId(), out Dictionary<string, PlayerWear> slots))
            return false;

        return slots.TryGetValue(NormalizeSlot(slot), out wear) && wear.IsWorn;
    }

    /// <summary>
    /// このプレイヤーの装着物のスナップショットです。
    /// </summary>
    internal static IReadOnlyList<PlayerWear> GetAll(Player player)
    {
        if (player is null || !Wears.TryGetValue(player.GetNetId(), out Dictionary<string, PlayerWear> slots))
            return [];

        return slots.Values.Where(wear => wear.IsWorn).ToList();
    }

    /// <summary>
    /// 装着物を 1 つ外して破棄します。二重に呼んでも安全です。
    /// </summary>
    internal static void Remove(PlayerWear wear)
    {
        if (wear is null) return;

        if (Wears.TryGetValue(wear.OwnerNetId, out Dictionary<string, PlayerWear> slots) &&
            slots.TryGetValue(wear.Slot, out PlayerWear current) &&
            ReferenceEquals(current, wear))
        {
            Forget(wear, destroyObject: true);
        }
    }

    /// <summary>
    /// このプレイヤーの装着物をすべて外して破棄します。
    /// </summary>
    internal static void RemoveAll(Player player)
    {
        foreach (PlayerWear wear in GetAll(player))
        {
            wear.Remove();
        }
    }

    /// <summary>
    /// 装着者の透明化状態を、その装着物すべてへ反映します。
    /// </summary>
    internal static void SyncOwner(PlayerWear wear)
    {
        if (wear.Owner.IsSafePlayer() && wear.Owner.GetNetId() == wear.OwnerNetId)
            SyncOwner(wear.Owner);
    }

    /// <summary>
    /// 当たり判定の追加を反映します。Primitive の Collider を待つので、少し後にもう一度見ます。
    /// </summary>
    internal static void QueueHitboxSync(PlayerWear wear)
    {
        SyncOwner(wear);

        RoundScope.Current.Delay(HitboxBindDelay, () =>
        {
            if (wear.IsWorn)
                SyncOwner(wear);
        });
    }

    /// <summary>
    /// <see cref="PlayerWear.ReplacesBody"/> の変更を反映します。
    /// </summary>
    internal static void SyncBodyReplacement(PlayerWear wear)
    {
        SyncOwner(wear);

        if (!wear.ReplacesBody)
            RestoreBody(wear);
    }

    /// <summary>
    /// このプレイヤーの名札の NPC です。無ければ null。
    /// </summary>
    internal static Npc GetNameplateNpc(Player player) =>
        player is not null && Nameplates.TryGetValue(player.GetNetId(), out WearNameplate nameplate) ? nameplate.Npc : null;

    /// <summary>
    /// 発砲の前後で、撃った本人のモデルの当たり判定を切り替えます。
    /// </summary>
    internal static void SetOwnShotHitboxes(uint netId, bool restore)
    {
        if (!Wears.TryGetValue(netId, out Dictionary<string, PlayerWear> slots)) return;

        bool replacesBody = false;

        foreach (PlayerWear wear in slots.Values)
        {
            wear.SetHitboxCollidersEnabled(restore);
            replacesBody |= wear.ReplacesBody;
        }

        // バニラの復元で素体の当たり判定が戻るので、切り直す。
        if (replacesBody && restore && Player.Get(netId) is { } owner)
            SetBodyHitboxes(owner, false);
    }

    private static string NormalizeSlot(string slot) =>
        string.IsNullOrWhiteSpace(slot) ? WearOptions.DefaultSlot : slot.Trim();

    private static void Forget(PlayerWear wear, bool destroyObject)
    {
        if (Wears.TryGetValue(wear.OwnerNetId, out Dictionary<string, PlayerWear> slots))
        {
            slots.Remove(wear.Slot);

            if (slots.Count == 0)
                Wears.Remove(wear.OwnerNetId);
        }

        foreach (uint netId in wear.HiddenNetIds)
        {
            HiddenByInvisibility.Remove(netId);
        }

        wear.HiddenNetIds.Clear();

        try
        {
            if (destroyObject)
                wear.Destroy();
            else
                wear.Release();

            if (wear.ReplacesBody)
                RestoreBody(wear);
        }
        catch (Exception exception)
        {
            Log.Warn($"[Slafight] 装着物の破棄に失敗しました: {exception}");
        }
    }

    /// <summary>
    /// 体の代わりが居なくなったら素体を戻します。
    /// </summary>
    private static void RestoreBody(PlayerWear wear)
    {
        if (Wears.TryGetValue(wear.OwnerNetId, out Dictionary<string, PlayerWear> slots) &&
            slots.Values.Any(other => other.ReplacesBody))
            return;

        if (Nameplates.TryGetValue(wear.OwnerNetId, out WearNameplate nameplate))
        {
            Nameplates.Remove(wear.OwnerNetId);
            nameplate.Dispose();
        }

        Player owner = wear.Owner;

        if (!owner.IsSafePlayer() || owner.GetNetId() != wear.OwnerNetId) return;

        PlayerVisibilitySyncProvider.ShowToAll(owner);
        SetBodyHitboxes(owner, true);
    }

    /// <summary>
    /// サーバー側の素体の当たり判定を切り替えます。クライアント側の判定と名札には影響しません。
    /// </summary>
    internal static void SetBodyHitboxes(Player owner, bool enabled)
    {
        if (owner.ReferenceHub.roleManager.CurrentRole is not IFpcRole role) return;

        foreach (HitboxIdentity hitbox in role.FpcModule.CharacterModelInstance.Hitboxes)
        {
            hitbox.SetColliders(enabled);
        }
    }

    private static void RemoveAll()
    {
        foreach (PlayerWear wear in Wears.Values.SelectMany(slots => slots.Values).ToList())
        {
            Forget(wear, destroyObject: true);
        }

        foreach (WearNameplate nameplate in Nameplates.Values)
        {
            nameplate.Dispose();
        }

        Nameplates.Clear();
        Wears.Clear();
        HiddenByInvisibility.Clear();
        PendingViewers.Clear();
        resyncScheduled = false;
    }

    private static void EnsureSyncLoop()
    {
        // IsInvisibleFor の増減にはイベントが無いので、装着中だけ定期的に見る。
        if (!syncLoop.IsRunning)
            syncLoop = RoundScope.Current.RunLoop(SyncInterval, SyncAll);
    }

    private static void SyncAll()
    {
        foreach (KeyValuePair<uint, Dictionary<string, PlayerWear>> entry in Wears.ToList())
        {
            // 外から破棄された Schematic の枠を空ける。
            foreach (PlayerWear stale in entry.Value.Values.Where(wear => wear.GameObject == null).ToList())
            {
                Forget(stale, destroyObject: false);
            }

            if (Player.Get(entry.Key) is { } owner)
                SyncOwner(owner);
        }
    }

    private static void SyncOwner(Player owner)
    {
        if (!Wears.TryGetValue(owner.GetNetId(), out Dictionary<string, PlayerWear> slots) || slots.Count == 0)
            return;

        SyncBody(owner, slots);

        bool invisibleForAll = IsInvisibleForAll(owner);
        HashSet<int> invisibleFor = GetInvisibleFor(owner);

        foreach (PlayerWear wear in slots.Values.ToList())
        {
            SyncWear(owner, wear, invisibleForAll, invisibleFor);
        }
    }

    /// <summary>
    /// 体の代わりと当たり判定の状態を保ちます。バニラの再設定や途中参加もここで拾います。
    /// </summary>
    private static void SyncBody(Player owner, Dictionary<string, PlayerWear> slots)
    {
        bool replacesBody = false;

        foreach (PlayerWear wear in slots.Values)
        {
            wear.SyncHitboxes();
            replacesBody |= wear.ReplacesBody;
        }

        if (!replacesBody) return;

        PlayerVisibilitySyncProvider.SetHiddenRule(owner, HidesBodyFrom);
        SetBodyHitboxes(owner, false);

        uint netId = owner.GetNetId();

        if (!Nameplates.TryGetValue(netId, out WearNameplate nameplate) || !nameplate.IsAlive)
        {
            nameplate?.Dispose();
            Nameplates[netId] = nameplate = new WearNameplate(owner);
        }

        nameplate.Sync();
    }

    private static void SyncWear(Player owner, PlayerWear wear, bool invisibleForAll, HashSet<int> invisibleFor)
    {
        bool shouldHide = !wear.IgnoreInvisibility && (invisibleForAll || invisibleFor is { Count: > 0 });

        // 隠す理由も隠しているものも無いのが通常時。ここで返す。
        if (!shouldHide && wear.HiddenNetIds.Count == 0) return;

        List<NetworkIdentity> identities = wear.ResolveIdentities();
        HashSet<uint> liveNetIds = new HashSet<uint>(identities.Select(identity => identity.netId));

        foreach (uint staleNetId in wear.HiddenNetIds.Where(netId => !liveNetIds.Contains(netId)).ToList())
        {
            HiddenByInvisibility.Remove(staleNetId);
            wear.HiddenNetIds.Remove(staleNetId);
        }

        // 本人の視界はバニラの SCP-268 と同じく変えない。
        HashSet<int> desired = NoViewers;

        if (shouldHide)
        {
            desired = [];

            foreach (Player viewer in Player.List)
            {
                if (viewer.Id != owner.Id && (invisibleForAll || invisibleFor.Contains(viewer.Id)))
                    desired.Add(viewer.Id);
            }
        }

        foreach (NetworkIdentity identity in identities)
        {
            ApplyHidden(wear, identity, desired);
        }
    }

    private static void ApplyHidden(PlayerWear wear, NetworkIdentity identity, HashSet<int> desired)
    {
        uint netId = identity.netId;
        bool hadPrevious = HiddenByInvisibility.TryGetValue(netId, out HashSet<int> previous);

        if (!hadPrevious && desired.Count == 0) return;

        previous ??= NoViewers;

        // 自分の Show が ShowVeto に潰されないよう、送信より先に更新する。
        if (desired.Count > 0)
        {
            HiddenByInvisibility[netId] = new HashSet<int>(desired);
            wear.HiddenNetIds.Add(netId);
        }
        else
        {
            HiddenByInvisibility.Remove(netId);
            wear.HiddenNetIds.Remove(netId);
        }

        foreach (int viewerId in desired.Where(id => !previous.Contains(id)))
        {
            Player.Get(viewerId)?.HideNetworkIdentity(identity);
        }

        foreach (int viewerId in previous.Where(id => !desired.Contains(id)))
        {
            if (Player.Get(viewerId) is { IsConnected: true } viewer)
                RestoreVisibility(identity, viewer);
        }
    }

    /// <summary>
    /// 透明化解除時の再表示です。表示条件があるならそちらを優先します。
    /// </summary>
    private static void RestoreVisibility(NetworkIdentity identity, Player viewer)
    {
        NetworkShowState state = identity.GetShowState();

        if (state is null || state.ShouldShow(viewer, Player.List))
            viewer.ShowNetworkIdentity(identity);
        else
            viewer.HideNetworkIdentity(identity);
    }

    private static bool IsInvisibleForAll(Player owner) =>
        owner.IsEffectActive<Invisible>() || owner.Role is FpcRole { IsInvisible: true };

    private static HashSet<int> GetInvisibleFor(Player owner)
    {
        if (owner.Role is not FpcRole role || role.IsInvisibleFor.Count == 0) return null;

        return new HashSet<int>(role.IsInvisibleFor.Where(viewer => viewer?.ReferenceHub != null).Select(viewer => viewer.Id));
    }

    private static bool IsHiddenByInvisibility(NetworkIdentity identity, Player viewer) =>
        identity != null &&
        viewer != null &&
        HiddenByInvisibility.TryGetValue(identity.netId, out HashSet<int> hidden) &&
        hidden.Contains(viewer.Id);

    /// <summary>
    /// Mirror が全オブジェクトを送り直した閲覧者へ、次のフレームで Hide を送り直します。
    /// </summary>
    private static void QueueViewerResync(Player viewer)
    {
        if (viewer is null || HiddenByInvisibility.Count == 0) return;

        PendingViewers.Add(viewer.Id);

        if (resyncScheduled) return;

        resyncScheduled = true;
        RoundScope.Current.Delay(0f, FlushViewerResync);
    }

    private static void FlushViewerResync()
    {
        resyncScheduled = false;

        foreach (HashSet<int> hidden in HiddenByInvisibility.Values)
        {
            hidden.ExceptWith(PendingViewers);
        }

        PendingViewers.Clear();
        SyncAll();
    }

    private static void OnChangedRole(PlayerChangedRoleEventArgs ev)
    {
        if (ev.Player?.ReferenceHub is not { } hub || Player.Get(hub) is not { } player) return;

        // 演出の一時差し替えでは外さない。
        if (RoleHold.IsSwapping(player)) return;

        foreach (PlayerWear wear in GetAll(player))
        {
            if (wear.Role != ev.NewRole.RoleTypeId)
                wear.Remove();
        }
    }

    private static void OnLeft(LeftEventArgs ev)
    {
        foreach (HashSet<int> hidden in HiddenByInvisibility.Values)
        {
            hidden.Remove(ev.Player.Id);
        }
    }

    private static void OnVerified(VerifiedEventArgs ev) => QueueViewerResync(ev.Player);

    private static void OnSpawned(Exiled.Events.EventArgs.Player.SpawnedEventArgs ev) => QueueViewerResync(ev.Player);

    private static void OnReceivingEffect(ReceivingEffectEventArgs ev)
    {
        if (ev.Effect is not Invisible || !Wears.ContainsKey(ev.Player.GetNetId())) return;

        Player player = ev.Player;
        uint netId = player.GetNetId();

        // 適用前に発火するので、確定後に見る。
        RoundScope.Current.Delay(0f, () =>
        {
            if (player.IsSafePlayer() && player.GetNetId() == netId)
                SyncOwner(player);
        });
    }
}
