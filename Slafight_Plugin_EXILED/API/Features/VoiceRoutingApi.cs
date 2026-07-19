using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Enums;
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
    internal VoiceRouteContext(
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

    public Player Sender { get; }
    public Player Receiver { get; }
    public VoiceMessage Message { get; }

    /// <summary>The channel accepted from the sender before per-receiver validation.</summary>
    public VoiceChatChannel SourceChannel { get; }

    /// <summary>The channel vanilla selected for this receiver. None means vanilla would not deliver it.</summary>
    public VoiceChatChannel NativeChannel { get; }
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
    /// Convenience rule for the common [CTeam.a, CTeam.b] -> [CTeam.x] case.
    /// Use the constructor directly when role state, distance, permissions, or other context is needed.
    /// For example: BetweenTeams("allies", [CTeam.SCPs, CTeam.SerpentsHand],
    /// [CTeam.SCPs, CTeam.SerpentsHand], VoiceRouteDecision.Direct()).
    /// </summary>
    public static VoiceRouteRule BetweenTeams(
        string id,
        IEnumerable<CTeam> senderTeams,
        IEnumerable<CTeam> receiverTeams,
        VoiceRouteDecision decision,
        Predicate<VoiceRouteContext> condition = null,
        int priority = 0,
        bool includeSender = false)
    {
        if (senderTeams == null)
            throw new ArgumentNullException(nameof(senderTeams));
        if (receiverTeams == null)
            throw new ArgumentNullException(nameof(receiverTeams));

        var senderSet = new HashSet<CTeam>(senderTeams);
        var receiverSet = new HashSet<CTeam>(receiverTeams);

        return new VoiceRouteRule(
            id,
            context =>
            {
                if (!includeSender && context.Sender.Id == context.Receiver.Id)
                    return null;

                return senderSet.Contains(context.Sender.GetTeam()) &&
                       receiverSet.Contains(context.Receiver.GetTeam()) &&
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

    private static long _registrationSequence;
    private static bool _registered;

    public static IReadOnlyCollection<VoiceRouteRule> RegisteredRules
        => OrderedRules().Select(entry => entry.Rule).ToArray();

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
    }

    public static bool Unregister(string id)
        => !string.IsNullOrWhiteSpace(id) && Rules.Remove(id.Trim());

    public static void ClearRules()
    {
        Rules.Clear();
        _registrationSequence = 0;
    }

    private static void OnVoiceChatting(VoiceChattingEventArgs args)
    {
        if (!args.IsAllowed || !IsUsable(args.Player) ||
            args.VoiceMessage.Data == null || args.VoiceMessage.DataLength <= 0)
            return;

        var batches = new Dictionary<string, DeliveryBatch>(StringComparer.OrdinalIgnoreCase);
        var sourceChannel = args.VoiceModule.CurrentChannel;

        foreach (var hub in ReferenceHub.AllHubs)
        {
            if (hub?.connectionToClient == null)
                continue;

            var receiver = Player.Get(hub);
            if (!IsUsable(receiver))
                continue;

            var nativeChannel = args.VoiceModule == null ||
                                hub.roleManager.CurrentRole is not PlayerRoles.Voice.IVoiceRole receiverVoiceRole
                ? VoiceChatChannel.None
                : receiverVoiceRole.VoiceModule.ValidateReceive(args.Player.ReferenceHub, sourceChannel);

            var context = new VoiceRouteContext(
                args.Player,
                receiver,
                args.VoiceMessage,
                sourceChannel,
                nativeChannel);
            var decision = Resolve(context);
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
                batch = new DeliveryBatch(route);
                batches.Add(route.DeliveryKey, batch);
            }

            batch.Targets.Add(hub);
        }

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

    private static void OnReceivingVoiceMessage(ReceivingVoiceMessageEventArgs args)
    {
        if (!args.IsAllowed || !IsUsable(args.Sender) || !IsUsable(args.Player))
            return;

        var context = new VoiceRouteContext(
            args.Sender,
            args.Player,
            args.VoiceMessage,
            args.VoiceModule.CurrentChannel,
            args.VoiceMessage.Channel);
        var decision = Resolve(context);
        if (decision?.SuppressNative == true)
            args.IsAllowed = false;
    }

    private static VoiceRouteDecision? Resolve(VoiceRouteContext context)
    {
        if (!string.IsNullOrEmpty(context.Sender.UniqueRole) &&
            CRole.TryGetByUniqueRole(context.Sender.UniqueRole, out var role) &&
            role != null)
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
                    Log.Error($"[VoiceRouting] CRole route failed for '{role.UniqueRoleName}': {ex}");
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

    private static IEnumerable<RegisteredRule> OrderedRules()
        => Rules.Values
            .OrderByDescending(entry => entry.Rule.Priority)
            .ThenByDescending(entry => entry.Sequence);

    private static bool IsUsable(Player player)
    {
        try
        {
            return player != null && player.ReferenceHub != null && player.IsNotHost();
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
        public DeliveryBatch(VoiceRouteDecision decision)
        {
            Decision = decision;
        }

        public VoiceRouteDecision Decision { get; }
        public List<ReferenceHub> Targets { get; } = [];
    }
}
