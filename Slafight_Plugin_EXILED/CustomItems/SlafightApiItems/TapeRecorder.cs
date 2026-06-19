using System.Collections.Generic;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class TapeRecorder : CItemUsable
{
    public override string DisplayName => "Tape Recorder";
    public override string Description => "W.I.P";
    protected override string UniqueKey => "TapeRecorder";
    protected override ItemType BaseItem => ItemType.Medkit;
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.yellow;
    protected override int MaxUseCount => 0;

    protected override void OnUsedEffect(UsingItemCompletedEventArgs ev)
    {
        if (!VoiceRecordingApi.TryGetRecording($"TapeRecorder_{ev.Item.Serial}", out var recording))
        {
            VoiceRecordingApi.StartAreaRecording($"TapeRecorder_{ev.Item.Serial}", ev.Player.Position, 100f, maxDuration: 8f, playerFilter: p => ev.Player == p);
            ev.Player.ShowHint("<color=yellow>録音を開始しました(8秒間)</color>");
            Timing.CallDelayed(8f, () => ev.Player.ShowHint("<color=yellow>録音を完了しました</color>"));
        }
        else
        {
            ev.Player.ShowHint("<color=yellow>再生を開始します・・・</color>");
            VoiceRecordingApi.Play(recording, ev.Player.Position, $"TapeRecorderPlay_{ev.Player.NetId}", isSpatial: true);
        }
    }
}