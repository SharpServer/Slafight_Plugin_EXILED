using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.Changes;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;
using Slafight_Plugin_EXILED.SpecialEvents;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.Features;

public class OmegaWarheadStartingEventArgs : EventArgs
{
    /// <summary>
    /// 起動しようとしているプレイヤー
    /// </summary>
    public Player Player { get; }
    public bool IsAllowed { get; set; }
    public OmegaWarheadStartingEventArgs(Player player, bool isAllowed)
    {
        Player = player;
        IsAllowed = isAllowed;
    }
}

public static class OmegaWarhead
{
    private static CoroutineHandle _warheadCoroutine;

    public static bool IsWarheadStarted;
    public static Player StartedPlayer;

    private static SpecialEventsHandler SpecialEventsHandler => SpecialEventsHandler.Instance;
    public static event EventHandler<OmegaWarheadStartingEventArgs> OmegaWarheadStarting;
    public static event Action OmegaWarheadDetonating;

    public static bool CanBeStart() => !IsWarheadStarted && SpecialEventsHandler.IsWarheadable();

    public static bool StartProtocol(float triggerTime = 0f, Player startedBy = null)
    {
        Log.Debug("[OMEGA WARHEAD]Called Start Protocol.");
        if (!CanBeStart()) return false;
        if (Warhead.IsInProgress) Warhead.Stop();
        RoundHazardController.SetDeadmanSwitchBlocked(true);
        RoundHazardController.SetAlphaWarheadDisarmLocked(true);

        if (_warheadCoroutine.IsRunning)
            Timing.KillCoroutines(_warheadCoroutine);

        _warheadCoroutine = Timing.RunCoroutine(WarheadSequence(triggerTime, startedBy));
        return true;
    }

    private static IEnumerator<float> WarheadSequence(float triggerTime, Player startedBy)
    {
        if (triggerTime > 0f)
            yield return Timing.WaitForSeconds(triggerTime);

        if (!Round.InProgress || IsWarheadStarted) yield break;

        var ev = new OmegaWarheadStartingEventArgs(startedBy, true);
        OmegaWarheadStarting?.Invoke(null, ev);
        if (!ev.IsAllowed) yield break;

        StartedPlayer = startedBy;
        IsWarheadStarted = true;
        AlarmLight.SetOmegaAlarmForAll(true);

        foreach (Room room in Room.List)
            room.Color = Color.blue;

        foreach (Door door in Door.List)
        {
            if (door.Type != DoorType.ElevatorGateA &&
                door.Type != DoorType.ElevatorGateB &&
                door.Type != DoorType.ElevatorLczA &&
                door.Type != DoorType.ElevatorLczB &&
                door.Type != DoorType.ElevatorNuke &&
                door.Type != DoorType.ElevatorScp049 &&
                door.Type != DoorType.ElevatorServerRoom)
            {
                door.IsOpen = true;
                door.PlaySound(DoorBeepType.InteractionAllowed);
                door.Lock(DoorLockType.Warhead);
            }
        }

        Exiled.API.Features.Cassie.MessageTranslated(
            $"By Order of O5 Command . Omega Warhead Sequence Activated . All Facility Detonated in T MINUS {Plugin.Singleton.Config.OwBoomTime} Seconds. Please evacuate to outside immediately .",
            $"O5評議会の指令に基づいた操作により、<color=blue>OMEGA WARHEAD</color>シーケンスが開始されました。施設の全てを{Plugin.Singleton.Config.OwBoomTime}秒後に爆破します。<split><b>直ちに脱出口を用いて施設外に避難してください。</b>",
            true);
        
        EvacuationRoundEndState.Begin();
        EscapeHandler.AddEscapeOverride(p => new EscapeHandler.EscapeTargetRole { Vanilla = RoleTypeId.Spectator });

        SpeakerApi.Play("omega_v2.ogg", "OmegaWarhead", Vector3.zero, true, null, false, 999999999, 0);

        var boomTime = Math.Max(0f, Plugin.Singleton.Config.OwBoomTime);
        var preLockWait = Math.Max(0f, boomTime - 5f);

        if (preLockWait > 0f)
            yield return Timing.WaitForSeconds(preLockWait);

        if (!Round.InProgress) yield break;

        WarheadDoorLockdown.LockAllDoorsClosed();

        var finalWait = Math.Min(5f, boomTime);
        if (finalWait > 0f)
            yield return Timing.WaitForSeconds(finalWait);

        if (!Round.InProgress) yield break;

        OmegaWarheadDetonating?.Invoke();
        AlphaWarheadController.Singleton.RpcShake(false);
        foreach (var player in Player.List)
        {
            if (!player.IsAlive)
                continue;

            player.ExplodeEffect(ProjectileType.FragGrenade);
            player.Kill("OMEGA WARHEADに爆破された");
        }
    }

    public static void Reset()
    {
        if (_warheadCoroutine.IsRunning)
            Timing.KillCoroutines(_warheadCoroutine);
        AlarmLight.SetOmegaAlarmForAll(false);
        EvacuationRoundEndState.End();
        IsWarheadStarted = false;
        StartedPlayer = null;
    }

    public static void Shutdown()
    {
        Reset();
        OmegaWarheadStarting = null;
        OmegaWarheadDetonating = null;
    }
}
