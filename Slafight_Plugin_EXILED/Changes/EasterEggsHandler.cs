using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using UnityEngine;
using Server = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.Changes;

public class EasterEggsHandler : IBootstrapHandler, IDisposable
{
    private const string MelancholyClip = "ee_melancholy.ogg";
    private const string MelancholyAudioPlayer = "EE_Melancholy";

    public static EasterEggsHandler Instance { get; private set; }
    public static void Register()
    {
        Unregister();
        Instance = new();
        Instance.LoadClips();
    }
    public static void Unregister()
    {
        Instance?.Dispose();
        Instance = null;
    }

    private bool _disposed;

    public EasterEggsHandler()
    {
        Server.RoundStarted += MelancholyNuke;
        Server.RestartingRound += ClearSpeakers;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Server.RoundStarted -= MelancholyNuke;
        Server.RestartingRound -= ClearSpeakers;
        ClearSpeakers();
        GC.SuppressFinalize(this);
    }
    public void ClearSpeakers()
        => SpeakerApi.DestroyAll();

    public void LoadClips()
    {
        SpeakerApi.LoadClip(MelancholyClip);
    }
    public void MelancholyNuke()
    {
        Room spawnRoom = Room.Get(RoomType.HczNuke);
        if (spawnRoom == null) return;

        Log.Debug(spawnRoom.Position);
        Vector3 offset = new Vector3(-2.25f,-5.65f,0f);
        Vector3 position = spawnRoom.Position + spawnRoom.Rotation * offset;
        SpeakerApi.PlayLoop(MelancholyClip, MelancholyAudioPlayer, position, null, false, 5.99999f, 0, false);
    }
}
