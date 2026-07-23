using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>Proximity-chat behavior declared by a CRole.</summary>
public readonly struct CRoleProximitySettings
{
    public CRoleProximitySettings(bool isAvailable, bool enabledByDefault = true)
    {
        IsAvailable = isAvailable;
        EnabledByDefault = isAvailable && enabledByDefault;
    }

    public bool IsAvailable { get; }
    public bool EnabledByDefault { get; }

    public static CRoleProximitySettings Disabled => default;

    public static CRoleProximitySettings Toggle(bool enabledByDefault = true)
        => new(true, enabledByDefault);
}

/// <summary>
/// One ordered voice route owned by a CRole. Return null when the next route should be evaluated.
/// </summary>
public readonly struct CRoleVoiceRoute
{
    public CRoleVoiceRoute(Func<VoiceRouteContext, VoiceRouteDecision?> evaluator)
    {
        Evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
    }

    public Func<VoiceRouteContext, VoiceRouteDecision?> Evaluator { get; }

    public VoiceRouteDecision? Evaluate(VoiceRouteContext context)
        => Evaluator?.Invoke(context);

    public static CRoleVoiceRoute ToPlayers(
        Predicate<Player> receivers,
        VoiceRouteDecision decision,
        Predicate<VoiceRouteContext> condition = null,
        bool includeSender = false)
    {
        if (receivers == null)
            throw new ArgumentNullException(nameof(receivers));

        return new CRoleVoiceRoute(context =>
        {
            if (!includeSender && context.Sender.Id == context.Receiver.Id)
                return null;

            return receivers(context.Receiver) && (condition == null || condition(context))
                ? decision
                : null;
        });
    }

    public static CRoleVoiceRoute ToTeams(
        IEnumerable<CTeam> receiverTeams,
        VoiceRouteDecision decision,
        Predicate<VoiceRouteContext> condition = null,
        bool includeSender = false,
        bool aliveReceiversOnly = true)
    {
        if (receiverTeams == null)
            throw new ArgumentNullException(nameof(receiverTeams));

        var receiverSet = new HashSet<CTeam>(receiverTeams);
        return ToPlayers(
            receiver => (!aliveReceiversOnly || receiver.IsAlive) &&
                        receiverSet.Contains(receiver.GetTeam()),
            decision,
            condition,
            includeSender);
    }

    /// <summary>Fallback route, useful for blocking every receiver not matched earlier.</summary>
    public static CRoleVoiceRoute All(
        VoiceRouteDecision decision,
        Predicate<VoiceRouteContext> condition = null,
        bool includeSender = true)
        => ToPlayers(_ => true, decision, condition, includeSender);
}

/// <summary>
/// Complete voice configuration for a CRole. Routes are evaluated in collection order.
/// </summary>
public readonly struct CRoleVoiceSettings
{
    private static readonly IReadOnlyList<CRoleVoiceRoute> EmptyRoutes = [];

    public CRoleVoiceSettings(
        CRoleProximitySettings proximity = default,
        IReadOnlyList<CRoleVoiceRoute> routes = null)
    {
        Proximity = proximity;
        Routes = routes;
    }

    public CRoleProximitySettings Proximity { get; }
    public IReadOnlyList<CRoleVoiceRoute> Routes => field ?? EmptyRoutes;

    public static CRoleVoiceSettings None => default;

    public static CRoleVoiceSettings WithProximity(bool enabledByDefault = true)
        => new(CRoleProximitySettings.Toggle(enabledByDefault));
}
