using System;
using System.Globalization;
using System.Linq;
using System.Text;
using CommandSystem;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using VoiceChat;

namespace Slafight_Plugin_EXILED.Commands.DevTools;

public class VoiceRecordingCommand : ICommand
{
    public string Command => "voicerec";
    public string[] Aliases { get; } = ["vrec", "recordvoice"];
    public string Description => "Test VoiceRecordingApi at your current position.";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!CommandTools.CheckPermission(sender, Command, out response))
            return false;

        if (!CommandTools.TryGetExecutor(sender, out var executor, out response))
            return false;

        if (arguments.Count < 1)
        {
            response = BuildUsage();
            return false;
        }

        switch (arguments.At(0).ToLowerInvariant())
        {
            case "start":
            case "rec":
                return Start(arguments, executor, out response);

            case "stop":
                return Stop(arguments, executor, out response);

            case "play":
                return Play(arguments, executor, out response);

            case "list":
                return List(out response);

            case "clear":
                VoiceRecordingApi.ClearAll();
                response = "Cleared all voice recordings.";
                return true;

            default:
                response = BuildUsage();
                return false;
        }
    }

    private static bool Start(ArraySegment<string> arguments, Player executor, out string response)
    {
        var key = arguments.Count >= 2 ? arguments.At(1) : DefaultKey(executor);
        var radius = 4f;
        var duration = 10f;

        if (arguments.Count >= 3 && !TryParsePositive(arguments.At(2), out radius))
        {
            response = $"Invalid radius: {arguments.At(2)}";
            return false;
        }

        if (arguments.Count >= 4 && !TryParsePositive(arguments.At(3), out duration))
        {
            response = $"Invalid duration: {arguments.At(3)}";
            return false;
        }

        VoiceRecordingApi.StartAreaRecording(
            key,
            executor.Position,
            radius,
            duration,
            [VoiceChatChannel.Proximity, VoiceChatChannel.ScpChat, VoiceChatChannel.Radio]);

MeowExtensions.ShowHint(        executor, $"<color=red>● 録音中</color>\n<size=24>{key} / {duration:0.#}秒 / 半径{radius:0.#}m</size>", duration);
        Timing.CallDelayed(duration + 0.05f, () =>
        {
            if (executor == null || !executor.IsConnected)
                return;

            if (VoiceRecordingApi.TryGetRecording(key, out var recording) && !VoiceRecordingApi.IsRecording(key))
MeowExtensions.ShowHint(                executor, $"<color=green>録音完了！</color>\n<size=24>{key}: {recording.FrameCount} frames / {recording.DurationSeconds:0.##}s</size>", 5f);
        });

        response = $"Recording started: key={key}, radius={radius:0.##}, duration={duration:0.##}s";
        return true;
    }

    private static bool Stop(ArraySegment<string> arguments, Player executor, out string response)
    {
        var key = arguments.Count >= 2 ? arguments.At(1) : DefaultKey(executor);
        if (!VoiceRecordingApi.StopRecording(key))
        {
            response = $"Recording session not found: {key}";
            return false;
        }

        VoiceRecordingApi.TryGetRecording(key, out var recording);
MeowExtensions.ShowHint(        executor, $"<color=green>録音完了！</color>\n<size=24>{key}: {recording?.FrameCount ?? 0} frames</size>", 5f);
        response = $"Recording stopped: {key}. Frames={recording?.FrameCount ?? 0}, Duration={recording?.DurationSeconds ?? 0f:0.##}s";
        return true;
    }

    private static bool Play(ArraySegment<string> arguments, Player executor, out string response)
    {
        var key = arguments.Count >= 2 ? arguments.At(1) : DefaultKey(executor);
        var maxDistance = 12f;

        if (arguments.Count >= 3 && !TryParsePositive(arguments.At(2), out maxDistance))
        {
            response = $"Invalid maxDistance: {arguments.At(2)}";
            return false;
        }

        if (!VoiceRecordingApi.TryGetRecording(key, out var recording))
        {
            response = $"Recording not found: {key}";
            return false;
        }

        VoiceRecordingApi.Play(key, executor.Position, maxDistance: maxDistance);
MeowExtensions.ShowHint(        executor, $"<color=yellow>録音再生中</color>\n<size=24>{key}</size>", Mathf.Max(2f, recording.DurationSeconds + 1f));
        response = $"Playing recording: {key}. Frames={recording.FrameCount}, Duration={recording.DurationSeconds:0.##}s, Hash={recording.Hash}";
        return true;
    }

    private static bool List(out string response)
    {
        if (VoiceRecordingApi.Recordings.Count == 0)
        {
            response = "No voice recordings.";
            return true;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Voice recordings:");
        foreach (var recording in VoiceRecordingApi.Recordings.Values.OrderBy(r => r.Key))
        {
            var state = VoiceRecordingApi.IsRecording(recording.Key) ? "recording" : "ready";
            sb.AppendLine($"  {recording.Key}: {state}, frames={recording.FrameCount}, duration={recording.DurationSeconds:0.##}s, hash={recording.Hash}");
        }

        response = sb.ToString().TrimEnd();
        return true;
    }

    private static string DefaultKey(Player player)
        => $"test_{player.Id}";

    private static bool TryParsePositive(string value, out float result)
        => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && result > 0f;

    private static string BuildUsage()
        => "Usage: sl voicerec <start [key] [radius] [duration]|stop [key]|play [key] [maxDistance]|list|clear>";
}
