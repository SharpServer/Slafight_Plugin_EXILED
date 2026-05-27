using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Toys;
using Exiled.Events.EventArgs.Player;
using LabApi.Events.Arguments.PlayerEvents;
using MapGeneration;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.Features;

public static class TerminalRift
{
    private static bool _registered = false;
    private static TerminalRiftLabHandler? _labHandler;

    private static CoroutineHandle _animCoroutineHandle;
    private static CoroutineHandle _timeoutHandle;
    
    public const float PositionTolerance = 2.25f;
    
    public static SchematicObject RiftObject;
    public static Vector3 RiftObjectPosition;
    public static readonly List<SchematicObject> ControlObjects = [];

    // ★追加
    private static Waypoint? _riftWaypoint;

    public static bool Invoking { get; private set; } = false;
    public static void Register()
    {
        if (_registered) return;

        Log.Debug("[TerminalRift] Registering...");

        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
        Exiled.Events.Handlers.Server.RestartingRound += Cleanup;
        Exiled.Events.Handlers.Player.ReceivingEffect += CancelDeath;
        Exiled.Events.Handlers.Player.ChangingRole += OnChanging;
        Exiled.Events.Handlers.Player.Dying += CancelDeathForDying;

        _labHandler = LabApiHandlerRegistry.Register(_labHandler);
        _registered = true;

        Log.Debug("[TerminalRift] Registered OK");
    }

    public static void Unregister()
    {
        if (!_registered) return;

        Log.Debug("[TerminalRift] Unregistering...");

        KillAllCoroutines();

        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
        Exiled.Events.Handlers.Server.RestartingRound -= Cleanup;
        Exiled.Events.Handlers.Player.ReceivingEffect -= CancelDeath;
        Exiled.Events.Handlers.Player.ChangingRole -= OnChanging;
        Exiled.Events.Handlers.Player.Dying -= CancelDeathForDying;

        LabApiHandlerRegistry.Unregister(ref _labHandler);
        _registered = false;

        Log.Debug("[TerminalRift] Unregistered OK");
    }

    private static void KillAllCoroutines()
    {
        if (_animCoroutineHandle.IsRunning)
            Timing.KillCoroutines(_animCoroutineHandle);
        if (_timeoutHandle.IsRunning)
            Timing.KillCoroutines(_timeoutHandle);

        _animCoroutineHandle = default;
        _timeoutHandle = default;
    }

    private static void OnRoundStarted()
    {
        Timing.CallDelayed(1.5f, () => 
        {
            ControlObjects.Clear();
            foreach (var map in MapUtils.LoadedMaps.Values)
            {
                if (map.SpawnedObjects == null) continue;
                foreach (var meo in map.SpawnedObjects)
                {
                    if (meo.TryGetComponent(out SchematicObject schematic))
                    {
                        if (schematic.Name == "Rift")
                        {
                            RiftObject = schematic;
                            RiftObjectPosition = schematic.Position;

                            // ★RiftObjectの子としてWaypointToyを生成
                            // BoundsSizeはRiftプラットフォームの大きさに合わせて調整してください
                            _riftWaypoint = Waypoint.Create(
                                parent: RiftObject.transform,
                                position: Vector3.up * 1.05f,
                                scale: new Vector3(4.5f, 3.5f, 3.5f)//,
                                //visualizeBounds: true
                            );
                            Log.Debug("[TerminalRift] WaypointToy created on Rift.");
                        }
                        else if (schematic.Name == "TerminalControl")
                        {
                            ControlObjects.Add(schematic);
                        }
                    }
                }
            }
        });
    }

    private static void Cleanup()
    {
        ControlObjects.Clear();
        Invoking = false;
        KillAllCoroutines();
        RiftObject = null;
        RiftObjectPosition = Vector3.zero;

        // ★Waypointを破棄
        if (_riftWaypoint != null)
        {
            try { _riftWaypoint.Destroy(); } catch { /* ignored */ }
            _riftWaypoint = null;
            Log.Debug("[TerminalRift] WaypointToy destroyed.");
        }
    }

    public static void TryInvoke()
    {
        Log.Debug($"TryInvoke: Invoking={Invoking}");
        if (Invoking) return;

        if (Round.IsLobby || Round.IsEnded)
        {
            Log.Debug("TryInvoke: round is lobby/ended, ignore.");
            return;
        }
        
        Invoking = true;
        KillAllCoroutines();

        if (RiftObject == null || RiftObject.gameObject == null)
        {
            Log.Warn("TryInvoke: RiftObject null or destroyed");
            Invoking = false;
            return;
        }

        if (!ControlObjects.Any())
        {
            Log.Warn("TryInvoke: no control objects");
            Invoking = false;
            return;
        }
        
        SpeakerApi.Play("Moving.ogg", "RiftElevator", RiftObjectPosition, true, RiftObject.transform, false, 30f, 0);

        _animCoroutineHandle = Timing.RunCoroutine(AnimSet());
        _timeoutHandle = Timing.CallDelayed(50f, () => ForceReset("timeout"));
    }

    private static void ForceReset(string reason)
    {
        if (!Invoking) return;

        Log.Warn($"TerminalRift ForceReset ({reason})");
        Invoking = false;
        KillAllCoroutines();
    }

    private static void CancelDeath(ReceivingEffectEventArgs ev)
    {
        if (ev.Player == null)
            return;

        if (ev.Effect is not PitDeath)
            return;

        var currentRoomType = ev.Player.CurrentRoom?.Type.ToString();

        if (currentRoomType == "HczTestRoom" ||
            currentRoomType == "Surface" ||
            string.IsNullOrEmpty(currentRoomType))
        {
            ev.IsAllowed = false;
        }
    }

    private static void OnChanging(ChangingRoleEventArgs ev)
    {
        if (ev.Player == null || !ev.IsAllowed) return;
        try
        {
            ev.Player.DisableAllEffects();
            ev.Player.EnableEffect(EffectType.SpawnProtected, 3.5f);
        }
        catch
        {
            // ignored
        }
    }

    private static void CancelDeathForDying(DyingEventArgs ev)
    {
        if (ev.Player == null) return;
        if (ev.Player.IsEffectActive<PitDeath>())
        {
            if (ev.Player.CurrentRoom?.Type == RoomType.Surface)
            {
                ev.IsAllowed = false;
                ev.Player.DisableEffect<PitDeath>();
                ev.Player.Health = ev.Player.MaxHealth;
            }
        }
    }

    private static IEnumerator<float> AnimSet()
    {
        if (RiftObject?.gameObject == null || !ControlObjects.Any())
        {
            ForceReset("no rift/controls at AnimSet start");
            yield break;
        }

        if (Round.IsLobby || Round.IsEnded)
        {
            ForceReset("round ended at AnimSet start");
            yield break;
        }
        
        yield return Timing.WaitUntilDone(Anim(RiftObjectPosition, new Vector3(0f, -28.5f, 0f), 12.5f));

        if (!Invoking) yield break;

        yield return Timing.WaitForSeconds(0.2f);
        SpeakerApi.Play("Beep.ogg", "RiftElevator", RiftObjectPosition, true, RiftObject.transform, false, 30f, 0);

        if (Round.IsLobby || Round.IsEnded)
        {
            ForceReset("round ended after down anim");
            yield break;
        }

        yield return Timing.WaitForSeconds(7.5f);

        if (!Invoking) yield break;

        if (RiftObject.gameObject == null)
        {
            ForceReset("rift destroyed before up anim");
            yield break;
        }

        SpeakerApi.Play("Moving.ogg", "RiftElevator", RiftObjectPosition, true, RiftObject.transform, false, 30f, 0);
        yield return Timing.WaitUntilDone(AnimToPosition(RiftObject.Position, RiftObjectPosition, 12.5f));

        if (!Invoking) yield break;

        yield return Timing.WaitForSeconds(0.2f);
        SpeakerApi.Play("Beep.ogg", "RiftElevator", RiftObjectPosition, true, RiftObject.transform, false, 30f, 0);
        
        Invoking = false;
    }
    
    private static IEnumerator<float> Anim(Vector3 startpos, Vector3 offset, float duration)
    {
        Vector3 startPos = startpos;
        Vector3 endPos = startPos + offset;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            if (Round.IsLobby || Round.IsEnded)
            {
                ForceReset("round ended during down anim");
                yield break;
            }

            if (RiftObject?.gameObject == null)
            {
                Log.Warn("Rift invalid during down anim");
                ForceReset("rift destroyed during down anim");
                yield break;
            }

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            Vector3 targetPos = Vector3.Lerp(startPos, endPos, progress);
            
            RiftObject.gameObject.transform.position = targetPos;
            // ★プレイヤーをWaypointに追従させる
            _riftWaypoint?.Base.UpdateWaypointChildren();

            yield return 0f;
        }
        
        if (RiftObject?.gameObject != null)
            RiftObject.gameObject.transform.position = endPos;
    }
    
    private static IEnumerator<float> AnimToPosition(Vector3 startpos, Vector3 endpos, float duration)
    {
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            if (Round.IsLobby || Round.IsEnded)
            {
                ForceReset("round ended during up anim");
                yield break;
            }

            if (RiftObject?.gameObject == null)
            {
                ForceReset("rift destroyed during up anim");
                yield break;
            }

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            Vector3 targetPos = Vector3.Lerp(startpos, endpos, progress);
            
            RiftObject.gameObject.transform.position = targetPos;
            // ★プレイヤーをWaypointに追従させる
            _riftWaypoint?.Base.UpdateWaypointChildren();

            yield return 0f;
        }
        
        if (RiftObject?.gameObject != null)
            RiftObject.gameObject.transform.position = endpos;
    }
}

public class TerminalRiftLabHandler : SlafightLabApiHandler
{
    protected override void RegisterEvents(EventSubscriptionScope subscriptions)
    {
        Log.Debug("[TerminalRiftLabHandler] Register");
        subscriptions.Add(
            () => LabApi.Events.Handlers.PlayerEvents.SearchedToy += OnSearchedToy,
            () => LabApi.Events.Handlers.PlayerEvents.SearchedToy -= OnSearchedToy);
    }

    private void OnSearchedToy(PlayerSearchedToyEventArgs ev)
    {
        if (ev.Player?.Room?.Name != RoomName.HczTestroom) return;
        Log.Debug("[TerminalRiftLabHandler] HczTestRoom → TryInvoke");
        TerminalRift.TryInvoke();
    }
}
