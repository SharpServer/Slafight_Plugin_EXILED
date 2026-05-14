using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using UnityEngine;

using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.Changes;

public class EasterEggsHandler : IBootstrapHandler
{
    public static EasterEggsHandler Instance { get; private set; }
    public static void Register() { Instance = new(); Instance.loadClips(); }
    public static void Unregister() { Instance = null; }

    public EasterEggsHandler()
    {
        Exiled.Events.Handlers.Server.RoundStarted += MelancholyNuke;
        Exiled.Events.Handlers.Server.RestartingRound += clearSpeakers;
    }

    ~EasterEggsHandler()
    {
        Exiled.Events.Handlers.Server.RoundStarted -= MelancholyNuke;
        Exiled.Events.Handlers.Server.RestartingRound -= clearSpeakers;
    }
    public static void CreateAndPlayAudio(string fileName, string audioPlayerName, Vector3 position, bool destroyOnEnd = false, Transform parent = null, bool isSpatial = false, float maxDistance = 5, float minDistance = 5, bool loadClip = true)
        => SpeakerApi.Play(fileName, audioPlayerName, position, destroyOnEnd, parent, isSpatial, maxDistance, minDistance, loadClip);

    public void clearSpeakers()
        => SpeakerApi.DestroyAll();

    public void loadClips()
    {
        SpeakerApi.LoadClip("ee_melancholy.ogg");
    }
    public void MelancholyNuke()
    {
        Room SpawnRoom = Room.Get(RoomType.HczNuke);
        Log.Debug(SpawnRoom.Position);
        Vector3 offset = new Vector3(-2.25f,-5.65f,0f);
        Vector3 Position = SpawnRoom.Position + SpawnRoom.Rotation * offset;
        Timing.RunCoroutine(MelancholyPlay(Position));
    }

    private IEnumerator<float> MelancholyPlay(Vector3 position)
    {
        int i = 0;
        for (;;)
        {
            CreateAndPlayAudio("ee_melancholy.ogg",("EE_Melancholy"+i),position,true,null,false,5.99999f,0,false);
            i++;
            yield return Timing.WaitForSeconds(420f);
        }
    }
}
