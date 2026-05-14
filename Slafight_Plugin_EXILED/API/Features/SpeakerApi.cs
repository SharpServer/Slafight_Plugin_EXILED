using System;
using System.Collections.Generic;
using System.IO;
using Exiled.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

public static class SpeakerApi
{
    public readonly struct Playback
    {
        public Playback(AudioPlayer audioPlayer, Speaker speaker, string clipName)
        {
            AudioPlayer = audioPlayer;
            Speaker = speaker;
            ClipName = clipName;
        }

        public AudioPlayer AudioPlayer { get; }
        public Speaker Speaker { get; }
        public string ClipName { get; }
    }

    public static string AudioDirectory => Plugin.Singleton.Config.AudioReferences;

    public static Speaker CreateOrGetSpeaker(
        string audioPlayerName,
        Vector3 position,
        Transform? parent = null,
        string? speakerName = null,
        bool isSpatial = false,
        float maxDistance = 5f,
        float minDistance = 5f)
    {
        var audioPlayer = AudioPlayer.CreateOrGet(audioPlayerName);
        return CreateOrGetSpeaker(audioPlayer, speakerName ?? audioPlayerName, position, parent, isSpatial, maxDistance, minDistance);
    }

    public static Speaker CreateOrGetSpeaker(
        AudioPlayer audioPlayer,
        string speakerName,
        Vector3 position,
        Transform? parent = null,
        bool isSpatial = false,
        float maxDistance = 5f,
        float minDistance = 5f)
    {
        if (audioPlayer == null)
            throw new ArgumentNullException(nameof(audioPlayer));

        if (string.IsNullOrWhiteSpace(speakerName))
            throw new ArgumentException("Speaker name cannot be empty.", nameof(speakerName));

        if (!audioPlayer.TryGetSpeaker(speakerName, out Speaker speaker))
        {
            speaker = audioPlayer.AddSpeaker(
                speakerName,
                isSpatial: isSpatial,
                maxDistance: maxDistance,
                minDistance: minDistance);
        }

        SetTransform(speaker, position, parent);
        return speaker;
    }

    public static Playback Play(
        string fileName,
        string audioPlayerName,
        Vector3 position,
        bool destroyOnEnd = false,
        Transform? parent = null,
        bool isSpatial = false,
        float maxDistance = 5f,
        float minDistance = 5f,
        bool loadClip = true,
        string? speakerName = null,
        string? clipName = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Audio file name cannot be empty.", nameof(fileName));

        if (string.IsNullOrWhiteSpace(audioPlayerName))
            throw new ArgumentException("Audio player name cannot be empty.", nameof(audioPlayerName));

        clipName ??= fileName;

        var audioPlayer = AudioPlayer.CreateOrGet(audioPlayerName);
        var speaker = CreateOrGetSpeaker(
            audioPlayer,
            speakerName ?? audioPlayerName,
            position,
            parent,
            isSpatial,
            maxDistance,
            minDistance);

        if (loadClip)
            LoadClip(fileName, clipName);

        audioPlayer.AddClip(clipName, destroyOnEnd: destroyOnEnd);
        return new Playback(audioPlayer, speaker, clipName);
    }

    public static void LoadClip(string fileName, string? clipName = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Audio file name cannot be empty.", nameof(fileName));

        clipName ??= fileName;
        AudioClipStorage.LoadClip(Path.Combine(AudioDirectory, fileName), clipName);
    }

    public static bool TryDestroy(string audioPlayerName)
    {
        if (string.IsNullOrWhiteSpace(audioPlayerName))
            return false;

        if (!AudioPlayer.TryGet(audioPlayerName, out AudioPlayer audioPlayer) || audioPlayer == null)
            return false;

        audioPlayer.Destroy();
        return true;
    }

    public static int DestroyAll()
    {
        var names = new List<string>(AudioPlayer.AudioPlayerByName.Count);
        foreach (var name in AudioPlayer.AudioPlayerByName.Keys)
        {
            if (!string.IsNullOrEmpty(name))
                names.Add(name);
        }

        foreach (var name in names)
            TryDestroy(name);

        return names.Count;
    }

    public static void SetTransform(Speaker speaker, Vector3 position, Transform? parent = null)
    {
        if (speaker == null)
            throw new ArgumentNullException(nameof(speaker));

        if (parent)
        {
            speaker.transform.SetParent(parent);
            speaker.transform.localPosition = Vector3.zero;
            speaker.transform.localRotation = Quaternion.identity;
            return;
        }

        speaker.Position = position;
    }

    public static IEnumerable<string> GetAudioPlayerNames()
        => new List<string>(AudioPlayer.AudioPlayerByName.Keys);
}
