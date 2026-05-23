using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.SpecialEvents;

namespace Slafight_Plugin_EXILED.API.Features.Teams;

public sealed class CTeamProfileManager : IBootstrapHandler
{
    public const string DefaultProfile = "Default";
    public const string FacilityTerminationProfile = "FacilityTermination";

    private static string? _activeProfile;

    public static string ActiveProfile => _activeProfile ?? ResolveImplicitProfile();

    public static void SetActive(string profileKey)
    {
        _activeProfile = string.IsNullOrWhiteSpace(profileKey)
            ? DefaultProfile
            : profileKey;
    }

    public static void ClearActive()
    {
        _activeProfile = null;
    }

    public static bool IsActive(string profileKey) =>
        string.Equals(ActiveProfile, profileKey, System.StringComparison.Ordinal);

    public static void Register()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers += Reset;
        Exiled.Events.Handlers.Server.RestartingRound += Reset;
        Exiled.Events.Handlers.Player.Left += OnPlayerLeft;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers -= Reset;
        Exiled.Events.Handlers.Server.RestartingRound -= Reset;
        Exiled.Events.Handlers.Player.Left -= OnPlayerLeft;
        Reset();
    }

    private static void Reset()
    {
        ClearActive();
        CTeamPlayerState.ClearAllTeamOverrides();
    }

    private static void OnPlayerLeft(LeftEventArgs ev)
    {
        if (ev.Player != null)
            CTeamPlayerState.ClearTeamOverride(ev.Player);
    }

    private static string ResolveImplicitProfile()
    {
        return SpecialEventsHandler.Instance?.NowEvent is SpecialEventType.FacilityTermination
            ? FacilityTerminationProfile
            : DefaultProfile;
    }
}
