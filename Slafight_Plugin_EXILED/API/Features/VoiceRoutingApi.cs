using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerRoles.Voice;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using VoiceChat;
using VoiceChat.Networking;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// One sender/receiver pair being evaluated by the voice router.
/// </summary>
public sealed class VoiceRouteContext
{
    internal VoiceRouteContext()
    {
    }

    internal VoiceRouteContext(
        Player sender,
        Player receiver,
        VoiceMessage message,
        VoiceChatChannel sourceChannel,
        VoiceChatChannel nativeChannel)
    {
        Set(sender, receiver, message, sourceChannel, nativeChannel);
    }

    public Player Sender { get; private set; }
    public Player Receiver { get; private set; }
    public VoiceMessage Message { get; private set; }

    /// <summary>The channel accepted from the sender before per-receiver validation.</summary>
    public VoiceChatChannel SourceChannel { get; private set; }

    /// <summary>The channel vanilla selected for this receiver. None means vanilla would not deliver it.</summary>
    public VoiceChatChannel NativeChannel { get; private set; }

    /// <summary>
    /// 送り手・受け手の組を差し替えます。Context は使い回すので、評価関数の外で保持しないこと。
    /// </summary>
    internal void Set(
        Player sender,
        Player receiver,
        VoiceMessage message,
        VoiceChatChannel sourceChannel,
        VoiceChatChannel nativeChannel)
    {
        Sender = sender;
        Receiver = receiver;
        Message = message;
        SourceChannel = sourceChannel;
        NativeChannel = nativeChannel;
    }
}

/// <summary>
/// Result of a matching voice rule. Direct delivery sends the original VoiceMessage to each
/// selected connection; spatial delivery reuses the existing SpeakerToy path for attenuation.
/// </summary>
public readonly struct VoiceRouteDecision
{
    private VoiceRouteDecision(
        bool suppressNative,
        string deliveryKey,
        VoiceChatChannel directChannel,
        bool isSpatial,
        float maxDistance,
        float minDistance,
        float volume)
    {
        SuppressNative = suppressNative;
        DeliveryKey = deliveryKey;
        DirectChannel = directChannel;
        IsSpatial = isSpatial;
        MaxDistance = maxDistance;
        MinDistance = minDistance;
        Volume = volume;
    }

    public bool SuppressNative { get; }
    public string DeliveryKey { get; }
    public VoiceChatChannel DirectChannel { get; }
    public bool HasDirectDelivery => DirectChannel != VoiceChatChannel.None;
    public bool HasSpatialDelivery => !string.IsNullOrWhiteSpace(DeliveryKey);
    public bool HasDelivery => HasDirectDelivery || HasSpatialDelivery;
    public bool IsSpatial { get; }
    public float MaxDistance { get; }
    public float MinDistance { get; }
    public float Volume { get; }

    public static VoiceRouteDecision Block()
        => new(true, null, VoiceChatChannel.None, false, 1f, 1f, 1f);

    /// <summary>
    /// Direct global delivery that is valid for both human and SCP voice modules.
    /// </summary>
    public static VoiceRouteDecision Direct(bool suppressNative = true)
        => Direct(VoiceChatChannel.RoundSummary, suppressNative);

    /// <summary>
    /// Direct delivery using a caller-selected client playback channel.
    /// The caller is responsible for choosing a channel accepted by the receiver and sender voice modules.
    /// </summary>
    public static VoiceRouteDecision Direct(
        VoiceChatChannel channel,
        bool suppressNative = true)
        => new(
            suppressNative,
            null,
            ValidateDirectChannel(channel),
            false,
            1f,
            1f,
            1f);

    public static VoiceRouteDecision Spatial(
        string deliveryKey,
        float maxDistance,
        float minDistance,
        float volume = 1f,
        bool suppressNative = false)
        => new(
            suppressNative,
            ValidateDeliveryKey(deliveryKey),
            VoiceChatChannel.None,
            true,
            Mathf.Max(1f, maxDistance),
            Mathf.Clamp(minDistance, 1f, Mathf.Max(1f, maxDistance)),
            Mathf.Max(0f, volume));

    private static string ValidateDeliveryKey(string deliveryKey)
    {
        if (string.IsNullOrWhiteSpace(deliveryKey))
            throw new ArgumentException("A voice delivery key is required.", nameof(deliveryKey));

        return deliveryKey.Trim();
    }

    private static VoiceChatChannel ValidateDirectChannel(VoiceChatChannel channel)
    {
        if (channel == VoiceChatChannel.None)
            throw new ArgumentException("A direct voice channel is required.", nameof(channel));

        return channel;
    }
}

/// <summary>
/// A priority-ordered voice rule. Return null when the rule does not apply.
/// The first rule returning a decision owns that sender/receiver pair.
/// </summary>
public sealed class VoiceRouteRule
{
    public VoiceRouteRule(
        string id,
        Func<VoiceRouteContext, VoiceRouteDecision?> evaluator,
        int priority = 0)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A voice route id is required.", nameof(id));

        Id = id.Trim();
        Evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        Priority = priority;
    }

    public string Id { get; }
    public int Priority { get; }
    public Func<VoiceRouteContext, VoiceRouteDecision?> Evaluator { get; }

    public static VoiceRouteRule ForPlayers(
        string id,
        Predicate<Player> senders,
        Predicate<Player> receivers,
        VoiceRouteDecision decision,
        Predicate<VoiceRouteContext> condition = null,
        int priority = 0)
    {
        if (senders == null)
            throw new ArgumentNullException(nameof(senders));
        if (receivers == null)
            throw new ArgumentNullException(nameof(receivers));

        return new VoiceRouteRule(
            id,
            context => senders(context.Sender) &&
                       receivers(context.Receiver) &&
                       (condition == null || condition(context))
                ? decision
                : null,
            priority);
    }

    /// <summary>
    /// チーム間の経路を作る簡易ルールです。役職状態や距離が要るならコンストラクタを使ってください。
    /// </summary>
    public static VoiceRouteRule BetweenTeams(
        string id,
        IEnumerable<CustomTeam> senderTeams,
        IEnumerable<CustomTeam> receiverTeams,
        VoiceRouteDecision decision,
        Predicate<VoiceRouteContext> condition = null,
        int priority = 0,
        bool includeSender = false)
    {
        if (senderTeams == null)
            throw new ArgumentNullException(nameof(senderTeams));
        if (receiverTeams == null)
            throw new ArgumentNullException(nameof(receiverTeams));

        var senderSet = new HashSet<CustomTeam>(senderTeams);
        var receiverSet = new HashSet<CustomTeam>(receiverTeams);

        return new VoiceRouteRule(
            id,
            context =>
            {
                if (!includeSender && context.Sender.Id == context.Receiver.Id)
                    return null;

                return senderSet.Contains(CustomTeam.Of(context.Sender)) &&
                       receiverSet.Contains(CustomTeam.Of(context.Receiver)) &&
                       (condition == null || condition(context))
                    ? decision
                    : null;
            },
            priority);
    }
}

/// <summary>
/// Central voice router. Rules may redirect, mirror, or suppress voice without replacing
/// unaffected vanilla voice behavior.
/// </summary>
public static class VoiceRoutingApi
{
    private static readonly Dictionary<string, RegisteredRule> Rules =
        new(StringComparer.OrdinalIgnoreCase);

    // 音声パケット1つにつき「受信者数 x 2」回 Resolve が走るため、
    // 並び替え結果・バッチ用の器・Context は毎回作らずここで使い回す。
    private static readonly RegisteredRule[] EmptyRules = [];
    private static readonly Dictionary<string, DeliveryBatch> BatchScratch =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<DeliveryBatch> BatchPool = [];
    private static readonly VoiceRouteContext ContextScratch = new();

    private static RegisteredRule[] _orderedRules;
    private static long _registrationSequence;
    private static bool _registered;

    public static IReadOnlyCollection<VoiceRouteRule> RegisteredRules
        => [.. OrderedRules().Select(entry => entry.Rule)];

    public static void RegisterEvents()
    {
        if (_registered)
            return;

        Exiled.Events.Handlers.Player.VoiceChatting += OnVoiceChatting;
        Exiled.Events.Handlers.Player.ReceivingVoiceMessage += OnReceivingVoiceMessage;
        _registered = true;
    }

    public static void UnregisterEvents()
    {
        if (!_registered)
            return;

        Exiled.Events.Handlers.Player.VoiceChatting -= OnVoiceChatting;
        Exiled.Events.Handlers.Player.ReceivingVoiceMessage -= OnReceivingVoiceMessage;
        ClearRules();
        _registered = false;
    }

    /// <summary>Adds or atomically replaces a rule with the same id.</summary>
    public static void Register(VoiceRouteRule rule)
    {
        if (rule == null)
            throw new ArgumentNullException(nameof(rule));

        Rules[rule.Id] = new RegisteredRule(rule, ++_registrationSequence);
        _orderedRules = null;
    }

    public static bool Unregister(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !Rules.Remove(id.Trim()))
            return false;

        _orderedRules = null;
        return true;
    }

    public static void ClearRules()
    {
        Rules.Clear();
        _registrationSequence = 0;
        _orderedRules = null;
    }

    private static void OnVoiceChatting(VoiceChattingEventArgs args)
    {
        if (!args.IsAllowed || !IsUsable(args.Player) ||
            args.VoiceMessage.Data == null || args.VoiceMessage.DataLength <= 0)
            return;

        // 送り手に効く経路が1つも無いなら、全 hub 走査ごと省く。
        // これを通らない限り毎パケット「受信者数」回の評価が走る。
        if (!HasAnyRoute(args.Player))
            return;

        // 使い回しの器なので、前回が例外で中断していた場合に備えて必ず空から始める。
        var batches = BatchScratch;
        if (batches.Count > 0)
            ReturnBatches(batches);

        var sourceChannel = args.VoiceModule.CurrentChannel;

        foreach (var hub in ReferenceHub.AllHubs)
        {
            if (hub?.connectionToClient == null)
                continue;

            var receiver = Player.Get(hub);
            if (!IsUsable(receiver))
                continue;

            var nativeChannel = args.VoiceModule == null ||
                                hub.roleManager.CurrentRole is not IVoiceRole receiverVoiceRole
                ? VoiceChatChannel.None
                : receiverVoiceRole.VoiceModule.ValidateReceive(args.Player.ReferenceHub, sourceChannel);

            ContextScratch.Set(
                args.Player,
                receiver,
                args.VoiceMessage,
                sourceChannel,
                nativeChannel);
            var decision = Resolve(ContextScratch);
            if (decision == null || !decision.Value.HasDelivery)
                continue;

            var route = decision.Value;
            if (route.HasDirectDelivery)
            {
                var directMessage = args.VoiceMessage;
                directMessage.Channel = route.DirectChannel;
                hub.connectionToClient.Send(directMessage);
            }

            if (!route.HasSpatialDelivery)
                continue;

            if (!batches.TryGetValue(route.DeliveryKey, out var batch))
            {
                batch = RentBatch(route);
                batches.Add(route.DeliveryKey, batch);
            }

            batch.Targets.Add(hub);
        }

        try
        {
            foreach (var batch in batches.Values)
            {
                var decision = batch.Decision;
                var speaker = PlayerSpeakerManager.GetOrCreateSpeaker(
                    args.Player,
                    decision.DeliveryKey,
                    decision.IsSpatial,
                    decision.MaxDistance,
                    decision.MinDistance,
                    decision.Volume,
                    speakerName: decision.DeliveryKey);

                if (!speaker.IsValid)
                {
                    Log.Warn($"[VoiceRouting] Could not create delivery '{decision.DeliveryKey}' for {args.Player.Nickname}.");
                    continue;
                }

                speaker.SendFrame(args.VoiceMessage.Data, args.VoiceMessage.DataLength, batch.Targets);
            }
        }
        finally
        {
            ReturnBatches(batches);
        }
    }

    private static void OnReceivingVoiceMessage(ReceivingVoiceMessageEventArgs args)
    {
        if (!args.IsAllowed || !IsUsable(args.Sender) || !IsUsable(args.Player))
            return;

        if (!HasAnyRoute(args.Sender))
            return;

        ContextScratch.Set(
            args.Sender,
            args.Player,
            args.VoiceMessage,
            args.VoiceModule.CurrentChannel,
            args.VoiceMessage.Channel);
        var decision = Resolve(ContextScratch);
        if (decision?.SuppressNative == true)
            args.IsAllowed = false;
    }

    /// <summary>
    /// 何も経路が無い送り手のパケットで hub 一覧に触れないための、安い門番です。
    /// </summary>
    private static bool HasAnyRoute(Player sender)
    {
        if (Rules.Count > 0)
            return true;

        return CustomRole.Of(sender) is { Voice.Routes.Count: > 0 };
    }

    private static DeliveryBatch RentBatch(VoiceRouteDecision decision)
    {
        DeliveryBatch batch;
        if (BatchPool.Count > 0)
        {
            int last = BatchPool.Count - 1;
            batch = BatchPool[last];
            BatchPool.RemoveAt(last);
        }
        else
        {
            batch = new DeliveryBatch();
        }

        batch.Reset(decision);
        return batch;
    }

    private static void ReturnBatches(Dictionary<string, DeliveryBatch> batches)
    {
        foreach (var batch in batches.Values)
        {
            batch.Targets.Clear();
            BatchPool.Add(batch);
        }

        batches.Clear();
    }

    /// <summary>
    /// 送り手の役職が持つ経路を先に見て、次に登録済みのルールを見ます。
    /// 最初に決まったものを返します。
    /// </summary>
    private static VoiceRouteDecision? Resolve(VoiceRouteContext context)
    {
        // 役職が自分で名乗る経路が最優先。
        if (CustomRole.Of(context.Sender) is { } role)
        {
            foreach (var route in role.Voice.Routes)
            {
                try
                {
                    var decision = route.Evaluate(context);
                    if (decision != null)
                        return decision;
                }
                catch (Exception ex)
                {
                    Log.Error($"[VoiceRouting] Role route failed for '{role.Name}': {ex}");
                }
            }
        }

        foreach (var entry in OrderedRules())
        {
            try
            {
                var decision = entry.Rule.Evaluator(context);
                if (decision != null)
                    return decision;
            }
            catch (Exception ex)
            {
                Log.Error($"[VoiceRouting] Rule '{entry.Rule.Id}' failed: {ex}");
            }
        }

        return null;
    }

    /// <summary>
    /// 評価順に並べたルールのキャッシュ。Register / Unregister / ClearRules で破棄される。
    /// </summary>
    private static RegisteredRule[] OrderedRules()
    {
        if (_orderedRules != null)
            return _orderedRules;

        if (Rules.Count == 0)
            return _orderedRules = EmptyRules;

        return _orderedRules =
        [
            .. Rules.Values
                .OrderByDescending(entry => entry.Rule.Priority)
                .ThenByDescending(entry => entry.Sequence)
        ];
    }

    private static bool IsUsable(Player player)
    {
        try
        {
            return player.IsSafePlayer();
        }
        catch
        {
            return false;
        }
    }

    private readonly struct RegisteredRule
    {
        public RegisteredRule(VoiceRouteRule rule, long sequence)
        {
            Rule = rule;
            Sequence = sequence;
        }

        public VoiceRouteRule Rule { get; }
        public long Sequence { get; }
    }

    private sealed class DeliveryBatch
    {
        public VoiceRouteDecision Decision { get; private set; }
        public List<ReferenceHub> Targets { get; } = [];

        public void Reset(VoiceRouteDecision decision)
        {
            Decision = decision;
            Targets.Clear();
        }
    }
}

/// <summary>
/// <see cref="VoiceRoutingApi"/> の起動と停止を受け持ちます。
/// </summary>
public sealed class VoiceRoutingApiLifecycle : EventHandlerBase
{
    /// <inheritdoc />
    public override void RegisterEvents() => VoiceRoutingApi.RegisterEvents();

    /// <inheritdoc />
    public override void UnregisterEvents() => VoiceRoutingApi.UnregisterEvents();
}
