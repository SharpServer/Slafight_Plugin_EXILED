using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using NVorbis;
using UnityEngine;
using VoiceChat;

namespace Slafight_Plugin_EXILED.API.Features;

public static class AudioClipApi
{
    private const int TargetSampleRate = VoiceChatSettings.SampleRate;
    private static readonly Dictionary<string, object> ClipCache = new(StringComparer.OrdinalIgnoreCase);

    public static string AudioDirectory => Plugin.Singleton.Config.AudioReferences;

    // UnityEngine.AudioClip を文字列から解決
    private static Type GetAudioClipType()
    {
        var t =
            Type.GetType("UnityEngine.AudioClip, UnityEngine.CoreModule", false) ??
            Type.GetType("UnityEngine.AudioClip, UnityEngine", false) ??
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEngine.AudioClip", false))
                .FirstOrDefault(x => x != null);

        if (t == null)
            throw new MissingFieldException("UnityEngine.AudioClip type not found.");
        return t;
    }

    private static object CreateClipObject(string name, int samples, int channels, int frequency, bool stream)
    {
        var audioClipType = GetAudioClipType();

        var method = audioClipType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m =>
            {
                if (m.Name != "Create")
                    return false;

                var p = m.GetParameters();
                return p.Length == 5 &&
                       p[0].ParameterType == typeof(string) &&
                       p[1].ParameterType == typeof(int) &&
                       p[2].ParameterType == typeof(int) &&
                       p[3].ParameterType == typeof(int) &&
                       p[4].ParameterType == typeof(bool);
            });

        if (method == null)
            throw new MissingMethodException("AudioClip.Create(string, int, int, int, bool) not found.");

        Log.Info($"[AudioClipApi] CreateClipObject name={name}, samples={samples}, ch={channels}, freq={frequency}, stream={stream}");

        var clip = method.Invoke(null, new object[] { name, samples, channels, frequency, stream });
        if (clip == null)
            throw new InvalidOperationException("AudioClip.Create returned null.");

        // 生成されたクリップの状態を確認
        var samplesProp = audioClipType.GetProperty("samples", BindingFlags.Public | BindingFlags.Instance);
        var channelsProp = audioClipType.GetProperty("channels", BindingFlags.Public | BindingFlags.Instance);
        var freqProp = audioClipType.GetProperty("frequency", BindingFlags.Public | BindingFlags.Instance);

        int clipSamples = samplesProp != null ? Convert.ToInt32(samplesProp.GetValue(clip)) : -1;
        int clipChannels = channelsProp != null ? Convert.ToInt32(channelsProp.GetValue(clip)) : -1;
        int clipFreq = freqProp != null ? Convert.ToInt32(freqProp.GetValue(clip)) : -1;

        Log.Info($"[AudioClipApi] Created clip: samples={clipSamples}, channels={clipChannels}, freq={clipFreq}");

        return clip;
    }

    private static void SetClipData(object clip, float[] data, int offsetSamples)
    {
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));
        if (data == null || data.Length == 0)
            throw new ArgumentException("SetClipData data is null or empty.", nameof(data));

        var clipType = clip.GetType();

        var samplesProp = clipType.GetProperty("samples", BindingFlags.Public | BindingFlags.Instance);
        var channelsProp = clipType.GetProperty("channels", BindingFlags.Public | BindingFlags.Instance);

        int clipSamples = samplesProp != null ? Convert.ToInt32(samplesProp.GetValue(clip)) : -1;
        int clipChannels = channelsProp != null ? Convert.ToInt32(channelsProp.GetValue(clip)) : -1;

        Log.Info($"[AudioClipApi] SetClipData: dataLen={data.Length}, offset={offsetSamples}, clip.samples={clipSamples}, clip.channels={clipChannels}");

        if (clipChannels <= 0)
            throw new InvalidOperationException("AudioClip.SetData failed; clip has no channels.");

        if (offsetSamples < 0 || offsetSamples >= clipSamples)
            throw new ArgumentException($"AudioClip.SetData failed; invalid offsetSamples={offsetSamples}");

        // Unity 的には data.Length + offsetSamples <= clip.samples である必要がある
        if (offsetSamples + data.Length > clipSamples)
            throw new ArgumentException($"AudioClip.SetData failed; data too long. dataLen={data.Length}, offset={offsetSamples}, clip.samples={clipSamples}");

        var method = clipType.GetMethod(
            "SetData",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new[] { typeof(float[]), typeof(int) },
            null);

        if (method == null)
            throw new MissingMethodException("AudioClip.SetData(float[], int) not found.");

        var ok = method.Invoke(clip, new object[] { data, offsetSamples });

        Log.Info($"[AudioClipApi] SetData.Invoke result={ok}");

        if (ok is bool b && !b)
            throw new InvalidOperationException("AudioClip.SetData returned false.");
    }

    public static object LoadFromFile(string fileName, string? clipName = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Audio file name cannot be empty.", nameof(fileName));

        clipName ??= fileName;

        if (ClipCache.TryGetValue(clipName, out var cachedClip) && cachedClip != null)
            return cachedClip;

        var fullPath = Path.Combine(AudioDirectory, fileName);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Audio file not found: {fullPath}", fullPath);

        var clip = CreateFromVorbisFile(fullPath, clipName);
        ClipCache[clipName] = clip;
        return clip;
    }

    public static object CreateFromSamples(float[] samples, string clipName = "CustomClip")
    {
        if (samples == null || samples.Length == 0)
            throw new ArgumentException("Samples cannot be empty.", nameof(samples));

        if (string.IsNullOrWhiteSpace(clipName))
            clipName = "CustomClip";

        var clip = CreateClipObject(clipName, samples.Length, 1, TargetSampleRate, false);
        SetClipData(clip, samples, 0);
        return clip;
    }

    private static object CreateFromVorbisFile(string fullPath, string clipName)
    {
        Log.Info($"[AudioClipApi] CreateFromVorbisFile path={fullPath}, clipName={clipName}");

        using var reader = new VorbisReader(fullPath);

        int channels = Math.Max(1, reader.Channels);
        int sampleRate = reader.SampleRate;

        long totalSamplesLong = reader.TotalSamples * channels;
        int totalSamples;
        try
        {
            totalSamples = checked((int)totalSamplesLong);
        }
        catch (OverflowException)
        {
            throw new InvalidOperationException($"Audio file is too large: {fullPath} (TotalSamples*channels={totalSamplesLong})");
        }

        Log.Info($"[AudioClipApi] Vorbis: channels={channels}, sampleRate={sampleRate}, totalSamples={totalSamples}");

        var allSamples = new float[totalSamples];
        var read = reader.ReadSamples(allSamples, 0, allSamples.Length);
        Log.Info($"[AudioClipApi] Vorbis: read={read}, bufferLen={allSamples.Length}");

        if (read <= 0)
            throw new InvalidOperationException($"Failed to read audio samples: {fullPath}");

        if (read < allSamples.Length)
            Array.Resize(ref allSamples, read);

        var mono = ConvertToMono48k(allSamples, sampleRate, channels);
        Log.Info($"[AudioClipApi] ConvertToMono48k: monoLen={mono.Length}, targetSampleRate={TargetSampleRate}");

        if (mono.Length == 0)
            throw new InvalidOperationException($"Converted audio is empty: {fullPath}");

        var clip = CreateClipObject(clipName, mono.Length, 1, TargetSampleRate, false);
        SetClipData(clip, mono, 0);
        return clip;
    }

    public static object? GetCached(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return null;

        return ClipCache.TryGetValue(clipName, out var clip) ? clip : null;
    }

    public static void CacheClip(string clipName, object clip)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            throw new ArgumentException("Clip name cannot be empty.", nameof(clipName));

        if (clip == null)
            throw new ArgumentNullException(nameof(clip));

        ClipCache[clipName] = clip;
    }

    public static bool RemoveCached(string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return false;

        return ClipCache.Remove(clipName);
    }

    public static void ClearCache()
    {
        ClipCache.Clear();
    }

    public static IEnumerable<string> GetCachedClipNames()
    {
        return ClipCache.Keys.ToArray();
    }

    public static float[] GetSamplesFromFile(string fileName, string? clipName = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Audio file name cannot be empty.", nameof(fileName));

        var fullPath = Path.Combine(AudioDirectory, fileName);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Audio file not found: {fullPath}", fullPath);

        using var reader = new VorbisReader(fullPath);

        int channels = Math.Max(1, reader.Channels);
        int sampleRate = reader.SampleRate;

        long totalSamplesLong = reader.TotalSamples * channels;
        int totalSamples;
        try
        {
            totalSamples = checked((int)totalSamplesLong);
        }
        catch (OverflowException)
        {
            throw new InvalidOperationException($"Audio file is too large: {fullPath} (TotalSamples*channels={totalSamplesLong})");
        }

        Log.Info($"[AudioClipApi] GetSamplesFromFile Vorbis: channels={channels}, sampleRate={sampleRate}, totalSamples={totalSamples}");

        var allSamples = new float[totalSamples];
        var read = reader.ReadSamples(allSamples, 0, allSamples.Length);
        Log.Info($"[AudioClipApi] GetSamplesFromFile read={read}, bufferLen={allSamples.Length}");

        if (read <= 0)
            throw new InvalidOperationException($"Failed to read audio samples: {fullPath}");

        if (read < allSamples.Length)
            Array.Resize(ref allSamples, read);

        return ConvertToMono48k(allSamples, sampleRate, channels);
    }

    public static float[] GetSamplesFromClip(object clip)
    {
        if (clip == null)
            throw new ArgumentNullException(nameof(clip));

        var type = clip.GetType();
        var samplesProp = type.GetProperty("samples", BindingFlags.Public | BindingFlags.Instance);
        if (samplesProp == null)
            throw new MissingMemberException("AudioClip.samples not found.");

        int samples = Convert.ToInt32(samplesProp.GetValue(clip));
        var data = new float[samples];

        var getData = type.GetMethod("GetData", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(float[]), typeof(int) }, null);
        if (getData == null)
            throw new MissingMethodException("AudioClip.GetData(float[], int) not found.");

        var ok = getData.Invoke(clip, new object[] { data, 0 });
        if (ok is bool b && !b)
            throw new InvalidOperationException("AudioClip.GetData failed.");

        return data;
    }

    private static float[] ConvertToMono48k(float[] input, int sampleRate, int channels)
    {
        if (input == null || input.Length == 0)
            return Array.Empty<float>();

        channels = Math.Max(1, channels);
        int frameCount = input.Length / channels;
        var mono = new float[frameCount];

        for (int frame = 0; frame < frameCount; frame++)
        {
            float sample = 0f;
            int offset = frame * channels;
            for (int channel = 0; channel < channels; channel++)
                sample += input[offset + channel];

            mono[frame] = sample / channels;
        }

        if (sampleRate <= 0 || sampleRate == TargetSampleRate)
            return mono;

        int outputLength = Math.Max(1, Mathf.RoundToInt(mono.Length * (TargetSampleRate / (float)sampleRate)));
        var output = new float[outputLength];
        float ratio = (mono.Length - 1) / (float)Math.Max(1, outputLength - 1);

        for (int i = 0; i < outputLength; i++)
        {
            float source = i * ratio;
            int left = Mathf.FloorToInt(source);
            int right = Math.Min(left + 1, mono.Length - 1);
            output[i] = Mathf.Lerp(mono[left], mono[right], source - left);
        }

        return output;
    }
}