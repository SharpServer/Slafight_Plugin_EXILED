#nullable enable
using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Pickups;
using Exiled.Events.EventArgs.Map;
using Exiled.Events.EventArgs.Player;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.Entities;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class Scp513Item : CItem
{
    public override string DisplayName => "SCP-513";
    public override string Description => "???";
    protected override string UniqueKey => "Scp513Item";
    protected override ItemType BaseItem => ItemType.Coin;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.gray;

    // Pickup.CreateAndSpawn 直後は SetParent の親子関係がクライアントに同期されないため、
    // Light と同じく毎フレーム位置追従で Schematic を Pickup にくっつける。
    private static readonly Dictionary<ushort, SchematicObject> ActiveSchematics = new();
    private static readonly Dictionary<ushort, CoroutineHandle> SchematicCoroutines = new();

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.FlippingCoin += OnFlipping;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.FlippingCoin -= OnFlipping;
        base.UnregisterEvents();
    }

    protected override void OnPickupAdded(PickupAddedEventArgs ev)
    {
        try
        {
            var schem = ObjectSpawner.SpawnSchematic("SCP513ItemModel", ev.Pickup.Position, ev.Pickup.Rotation);
            if (schem != null)
            {
                var serial = ev.Pickup.Serial;
                ActiveSchematics[serial] = schem;
                SchematicCoroutines[serial] = Timing.RunCoroutine(TrackSchematic(ev.Pickup, schem));
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[SCP513] Schematic spawn failed: {ex.Message}");
        }

        base.OnPickupAdded(ev);
    }

    private static IEnumerator<float> TrackSchematic(Pickup pickup, SchematicObject schem)
    {
        while (pickup != null && schem != null
               && pickup.Base != null && schem.gameObject != null)
        {
            schem.transform.position = pickup.Position;
            schem.transform.rotation = pickup.Rotation;
            yield return Timing.WaitForOneFrame;
        }
    }

    protected override void OnPickupDestroyed(PickupDestroyedEventArgs ev)
    {
        if (ev.Pickup != null)
        {
            var serial = ev.Pickup.Serial;
            if (SchematicCoroutines.TryGetValue(serial, out var handle))
            {
                Timing.KillCoroutines(handle);
                SchematicCoroutines.Remove(serial);
            }
            if (ActiveSchematics.TryGetValue(serial, out var schem))
            {
                try { schem?.Destroy(); } catch { /* ignore */ }
                ActiveSchematics.Remove(serial);
            }
        }
        base.OnPickupDestroyed(ev);
    }

    private void OnFlipping(FlippingCoinEventArgs ev)
    {
        if (!CheckHeld(ev.Player)) return;
        Scp513.AddTarget(ev.Player);
        ev.Player.ShowHint("<size=25>何か視線を感じる気がする...</size>");
        var room = Room.Random(ZoneType.HeavyContainment);
        Spawn(room.WorldPosition(Vector3.up * 0.25f));
        ev.Item?.Destroy();
    }
}