using System;
using LabApi.Features.Console;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Changes;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class PreloadHandler : SlafightLabApiHandler
{
    protected override void RegisterEvents(EventSubscriptionScope subscriptions) { }

    public override void OnServerWaitingForPlayers()
    {
        // WaitingForPlayersChanges Clips
        TryPreloadClip(WaitingForPlayersChanges.WaitingMusicClipName);
        TryPreloadClip(WaitingForPlayersChanges.RoundStartOutroClipName);

        // EzShelterElevator Clips
        TryPreloadClip(EzShelterElevator.DefaultMovingAudio);
        TryPreloadClip(EzShelterElevator.DefaultDoorCloseAudio);
        TryPreloadClip(EzShelterElevator.DefaultDoorOpenAudio);
        TryPreloadClip(EzShelterElevator.DefaultElevatorJamAudio);
    }

    private static void TryPreloadClip(string fileName)
    {
        try
        {
            SpeakerApi.LoadClip(fileName);
        }
        catch (Exception ex)
        {
            Logger.Warn($"[WaitingForPlayersChanges] Failed to preload clip '{fileName}': {ex.Message}");
        }
    }
}