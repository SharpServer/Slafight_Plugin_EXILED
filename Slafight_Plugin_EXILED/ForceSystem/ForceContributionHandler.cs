using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp914;
using Respawning;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.Handlers;
using UnityEngine;

using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;
using Scp914Handlers = Exiled.Events.Handlers.Scp914;

namespace Slafight_Plugin_EXILED.ForceSystem;

/// <summary>
/// 貢献度の加点・減点を拾います。重みは隊 (<see cref="ForceBase.MembershipImpact"/> など) に聞きます。
/// </summary>
public sealed class ForceContributionHandler : EventHandlerBase
{
    /// <summary>継続所属を評価する間隔 (秒)。</summary>
    private const float MembershipInterval = 10f;

    /// <summary>この秒数ぶん喋り続けたら 1 回ぶんの加点にします。</summary>
    private const float CommunicationWindow = 20f;

    /// <summary>1 回の窓で加点できる上限。スパムで稼がせないための蓋です。</summary>
    private const int CommunicationCapPerWindow = 1;

    /// <summary>同士討ちを「意図的」と見なす、敵が居ない距離 (m)。</summary>
    private const float NoEnemyRadius = 30f;

    /// <summary>草案の「部隊内の全部隊の 40% が集まっている場所」の割合。</summary>
    private const float GatheredRatio = 0.40f;

    /// <summary>集結していると見なす距離 (m)。</summary>
    private const float GatheredRadius = 15f;

    /// <summary>貢献 1 点あたり、次の波を早める秒数。</summary>
    private const float WaveSecondsPerPoint = -0.5f;

    private static readonly float NoEnemyRadiusSqr = NoEnemyRadius * NoEnemyRadius;
    private static readonly float GatheredRadiusSqr = GatheredRadius * GatheredRadius;

    /// <summary>直近に加点した時刻です。窓ごとに 1 回だけ加点するために持ちます。</summary>
    private static readonly Dictionary<uint, float> LastCommunicationReward = new();

    /// <inheritdoc />
    public override void RegisterEvents()
    {
        PlayerHandlers.Hurting += OnHurting;
        PlayerHandlers.VoiceChatting += OnVoiceChatting;
        PlayerHandlers.PickingUpItem += OnPickingUpItem;
        Scp914Handlers.UpgradingInventoryItem += OnUpgradingInventoryItem;
        Scp914Handlers.UpgradingPickup += OnUpgradingPickup;
        PlayerHandlers.ThrownProjectile += OnThrownProjectile;
        EscapeHandler.Escaped += OnEscaped;
        PlayerHandlers.ActivatingGenerator += OnActivatingGenerator;
        PlayerHandlers.Died += OnDied;

        ServerHandlers.RoundStarted += OnRoundStarted;
    }

    /// <summary>
    /// 継続所属の評価ループをラウンドごとに <see cref="RoundScope"/> へ載せます。
    /// </summary>
    private static void OnRoundStarted()
    {
        LastCommunicationReward.Clear();

        RoundScope.Current.RunLoop(MembershipInterval, RewardMembership);
        RoundScope.Current.OnEnd(LastCommunicationReward.Clear);
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        PlayerHandlers.Hurting -= OnHurting;
        PlayerHandlers.VoiceChatting -= OnVoiceChatting;
        PlayerHandlers.PickingUpItem -= OnPickingUpItem;
        Scp914Handlers.UpgradingInventoryItem -= OnUpgradingInventoryItem;
        Scp914Handlers.UpgradingPickup -= OnUpgradingPickup;
        PlayerHandlers.ThrownProjectile -= OnThrownProjectile;
        EscapeHandler.Escaped -= OnEscaped;
        PlayerHandlers.ActivatingGenerator -= OnActivatingGenerator;
        PlayerHandlers.Died -= OnDied;
        ServerHandlers.RoundStarted -= OnRoundStarted;

        LastCommunicationReward.Clear();
    }

    // ───────────────────────────────
    // 加点
    // ───────────────────────────────

    /// <summary>
    /// 隊に居続けている隊員に加点します。
    /// </summary>
    private static void RewardMembership()
    {
        foreach (ForceBase force in ForceRegistry.All.ToArray())
        {
            foreach (ForceMember member in force.Members.ToArray())
            {
                if (!member.IsAlive || !member.Player.IsAlive) continue;

                ForceContribution.Reward(member, force.MembershipImpact);
            }
        }
    }

    /// <summary>
    /// 喋っている隊員に加点します。<see cref="CommunicationWindow"/> 秒に <see cref="CommunicationCapPerWindow"/> 回までです。
    /// </summary>
    private static void OnVoiceChatting(VoiceChattingEventArgs ev)
    {
        if (ev?.Player is not { } player) return;

        if (player.GetForceMember() is not { Force: { } force } member) return;

        uint netId = member.NetId;

        if (LastCommunicationReward.TryGetValue(netId, out float last) &&
            Time.time - last < CommunicationWindow)
            return;

        LastCommunicationReward[netId] = Time.time;

        for (int index = 0; index < CommunicationCapPerWindow; index++)
            ForceContribution.Reward(member, force.CommunicationImpact);
    }

    /// <summary>
    /// キーカードを拾ったことを評価します。
    /// </summary>
    /// <remarks>標準では何も起きません。D クラスのギャングだけが加点します。</remarks>
    private static void OnPickingUpItem(PickingUpItemEventArgs ev)
    {
        if (!ev.IsAllowed || ev.Pickup is null) return;

        if (!IsKeycard(ev.Pickup.Type)) return;

        if (ev.Player.GetForceMember() is not { Force: { } force } member) return;

        ForceContribution.Reward(member, force.KeycardPickupReward);
    }

    // ───────────────────────────────
    // 914
    // ───────────────────────────────

    private static void OnUpgradingInventoryItem(UpgradingInventoryItemEventArgs ev)
    {
        if (!ev.IsAllowed || ev.Item is null) return;

        Judge914(ev.Player, ev.Item.Type);
    }

    private static void OnUpgradingPickup(UpgradingPickupEventArgs ev)
    {
        if (!ev.IsAllowed || ev.Pickup is null) return;

        // 置いた本人が誰かは拾えないので、所持者が分かるときだけ評価する。
        Judge914(ev.Pickup.PreviousOwner, ev.Pickup.Type);
    }

    /// <summary>
    /// SCP-914 にキーカードを通したことを評価します。
    /// </summary>
    private static void Judge914(Player player, ItemType type)
    {
        if (!IsKeycard(type)) return;

        if (player.GetForceMember() is not { Force: { } force } member) return;

        ForceContribution.Penalize(member, force.Scp914KeycardPenalty);
        ForceContribution.Reward(member, force.Scp914KeycardReward);
    }

    // ───────────────────────────────
    // 減点
    // ───────────────────────────────

    /// <summary>
    /// 同士討ちと、無抵抗な相手の即射殺を減点します。
    /// </summary>
    private static void OnHurting(HurtingEventArgs ev)
    {
        if (ev?.Attacker is not { } attacker || ev.Player is not { } victim) return;

        if (ReferenceEquals(attacker, victim)) return;

        if (attacker.GetForceMember() is not { Force: { } force } member) return;

        if (attacker.IsAllyOf(victim))
        {
            if (IsDeliberateFriendlyFire(attacker, force))
                ForceContribution.Penalize(member, force.FriendlyFirePenalty);

            return;
        }

        // 敵に対する攻撃。無抵抗な相手を一撃で殺したときだけ減点する。
        if (ev.IsDeathExpected() && !IsArmed(victim))
            ForceContribution.Penalize(member, force.ExecutionPenalty);
    }

    /// <summary>
    /// この同士討ちが意図的か。付近に敵が居らず、部隊の 40% がその場に居るときだけ true。
    /// </summary>
    private static bool IsDeliberateFriendlyFire(Player attacker, ForceBase force)
    {
        Vector3 origin = attacker.Position;

        bool enemyNearby = Player.List.Any(other =>
            other.IsSafePlayer() && other.IsAlive &&
            attacker.IsEnemyOf(other) &&
            (other.Position - origin).sqrMagnitude <= NoEnemyRadiusSqr);

        if (enemyNearby) return false;

        int alive = force.AliveCount;

        if (alive <= 0) return false;

        int gathered = force.Members.Count(member =>
            member.IsAlive && member.Player.IsAlive &&
            (member.Player.Position - origin).sqrMagnitude <= GatheredRadiusSqr);

        return (float)gathered / alive >= GatheredRatio;
    }

    /// <summary>
    /// 相手が銃を持っているかどうか。
    /// </summary>
    private static bool IsArmed(Player player) =>
        player.Items.Any(item => item is Firearm);

    /// <summary>
    /// SCP-018 を投げたら意図的な FF として減点します。
    /// </summary>
    private static void OnThrownProjectile(ThrownProjectileEventArgs ev)
    {
        if (ev?.Player is null || ev.Item?.Type is not ItemType.SCP018) return;

        if (ev.Player.GetForceMember() is not { Force: { } force } member) return;

        ForceContribution.Penalize(member, force.FriendlyFirePenalty);
    }

    // ───────────────────────────────
    // SL 標準の貢献
    //
    // 草案の「もちろん SL 標準の貢献度の機会も用いますが」にあたります。
    // バニラの influence は陣営単位で個人の内訳を持たないので、
    // 同じ機会をこちらでも拾って個人に付けます。
    // ───────────────────────────────

    /// <summary>脱出した隊員の、脱出前の隊に加点します。</summary>
    private static void OnEscaped(EscapeContext escape)
    {
        if (escape.Force is not { } force) return;

        if (escape.ForceMember is { } member)
            ForceContribution.Reward(member, ForceImpact.Medium);

        GrantWaveProgress(force, ForceImpact.Medium);
    }

    private static void OnActivatingGenerator(ActivatingGeneratorEventArgs ev)
    {
        if (!ev.IsAllowed || ev.Player.GetForceMember() is not { Force: { } force } member) return;

        ForceContribution.Reward(member, ForceImpact.Medium);
        GrantWaveProgress(force, ForceImpact.Medium);
    }

    /// <summary>
    /// SCP を倒した人に加点します。
    /// </summary>
    private static void OnDied(DiedEventArgs ev)
    {
        if (ev?.Attacker is not { } attacker || ev.Player is not { } victim) return;

        if (ReferenceEquals(attacker, victim) || !victim.IsScp) return;

        if (attacker.GetForceMember() is not { Force: { } force } member) return;

        ForceContribution.Reward(member, ForceImpact.Large);
        GrantWaveProgress(force, ForceImpact.Large);
    }

    /// <summary>
    /// 隊の働きを陣営の influence と次の波のタイマーに反映します。
    /// </summary>
    private static void GrantWaveProgress(ForceBase force, ForceImpact impact)
    {
        try
        {
            FactionInfluenceManager.Add(force.Faction, (int)impact);
            WaveManager.AdvanceTimer(force.Faction, (int)impact * WaveSecondsPerPoint);
        }
        catch (Exception exception)
        {
            Log.Debug($"[Force] ウェーブへの反映に失敗しました: {exception.Message}");
        }
    }

    private static bool IsKeycard(ItemType type) => type.IsKeycard();
}
