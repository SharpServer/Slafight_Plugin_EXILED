using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Serializable;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace Slafight_Plugin_EXILED.LabApiBridgeHandlers;

public class CustomItemTriggerPointLabHandler : SlafightLabApiHandler
{
    protected override void RegisterEvents(EventSubscriptionScope subscriptions)
    {
        subscriptions.Add(
            () => LabApi.Events.Handlers.ServerEvents.RoundStarted += OnRoundStarted,
            () => LabApi.Events.Handlers.ServerEvents.RoundStarted -= OnRoundStarted);
    }

    private static void OnRoundStarted()
    {
        Logger.Info("LabApi Loader: Green");

        Timing.CallDelayed(1.05f, () =>
        {
            CItem.Get<NvgNormal>()?.Spawn(Room.Get(RoomType.Hcz939).WorldPosition(Vector3.up * 1.5f));
        });

        Timing.CallDelayed(2.0f, SpawnTriggerPointItems);
    }

    private static void SpawnTriggerPointItems()
    {
        foreach (var point in TriggerPointManager.GetAll())
        {
            if (point.Base is not SerializableCustomTriggerPoint trig || string.IsNullOrEmpty(trig.Tag))
                continue;

            var pos = TriggerPointManager.GetWorldPosition(point);

            try
            {
                SpawnByTag(trig.Tag, pos);
            }
            catch (Exception e)
            {
                Log.Error($"[CustomItemTriggerPointLabHandler] Error while spawning CustomItem at trigger point {trig.Tag}: {e}");
            }
        }
    }

    private static void SpawnByTag(string tag, Vector3 pos)
    {
        switch (tag)
        {
            case "CISR_GoCRailgun":
                CItem.Get<GunGoCRailgun>()?.Spawn(pos);
                break;
            case "CISR_Scp1425":
                var scp1425Pickup = CItem.Get<Scp1425>()?.Spawn(pos);
                scp1425Pickup?.Rotation *= Quaternion.Euler(180f, 0f, 0f);
                break;
            case "CISR_SNAV300":
                CItem.Get<SNAV300>()?.Spawn(pos);
                break;
            case "CISR_MFP":
                CItem.Get<ClassXMemoryForcePil>()?.Spawn(pos);
                break;
            case "Scp682SpawnPoint":
                CItem.Get<OmegaWarheadAccess>()?.Spawn(pos);
                break;
            case "CISR_SCP513":
                CItem.Get<Scp513Item>()?.Spawn(pos);
                break;
            case "CISR_SQ":
                var sq = CItem.Get<SchwarzschildQuasar>()?.Spawn(pos);
                sq?.Transform.localEulerAngles = new Vector3(0f, 0f, 90f);
                break;
            case "CISR_AntiMemeGrenade":
                for (var i = 0; i < 5; i++)
                    CItem.Get<NeutralizeGrenade>()?.Spawn(pos);
                break;
            case "AntiMemeButton":
                MapFlags.AntiMemeButton = pos;
                break;
            case "CISR_KeycardArmory1":
                CItem.Get<KeycardArmoryLevel1>()?.Spawn(pos);
                break;
            case "CISR_KeycardArmory2":
                CItem.Get<KeycardArmoryLevel2>()?.Spawn(pos);
                break;
            case "CISR_KeycardArmory3":
                CItem.Get<KeycardArmoryLevel3>()?.Spawn(pos);
                break;
            case "CISR_KeycardSurveillance":
                CItem.Get<KeycardSurveillance>()?.Spawn(pos);
                break;
        }
    }
}
