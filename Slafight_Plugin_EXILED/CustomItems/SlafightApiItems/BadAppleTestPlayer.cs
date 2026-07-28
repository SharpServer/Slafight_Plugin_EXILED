using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

/// <summary>
/// Test CItem that plays the Bad Apple shadow animation on the Chaos Keycard's
/// SNAPI display. The decoded frames are shared by every copy of this item.
/// </summary>
public sealed class BadAppleTestPlayer : CItem
{
    private const string MediaSource = "https://www.nicovideo.jp/watch/sm8628149";
    private const float FramesPerSecond = 10f;
    private const int MaxFrames = 3000;

    private readonly Dictionary<ushort, CoroutineHandle> _pendingPlaybacks = new();
    private readonly HashSet<ushort> _activeSerials = new();
    private Task<IReadOnlyList<VideoFrameData>>? _frameLoadTask;

    public override string DisplayName => "Bad Apple!! Player [TEST]";
    public override string Description =>
        "選択するとChaos KeycardのSnake画面でBad Apple!!の影絵を再生するテストデバイス。";

    protected override string UniqueKey => "BadAppleTest";
    protected override ItemType BaseItem => ItemType.KeycardChaosInsurgency;

    public override void UnregisterEvents()
    {
        ClearRuntimeState();
        base.UnregisterEvents();
    }

    protected override void OnChangingItem(ChangingItemEventArgs ev)
    {
        base.OnChangingItem(ev);
        if (!ev.IsAllowed || ev.Player == null || ev.Item == null || !Check(ev.Item))
            return;

        QueuePlayback(ev.Player, ev.Item.Serial);
    }

    protected override void OnReleased(ItemRemovedEventArgs ev)
    {
        if (ev.Item != null)
            StopPlayback(ev.Item.Serial);

        base.OnReleased(ev);
    }

    protected override void OnOwnerDying(DyingEventArgs ev)
    {
        if (ev.Player != null)
        {
            foreach (var serial in ev.Player.Items
                         .Where(item => item != null && Check(item))
                         .Select(item => item.Serial)
                         .ToArray())
            {
                StopPlayback(serial);
            }
        }

        base.OnOwnerDying(ev);
    }

    protected override void OnSerialUntracked(ushort serial)
    {
        StopPlayback(serial);
        base.OnSerialUntracked(serial);
    }

    protected override void OnWaitingForPlayers()
    {
        ClearRuntimeState();
        base.OnWaitingForPlayers();
    }

    private void QueuePlayback(Player player, ushort serial)
    {
        if (_pendingPlaybacks.TryGetValue(serial, out var existing) && existing.IsRunning)
            return;

        StopPlayback(serial);
        _pendingPlaybacks[serial] = Timing.RunCoroutine(LoadAndPlay(player.Id, serial));
    }

    private IEnumerator<float> LoadAndPlay(int playerId, ushort serial)
    {
        yield return Timing.WaitForOneFrame;

        Player? player = Player.Get(playerId);
        if (!IsHeldBy(player, serial))
        {
            _pendingPlaybacks.Remove(serial);
            yield break;
        }

        Task<IReadOnlyList<VideoFrameData>> loadTask;
        try
        {
            loadTask = GetOrStartFrameLoad();
        }
        catch (Exception ex)
        {
            _pendingPlaybacks.Remove(serial);
            player!.ShowHint(BuildLoadError(ex), 6f);
            yield break;
        }

        while (!loadTask.IsCompleted)
        {
            if (!IsHeldBy(Player.Get(playerId), serial))
            {
                _pendingPlaybacks.Remove(serial);
                yield break;
            }

            yield return Timing.WaitForOneFrame;
        }

        _pendingPlaybacks.Remove(serial);
        player = Player.Get(playerId);
        if (!IsHeldBy(player, serial))
            yield break;

        if (loadTask.IsCanceled)
        {
            player!.ShowHint("<color=#ff7777>Bad Apple!!の読み込みがキャンセルされました。</color>", 5f);
            yield break;
        }

        if (loadTask.IsFaulted)
        {
            if (ReferenceEquals(_frameLoadTask, loadTask))
                _frameLoadTask = null;

            var error = loadTask.Exception?.GetBaseException() ??
                        new InvalidOperationException("Unknown media loading error.");
            player!.ShowHint(BuildLoadError(error), 6f);
            yield break;
        }

        try
        {
            SnakeImageApi.PlayFrames(serial, loadTask.Result, CreatePlaybackOptions());
            _activeSerials.Add(serial);
            player!.ShowHint(
                "<color=#ffffff><b>Bad Apple!!</b></color>\n" +
                "<color=#aaaaaa>Chaos Keycardを調べると再生画面を確認できます。</color>",
                4f);
        }
        catch (Exception ex)
        {
            player!.ShowHint(BuildLoadError(ex), 6f);
            Log.Error($"[BadAppleTestPlayer] Playback failed for serial {serial}: {ex}");
        }
    }

    private Task<IReadOnlyList<VideoFrameData>> GetOrStartFrameLoad()
    {
        if (_frameLoadTask is { IsCompletedSuccessfully: true } ||
            _frameLoadTask is { IsCompleted: false })
        {
            return _frameLoadTask;
        }

        _frameLoadTask = Task.Run<IReadOnlyList<VideoFrameData>>(() =>
            YtDlpApi.IsSupportedUrl(MediaSource)
                ? MediaProcessingApi.GetFramesFromUrl(
                    MediaSource,
                    SnakeImageOptions.NativeWidth,
                    SnakeImageOptions.NativeHeight,
                    FramesPerSecond,
                    MaxFrames,
                    VideoPixelFormat.BlackWhite8)
                : MediaProcessingApi.GetFramesFromFile(
                    MediaSource,
                    SnakeImageOptions.NativeWidth,
                    SnakeImageOptions.NativeHeight,
                    FramesPerSecond,
                    MaxFrames,
                    VideoPixelFormat.BlackWhite8));
        return _frameLoadTask;
    }

    private static SnakeImageOptions CreatePlaybackOptions() =>
        new()
        {
            FramesPerSecond = FramesPerSecond,
            MaxFrames = MaxFrames,
            Invert = true,
            Loop = true,
            StopWhenUnequipped = true,
            StopOnSnakeInput = false,
            RestoreSnakeOnStop = true,
            TakeOverOwnerSession = true,
        };

    private bool IsHeldBy(Player? player, ushort serial)
        => player != null &&
           player.IsConnected &&
           player.CurrentItem?.Serial == serial &&
           Check(player.CurrentItem);

    private void StopPlayback(ushort serial)
    {
        if (_pendingPlaybacks.Remove(serial, out var pending) && pending.IsRunning)
            Timing.KillCoroutines(pending);

        SnakeImageApi.Stop(serial);
        _activeSerials.Remove(serial);
    }

    private void ClearRuntimeState()
    {
        foreach (var pending in _pendingPlaybacks.Values.ToArray())
        {
            if (pending.IsRunning)
                Timing.KillCoroutines(pending);
        }

        _pendingPlaybacks.Clear();
        foreach (var serial in _activeSerials.ToArray())
            SnakeImageApi.Stop(serial);
        _activeSerials.Clear();
        _frameLoadTask = null;
    }

    private static string BuildLoadError(Exception error)
    {
        Log.Error($"[BadAppleTestPlayer] Failed to load Bad Apple media: {error}");
        return "<color=#ff7777>Bad Apple!!の読み込み・再生に失敗しました。</color>\n" +
               $"<size=18>{error.Message}</size>";
    }
}
