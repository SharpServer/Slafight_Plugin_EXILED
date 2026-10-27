using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

using Log = Exiled.API.Features.Log;
using PlayerHandlers = Exiled.Events.Handlers.Player;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// <see cref="CustomFirearm.GunSounds"/> を実際の銃声へ適用します。
/// </summary>
public sealed class GunSoundHandler : EventHandlerBase
{
    private const string SpeakerPrefix = "Slafight_GunSound_";

    /// <summary>スピーカー名ごとの最終再生時刻です。</summary>
    private readonly Dictionary<string, float> lastPlayedAt = new();

    /// <summary>クリップ一覧をログへ出し済みの (型, 銃種) です。</summary>
    private readonly HashSet<(Type, ItemType)> loggedClipTables = [];

    public override void RegisterEvents()
    {
        PlayerHandlers.SendingGunSound += OnSendingGunSound;
        PlayerHandlers.ReceivingGunSound += OnReceivingGunSound;
    }

    public override void UnregisterEvents()
    {
        PlayerHandlers.SendingGunSound -= OnSendingGunSound;
        PlayerHandlers.ReceivingGunSound -= OnReceivingGunSound;
        DestroySpeakers(_ => true);
    }

    public override void OnPlayerLeft(PlayerLeftEventArgs ev)
    {
        if (ev.Player?.ReferenceHub == null)
            return;

        string suffix = "_" + ev.Player.NetworkId;
        DestroySpeakers(name => name.EndsWith(suffix, StringComparison.Ordinal));
    }

    public override void OnServerRoundRestarted() => DestroySpeakers(_ => true);

    private void OnSendingGunSound(SendingGunSoundEventArgs ev)
    {
        if (Find(ev.Firearm) is not { } custom)
            return;

        GunSoundTable table = custom.SoundTable;
        if (table.LogClips)
            LogSound(custom, ev);

        if (!table.TryResolve(ev.Firearm, ev.AudioIndex, ev.MixerChannel, out var sound, out string key))
            return;

        ev.IsAllowed = false;

        if (ev.Player == null || string.IsNullOrWhiteSpace(sound.Audio))
            return;

        string speaker = $"{SpeakerPrefix}{custom.GetType().Name}_{key}_{ev.Player.NetId}";
        if (lastPlayedAt.TryGetValue(speaker, out float last) && Time.time - last < sound.MinInterval)
            return;

        lastPlayedAt[speaker] = Time.time;
        SpeakerApi.PlayOneShot(
            sound.Audio,
            speaker,
            ev.SendingPosition,
            voices: sound.Voices,
            isSpatial: sound.IsSpatial,
            maxDistance: sound.Range,
            volume: sound.Volume);
    }

    private static void OnReceivingGunSound(ReceivingGunSoundEventArgs ev)
    {
        if (Find(ev.Firearm)?.SoundTable.TryResolve(ev.Firearm, ev.AudioIndex, ev.MixerChannel, out _, out _) == true)
            ev.IsAllowed = false;
    }

    /// <summary>この銃の CustomFirearm です。Hybrid なら現在のモードを返します。</summary>
    private static CustomFirearm Find(Firearm firearm)
    {
        if (firearm == null)
            return null;

        CustomItem custom = CustomItem.Of(firearm.Serial);
        if (custom is CustomHybrid hybrid)
            custom = hybrid.ActiveItem(firearm.Serial);

        return custom is CustomFirearm { SoundTable: { IsEmpty: false } or { LogClips: true } } firearmItem
            ? firearmItem
            : null;
    }

    private void LogSound(CustomFirearm custom, SendingGunSoundEventArgs ev)
    {
        string owner = custom.GetType().Name;

        if (loggedClipTables.Add((custom.GetType(), ev.Firearm.Type)))
        {
            var clips = GunSoundResolver.DumpClips(ev.Firearm);
            Log.Info(clips.Count == 0
                ? $"[{owner}] {ev.Firearm.Type}: AudioModule のクリップ一覧を取得できませんでした。"
                : $"[{owner}] {ev.Firearm.Type} registered clips ({clips.Count}):\n  " + string.Join("\n  ", clips));
        }

        Log.Info(
            $"[{owner}] {ev.Firearm.Type} sound: index={ev.AudioIndex}" +
            $" clip='{GunSoundResolver.GetClipName(ev.Firearm, ev.AudioIndex) ?? "<unknown>"}'" +
            $" kind={GunSoundResolver.Resolve(ev.Firearm, ev.AudioIndex, ev.MixerChannel)?.ToString() ?? "<none>"}" +
            $" channel={ev.MixerChannel} range={ev.Range}");
    }

    private void DestroySpeakers(Func<string, bool> match)
    {
        foreach (string name in SpeakerApi.GetAudioPlayerNames()
                     .Where(name => name.StartsWith(SpeakerPrefix, StringComparison.Ordinal) && match(name))
                     .ToArray())
        {
            SpeakerApi.TryDestroy(name);
        }

        foreach (string name in lastPlayedAt.Keys.Where(match).ToArray())
            lastPlayedAt.Remove(name);
    }
}
