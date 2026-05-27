using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

using Slafight_Plugin_EXILED.API.Features;
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
        public string AudioPlayerName => AudioPlayer?.Name ?? string.Empty;
        public bool IsValid => AudioPlayer != null && !string.IsNullOrWhiteSpace(ClipName);

        public bool Stop()
            => SpeakerApi.Stop(this);

        public bool DestroyAudioPlayer()
            => SpeakerApi.TryDestroy(AudioPlayerName);

        public void SetTransform(Vector3 position, Transform? parent = null)
            => SpeakerApi.SetTransform(this, position, parent);
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
        if (string.IsNullOrWhiteSpace(audioPlayerName))
            throw new ArgumentException("Audio player name cannot be empty.", nameof(audioPlayerName));

        var audioPlayer = AudioPlayer.CreateOrGet(audioPlayerName);
        return PlayCore(
            fileName,
            audioPlayer,
            position,
            parent,
            isSpatial,
            maxDistance,
            minDistance,
            loadClip,
            speakerName ?? audioPlayerName,
            clipName,
            loop: false,
            destroyOnEnd: destroyOnEnd,
            restartIfAlreadyPlaying: false);
    }

    public static Playback Play(
        string fileName,
        AudioPlayer audioPlayer,
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
        return PlayCore(
            fileName,
            audioPlayer,
            position,
            parent,
            isSpatial,
            maxDistance,
            minDistance,
            loadClip,
            speakerName,
            clipName,
            loop: false,
            destroyOnEnd: destroyOnEnd,
            restartIfAlreadyPlaying: false);
    }

    public static Playback PlayLoop(
        string fileName,
        string audioPlayerName,
        Vector3 position,
        Transform? parent = null,
        bool isSpatial = false,
        float maxDistance = 5f,
        float minDistance = 5f,
        bool loadClip = true,
        string? speakerName = null,
        string? clipName = null,
        bool restartIfAlreadyPlaying = true)
    {
        if (string.IsNullOrWhiteSpace(audioPlayerName))
            throw new ArgumentException("Audio player name cannot be empty.", nameof(audioPlayerName));

        var audioPlayer = AudioPlayer.CreateOrGet(audioPlayerName);
        return PlayCore(
            fileName,
            audioPlayer,
            position,
            parent,
            isSpatial,
            maxDistance,
            minDistance,
            loadClip,
            speakerName ?? audioPlayerName,
            clipName,
            loop: true,
            destroyOnEnd: false,
            restartIfAlreadyPlaying: restartIfAlreadyPlaying);
    }

    public static Playback PlayLoop(
        string fileName,
        AudioPlayer audioPlayer,
        Vector3 position,
        Transform? parent = null,
        bool isSpatial = false,
        float maxDistance = 5f,
        float minDistance = 5f,
        bool loadClip = true,
        string? speakerName = null,
        string? clipName = null,
        bool restartIfAlreadyPlaying = true)
    {
        return PlayCore(
            fileName,
            audioPlayer,
            position,
            parent,
            isSpatial,
            maxDistance,
            minDistance,
            loadClip,
            speakerName,
            clipName,
            loop: true,
            destroyOnEnd: false,
            restartIfAlreadyPlaying: restartIfAlreadyPlaying);
    }

    private static Playback PlayCore(
        string fileName,
        AudioPlayer audioPlayer,
        Vector3 position,
        Transform? parent,
        bool isSpatial,
        float maxDistance,
        float minDistance,
        bool loadClip,
        string? speakerName,
        string? clipName,
        bool loop,
        bool destroyOnEnd,
        bool restartIfAlreadyPlaying)
    {
        if (audioPlayer == null)
            throw new ArgumentNullException(nameof(audioPlayer));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Audio file name cannot be empty.", nameof(fileName));

        clipName ??= fileName;

        var speaker = CreateOrGetSpeaker(
            audioPlayer,
            speakerName ?? audioPlayer.Name,
            position,
            parent,
            isSpatial,
            maxDistance,
            minDistance);

        if (loadClip)
            LoadClip(fileName, clipName);

        if (restartIfAlreadyPlaying)
            audioPlayer.RemoveClipByName(clipName);

        audioPlayer.AddClip(clipName, loop: loop, destroyOnEnd: destroyOnEnd);
        return new Playback(audioPlayer, speaker, clipName);
    }

    public static void LoadClip(string fileName, string? clipName = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Audio file name cannot be empty.", nameof(fileName));

        clipName ??= fileName;
        if (AudioClipStorage.AudioClips.ContainsKey(clipName))
            return;

        AudioClipStorage.LoadClip(Path.Combine(AudioDirectory, fileName), clipName);
    }

    public static bool StopClip(string audioPlayerName, string clipName)
    {
        if (string.IsNullOrWhiteSpace(audioPlayerName) || string.IsNullOrWhiteSpace(clipName))
            return false;

        return AudioPlayer.TryGet(audioPlayerName, out AudioPlayer audioPlayer) &&
               audioPlayer != null &&
               audioPlayer.RemoveClipByName(clipName);
    }

    public static bool Stop(Playback playback)
    {
        if (!playback.IsValid)
            return false;

        return playback.AudioPlayer.RemoveClipByName(playback.ClipName);
    }

    public static int StopClip(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return 0;

        int stopped = 0;
        foreach (var audioPlayerName in GetAudioPlayerNames())
        {
            if (StopClip(audioPlayerName, clipName))
                stopped++;
        }

        return stopped;
    }

    public static int StopClips(params Playback[] playbacks)
    {
        if (playbacks == null || playbacks.Length == 0)
            return 0;

        int stopped = 0;
        foreach (var playback in playbacks)
        {
            if (Stop(playback))
                stopped++;
        }

        return stopped;
    }

    public static int StopClips(string audioPlayerName, params string[] clipNames)
    {
        if (string.IsNullOrWhiteSpace(audioPlayerName) || clipNames == null || clipNames.Length == 0)
            return 0;

        int stopped = 0;
        foreach (var clipName in clipNames)
        {
            if (StopClip(audioPlayerName, clipName))
                stopped++;
        }

        return stopped;
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

    public static bool SetTransform(Playback playback, Vector3 position, Transform? parent = null)
    {
        if (playback.Speaker == null)
            return false;

        SetTransform(playback.Speaker, position, parent);
        return true;
    }

    public static bool TryGetAudioPlayer(string audioPlayerName, out AudioPlayer audioPlayer)
    {
        if (string.IsNullOrWhiteSpace(audioPlayerName))
        {
            audioPlayer = null;
            return false;
        }

        return AudioPlayer.TryGet(audioPlayerName, out audioPlayer) && audioPlayer != null;
    }

    public static bool TryGetSpeaker(string audioPlayerName, string speakerName, out Speaker speaker)
    {
        speaker = null;
        return TryGetAudioPlayer(audioPlayerName, out var audioPlayer) &&
               !string.IsNullOrWhiteSpace(speakerName) &&
               audioPlayer.TryGetSpeaker(speakerName, out speaker) &&
               speaker != null;
    }

    public static IEnumerable<string> GetAudioPlayerNames()
        => new List<string>(AudioPlayer.AudioPlayerByName.Keys);
}
