using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;
using UnityEngine;

namespace Slafight_Plugin_EXILED.LabApiBridgeHandlers;

public class TriggerPointItemSpawner : SlafightLabApiHandler
{
    protected override void RegisterEvents(EventSubscriptionScope subscriptions)
    {
        subscriptions.Add(
            () => LabApi.Events.Handlers.ServerEvents.RoundStarted += OnRoundStarted,
            () => LabApi.Events.Handlers.ServerEvents.RoundStarted -= OnRoundStarted);
    }

    private static void OnRoundStarted()
    {
        Timing.CallDelayed(1.05f, () =>
        {
            CItem.Get<NvgNormal>()?.Spawn(Room.Get(RoomType.Hcz939).WorldPosition(Vector3.up * 1.5f));
        });

        Timing.CallDelayed(2.25f, SpawnItems);
    }

    private static void SpawnItems()
    {
        SpawnIfSet(MapFlags.CisrGoCRailgunPoint, pos => CItem.Get<GunGoCRailgun>()?.Spawn(pos));
        SpawnIfSet(MapFlags.CisrScp1425Point, pos =>
        {
            var pickup = CItem.Get<Scp1425>()?.Spawn(pos);
            pickup?.Rotation *= Quaternion.Euler(180f, 0f, 0f);
        });
        SpawnIfSet(MapFlags.CisrSnav300Point, pos => CItem.Get<SNAV300>()?.Spawn(pos));
        SpawnIfSet(MapFlags.CisrMemoryForcePillPoint, pos => CItem.Get<ClassXMemoryForcePil>()?.Spawn(pos));
        SpawnIfSet(MapFlags.Scp682SpawnPoint, pos => CItem.Get<OmegaWarheadAccess>()?.Spawn(pos));
        SpawnIfSet(MapFlags.CisrScp513Point, pos => CItem.Get<Scp513Item>()?.Spawn(pos));
        SpawnIfSet(MapFlags.CisrSchwarzschildQuasarPoint, pos =>
        {
            var pickup = CItem.Get<SchwarzschildQuasar>()?.Spawn(pos);
            pickup?.Transform.localEulerAngles = new Vector3(0f, 0f, 90f);
        });
        SpawnIfSet(MapFlags.CisrAntiMemeGrenadePoint, pos =>
        {
            for (var i = 0; i < 5; i++)
                CItem.Get<NeutralizeGrenade>()?.Spawn(pos);
        });
        SpawnIfSet(MapFlags.CisrKeycardArmory1Point, pos => CItem.Get<KeycardArmoryLevel1>()?.Spawn(pos));
        SpawnIfSet(MapFlags.CisrKeycardArmory2Point, pos => CItem.Get<KeycardArmoryLevel2>()?.Spawn(pos));
        SpawnIfSet(MapFlags.CisrKeycardArmory3Point, pos => CItem.Get<KeycardArmoryLevel3>()?.Spawn(pos));
        SpawnIfSet(MapFlags.CisrKeycardSurveillancePoint, pos => CItem.Get<KeycardSurveillance>()?.Spawn(pos));
    }

    private static void SpawnIfSet(Vector3 position, System.Action<Vector3> spawn)
    {
        if (position != Vector3.zero)
            spawn(position);
    }
}
