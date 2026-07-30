#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Exiled.API.Features;
using UnityEngine;
using VoiceChat;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// 音声ファイル / PCM サンプルから <see cref="AudioClip"/> を作り、名前付きでキャッシュする。
/// </summary>
/// <remarks>
/// <para>
/// 生成するクリップは常にモノラル / <see cref="VoiceChatSettings.SampleRate"/>。
/// SCP:SL の音声送信経路（<see cref="SpeakerApi"/>）がその形式しか扱わないため。
/// </para>
/// <para>
/// <see cref="AudioClip"/> は <see cref="UnityEngine.Object"/> なので、キャッシュから外すときは
/// ネイティブ側も破棄する。キャッシュがクリップの所有者である前提。
/// </para>
/// </remarks>
public static class AudioClipApi
{
    private const int TargetSampleRate = VoiceChatSettings.SampleRate;
    private const int TargetChannels = 1;

    private static readonly Dictionary<string, AudioClip> ClipCache = new(StringComparer.OrdinalIgnoreCase);

    public static string AudioDirectory => Plugin.Singleton.Config.AudioReferences;

    /// <summary>
    /// <paramref name="fileName"/> をデコードして <see cref="AudioClip"/> を作る。
    /// 同じ <paramref name="clipName"/> が既にキャッシュされていればそれを返す。
    /// </summary>
    public static AudioClip LoadFromFile(string fileName, string? clipName = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Audio file name cannot be empty.", nameof(fileName));

        clipName ??= fileName;

        if (TryGetCached(clipName, out var cached))
            return cached!;

        var clip = CreateFromSamples(GetSamplesFromFile(fileName), clipName);
        ClipCache[clipName] = clip;
        return clip;
    }

    /// <summary>モノラル PCM サンプル列から <see cref="AudioClip"/> を作る（キャッシュしない）。</summary>
    public static AudioClip CreateFromSamples(float[] samples, string clipName = "CustomClip")
    {
        if (samples == null || samples.Length == 0)
            throw new ArgumentException("Samples cannot be empty.", nameof(samples));

        if (string.IsNullOrWhiteSpace(clipName))
            clipName = "CustomClip";

        var clip = AudioClip.Create(clipName, samples.Length, TargetChannels, TargetSampleRate, stream: false);
        if (clip == null)
            throw new InvalidOperationException($"AudioClip.Create returned null for '{clipName}'.");

        if (!clip.SetData(samples, 0))
        {
            UnityEngine.Object.Destroy(clip);
            throw new InvalidOperationException(
                $"AudioClip.SetData failed for '{clipName}' (samples={samples.Length}, channels={TargetChannels}, freq={TargetSampleRate}).");
        }

        return clip;
    }

    /// <summary>キャッシュ済みクリップ。無ければ null。破棄済みのエントリは掃除して null を返す。</summary>
    public static AudioClip? GetCached(string clipName)
        => TryGetCached(clipName, out var clip) ? clip : null;

    /// <summary>外部で作ったクリップをキャッシュへ登録する。同名の既存クリップは破棄される。</summary>
    public static void CacheClip(string clipName, AudioClip clip)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            throw new ArgumentException("Clip name cannot be empty.", nameof(clipName));

        if (clip == null)
            throw new ArgumentNullException(nameof(clip));

        if (ClipCache.TryGetValue(clipName, out var existing) && existing != clip)
            DestroyClip(existing);

        ClipCache[clipName] = clip;
    }

    /// <summary>キャッシュから外し、ネイティブクリップも破棄する。</summary>
    public static bool RemoveCached(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return false;

        if (!ClipCache.TryGetValue(clipName, out var clip))
            return false;

        DestroyClip(clip);
        return ClipCache.Remove(clipName);
    }

    /// <summary>キャッシュを空にする。<paramref name="destroyClips"/> が true ならクリップも破棄する。</summary>
    public static void ClearCache(bool destroyClips = true)
    {
        if (destroyClips)
        {
            foreach (var clip in ClipCache.Values)
                DestroyClip(clip);
        }

        ClipCache.Clear();
    }

    public static IEnumerable<string> GetCachedClipNames()
        => ClipCache.Keys.ToArray();

    /// <summary>音声ファイルをモノラル 48kHz の PCM サンプル列へデコードする。</summary>
    public static float[] GetSamplesFromFile(string fileName, string? clipName = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Audio file name cannot be empty.", nameof(fileName));

        var fullPath = Path.Combine(AudioDirectory, fileName);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Audio file not found: {fullPath}", fullPath);

        var samples = FfmpegAudioDecoder.DecodeToMono48k(fullPath);
        if (samples.Length == 0)
            throw new InvalidOperationException($"Decoded audio is empty: {fullPath}");

        return samples;
    }

    /// <summary><see cref="AudioClip"/> から PCM サンプル列を読み出す。</summary>
    public static float[] GetSamplesFromClip(AudioClip clip)
    {
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));

        var data = new float[clip.samples * clip.channels];
        if (!clip.GetData(data, 0))
            throw new InvalidOperationException($"AudioClip.GetData failed for '{clip.name}'.");

        return data;
    }

    /// <summary>
    /// キャッシュを引く。ネイティブ側が破棄済みのエントリは取り除いて false を返す。
    /// </summary>
    private static bool TryGetCached(string clipName, out AudioClip? clip)
    {
        clip = null;
        if (string.IsNullOrWhiteSpace(clipName))
            return false;

        if (!ClipCache.TryGetValue(clipName, out var cached))
            return false;

        // UnityEngine.Object は破棄されると == null が true になる（参照は残る）。
        if (cached == null)
        {
            ClipCache.Remove(clipName);
            return false;
        }

        clip = cached;
        return true;
    }

    private static void DestroyClip(AudioClip? clip)
    {
        if (clip == null)
            return;

        try
        {
            UnityEngine.Object.Destroy(clip);
        }
        catch (Exception ex)
        {
            Log.Warn($"[AudioClipApi] Failed to destroy clip '{clip.name}': {ex.Message}");
        }
    }
}
