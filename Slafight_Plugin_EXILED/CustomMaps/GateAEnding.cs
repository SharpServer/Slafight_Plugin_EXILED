using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using MEC;
using NetworkManagerUtils.Dummies;
using PlayerRoles;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

using Slafight_Plugin_EXILED.API.Features;
namespace Slafight_Plugin_EXILED.CustomMaps;

public static class GateAEnding
{
    public static void Register()
    {
        Exiled.Events.Handlers.Server.RoundStarted += SetupNpcs;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.RoundStarted -= SetupNpcs;
    }

    private static SchematicObject? _triggerObject;
    public static bool NowPlayingSound = false;
    private static Npc npc1;
    private static Npc npc2;
    private static Npc npc3;
    private static List<Npc> _npcs;

    private static CoroutineHandle handle;


    private static void SetupNpcs()
    {
        Timing.CallDelayed(1.5f, () =>
        {
            _triggerObject = null;
            foreach (var map in MapUtils.LoadedMaps.Values)
            {
                if (map.SpawnedObjects == null) continue;
                foreach (var meo in map.SpawnedObjects)
                {
                    if (meo.TryGetComponent(out SchematicObject schematic) && schematic.Name == "GateAEnding")
                    {
                        _triggerObject = schematic;
                        handle = Timing.RunCoroutine(Coroutine());
                        break;
                    }
                }
                if (_triggerObject != null) break;
            }
        });

        NowPlayingSound = false;
        Timing.KillCoroutines(handle);
        npc1 = Npc.Spawn("???", RoleTypeId.ChaosRifleman, true, new Vector3(-14.742f, 295.5f, -6.453f));
        npc2 = Npc.Spawn("???", RoleTypeId.ChaosRifleman, true, new Vector3(-14.297f, 295.5f, -8.121f));
        npc3 = Npc.Spawn("???", RoleTypeId.ChaosRifleman, true, new Vector3(-14.613f, 295.5f, -9.582f));
        Timing.CallDelayed(0.5f, () =>
        {
            npc1.Rotation *= Quaternion.Euler(0f, 90f, 0f);
            npc1.IsGodModeEnabled = true;
            npc2.Rotation *= Quaternion.Euler(0f, 90f, 0f);
            npc2.IsGodModeEnabled = true;
            npc3.Rotation *= Quaternion.Euler(0f, 90f, 0f);
            npc3.IsGodModeEnabled = true;
            _npcs = [npc1, npc2, npc3];
            _npcs.ForEach(npc => npc.IsSpectatable = false);
        });
    }

    private static IEnumerator<float> Coroutine()
    {
        if (!Round.InProgress) yield break;
        if (_triggerObject == null) yield return Timing.WaitForSeconds(3f);
        if (Player.List.Any(player => Vector3.Distance(player.Position, _triggerObject.Position) <= 2.5f))
        {
            if (Player.List.Any(player =>
                    Vector3.Distance(player.Position, _triggerObject.Position) <= 2.5f &&
                    player.GetTeam() != CTeam.ClassD))
            {
                _npcs.ForEach(npc => {
                    TryInvokeDummy(npc, "Shoot->Hold");
                });
            }
            else if (!NowPlayingSound)
            {
                NowPlayingSound = true;
                SpeakerApi.Play("SpawnBell.ogg", "GateAEnding", _triggerObject.Position, true, null, false, 10, 0);
                var players = Player.List
                    .Where(player => Vector3.Distance(player.Position, _triggerObject.Position) <= 2.5f).ToList();
                players.ForEach(player =>
                {
                    player.IsGodModeEnabled = true;
                    player.EnableEffect(EffectType.Invigorated, 255);
                    player.EnableEffect(EffectType.Slowness, 95);
                });
                yield return Timing.WaitForSeconds(1.1f);
                if (!Round.InProgress) yield break;
                players = Player.List
                    .Where(player => Vector3.Distance(player.Position, _triggerObject.Position) <= 2.5f).ToList();
                players.ForEach(player =>
                {
                    player.IsGodModeEnabled = true;
                    player.EnableEffect(EffectType.Invigorated, 255);
                    player.EnableEffect(EffectType.Slowness, 95);
                });
                SpeakerApi.Play("CI.ogg", "GateAEnding", _triggerObject.Position, true, null, false, 10, 0);
                yield return Timing.WaitForSeconds(4.15f);
                if (!Round.InProgress) yield break;
                players = Player.List
                    .Where(player => Vector3.Distance(player.Position, _triggerObject.Position) <= 2.5f).ToList();
                players.ForEach(player =>
                {
                    player.IsGodModeEnabled = true;
                    player.EnableEffect(EffectType.Invigorated, 255);
                    player.EnableEffect(EffectType.Slowness, 95);
                });
                SpeakerApi.Play("DeathBell.ogg", "GateAEnding", _triggerObject.Position, true, null, false, 10, 0);
                players.ForEach(player =>
                {
                    player.EnableEffect(EffectType.Blinded, 255, 2f);
                    player.SaveItems();
                    player.SetRole(RoleTypeId.ChaosConscript);
                });
                NowPlayingSound = false;
            }
            else
            {
                _npcs.ForEach(npc => npc.CurrentItem = Item.Create(ItemType.GunAK));
            }
        }

        yield return Timing.WaitForSeconds(0.5f);
    }
    
    private static void TryInvokeDummy(Npc npc, string action)
    {
        var hub = npc.ReferenceHub;
        foreach (var a in DummyActionCollector.ServerGetActions(hub))
        {
            if (a.Name.EndsWith(action)) { a.Action(); break; }
        }
    }
}