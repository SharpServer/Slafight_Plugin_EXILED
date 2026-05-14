using Exiled.API.Enums;
using Exiled.API.Features;
using UnityEngine;

using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.Changes;

public class EasterEggsHandler : IBootstrapHandler
{
    private const string MelancholyClip = "ee_melancholy.ogg";
    private const string MelancholyAudioPlayer = "EE_Melancholy";

    public static EasterEggsHandler Instance { get; private set; }
    public static void Register() { Instance = new(); Instance.LoadClips(); }
    public static void Unregister()
    {
        if (Instance != null)
        {
            Exiled.Events.Handlers.Server.RoundStarted -= Instance.MelancholyNuke;
            Exiled.Events.Handlers.Server.RestartingRound -= Instance.ClearSpeakers;
        }

        Instance = null;
    }

    public EasterEggsHandler()
    {
        Exiled.Events.Handlers.Server.RoundStarted += MelancholyNuke;
        Exiled.Events.Handlers.Server.RestartingRound += ClearSpeakers;
    }

    ~EasterEggsHandler()
    {
        Exiled.Events.Handlers.Server.RoundStarted -= MelancholyNuke;
        Exiled.Events.Handlers.Server.RestartingRound -= ClearSpeakers;
    }
    public static void CreateAndPlayAudio(string fileName, string audioPlayerName, Vector3 position, bool destroyOnEnd = false, Transform parent = null, bool isSpatial = false, float maxDistance = 5, float minDistance = 5, bool loadClip = true)
        => SpeakerApi.Play(fileName, audioPlayerName, position, destroyOnEnd, parent, isSpatial, maxDistance, minDistance, loadClip);

    public static void CreateAndLoopAudio(string fileName, string audioPlayerName, Vector3 position, Transform parent = null, bool isSpatial = false, float maxDistance = 5, float minDistance = 5, bool loadClip = true)
        => SpeakerApi.PlayLoop(fileName, audioPlayerName, position, parent, isSpatial, maxDistance, minDistance, loadClip);

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
        CreateAndLoopAudio(MelancholyClip, MelancholyAudioPlayer, position, null, false, 5.99999f, 0, false);
    }
}
