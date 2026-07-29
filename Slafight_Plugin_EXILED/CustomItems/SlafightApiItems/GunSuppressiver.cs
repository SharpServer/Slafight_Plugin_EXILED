using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.Handlers;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class GunSuppressiver : CItemWeapon
{
    private const string ShotClip = "GunSuppressiver.ogg";
    private const string SpeakerPrefix = "GunSuppressiver_";

    /// <summary>プレイヤー 1 人あたりのスピーカー数。連射時の音の重なりぶんだけ確保する。</summary>
    private const int ShotVoices = 2;

    private const float ShotRange = 15f;

    /// <summary>
    /// 同一発砲で SendingGunSound が複数回飛ぶ（発砲音・機構音）ため、
    /// これより短い間隔の再生要求は同じ 1 発とみなして捨てる。
    /// FSP9 のサイクルレートより十分短い値。
    /// </summary>
    private const float MinShotInterval = 0.04f;

    /// <summary>プレイヤー ID → 最後に発砲音を鳴らした <see cref="Time.time"/>。</summary>
    private readonly Dictionary<int, float> _lastShotTime = new();

    public override string DisplayName => "Suppressiver";
    public override string Description => string.Empty;

    protected override string UniqueKey => "GunSuppressiver";
    protected override ItemType BaseItem => ItemType.GunFSP9;

    protected override float Damage => 30f;
    protected override byte MagazineSize => 42;
    protected override Vector3 Scale => new(1f, 1f, 1.15f);

    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => CustomColor.ChaoticGreen.ToUnityColor();

    public override void RegisterEvents()
    {
        SpeakerApi.TryLoadClip(ShotClip);
        Player.SendingGunSound += OnSendingGunSound;
        Player.Left += OnPlayerLeft;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Player.SendingGunSound -= OnSendingGunSound;
        Player.Left -= OnPlayerLeft;
        ClearShotSpeakers();
        base.UnregisterEvents();
    }

    protected override void OnWaitingForPlayers()
    {
        ClearShotSpeakers();
        base.OnWaitingForPlayers();
    }

    private void OnSendingGunSound(SendingGunSoundEventArgs ev)
    {
        if (!Check(ev.Item) || ev.AudioIndex is not (0 or 1 or 2)) return;
        ev.IsAllowed = false;

        if (ev.Player == null) return;

        int playerId = ev.Player.Id;
        if (_lastShotTime.TryGetValue(playerId, out float last) && Time.time - last < MinShotInterval)
            return;

        _lastShotTime[playerId] = Time.time;

        // スピーカーは撃つたびに作らず、プレイヤーごとに ShotVoices 個を使い回す。
        SpeakerApi.PlayOneShot(
            ShotClip,
            SpeakerPrefix + playerId,
            ev.SendingPosition,
            voices: ShotVoices,
            isSpatial: true,
            maxDistance: ShotRange);
    }

    private void OnPlayerLeft(LeftEventArgs ev)
    {
        if (ev.Player == null) return;

        SpeakerApi.TryDestroy(SpeakerPrefix + ev.Player.Id);
        _lastShotTime.Remove(ev.Player.Id);
    }

    private void ClearShotSpeakers()
    {
        foreach (string name in SpeakerApi.GetAudioPlayerNames()
                     .Where(n => n.StartsWith(SpeakerPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            SpeakerApi.TryDestroy(name);
        }

        _lastShotTime.Clear();
    }
}
