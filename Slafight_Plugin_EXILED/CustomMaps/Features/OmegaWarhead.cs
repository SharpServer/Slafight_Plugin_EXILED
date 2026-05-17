using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Changes;
using Slafight_Plugin_EXILED.SpecialEvents;
using UnityEngine;
using EventHandler = Slafight_Plugin_EXILED.MainHandlers.EventHandler;

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
    private static Action<string, string, Vector3, bool, Transform, bool, float, float> CreateAndPlayAudio = EventHandler.CreateAndPlayAudio;

    public static event EventHandler<OmegaWarheadStartingEventArgs> OmegaWarheadStarting;

    public static bool CanBeStart() => !IsWarheadStarted && SpecialEventsHandler.IsWarheadable();

    public static bool StartProtocol(float triggerTime = 0f, Player startedBy = null)
    {
        Log.Debug("[OMEGA WARHEAD]Called Start Protocol.");
        if (!CanBeStart()) return false;
        if (Warhead.IsInProgress) Warhead.Stop();
        EventHandler.Instance.DeadmanDisable = true;
        Warhead.IsLocked = true;

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
            $"O5評議会の指令に基づいた操作により、<color=blue>OMEGA WARHEAD</color>シーケンスが開始されました。施設の全てを{Plugin.Singleton.Config.OwBoomTime}秒後に爆破します。<split>直ちに施設外に避難してください。",
            true);
        
        EscapeHandler.AddEscapeOverride(p => new EscapeHandler.EscapeTargetRole { Vanilla = RoleTypeId.Spectator });

        CreateAndPlayAudio("omega_v2.ogg", "OmegaWarhead", Vector3.zero, true, null, false, 999999999, 0);

        yield return Timing.WaitForSeconds(Plugin.Singleton.Config.OwBoomTime);

        if (!Round.InProgress) yield break;

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
        IsWarheadStarted = false;
        StartedPlayer = null;
    }

    public static void Shutdown()
    {
        Reset();
        OmegaWarheadStarting = null;
    }
}
