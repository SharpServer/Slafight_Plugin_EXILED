using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

namespace Slafight_Plugin_EXILED.CustomMaps.Core.Interactions;

public class UDoorHandler : IBootstrapHandler
{
    public static void Register()
    {
        UDoor.BeforeInteraction += OnInteractingDoor;
        UDoor.AfterInteraction += OnInteractedDoor;
    }

    public static void Unregister()
    {
        UDoor.BeforeInteraction -= OnInteractingDoor;
        UDoor.AfterInteraction -= OnInteractedDoor;
    }

    private static void OnInteractingDoor(UDoorInteractionContext ctx)
    {
        switch (ctx.Door.Type)
        {
            case UDoorType.EzEvacuationShelter:
                if (ShelterManager.FirstFlag && !ShelterManager.LightIsOn)
                {
                    ctx.IsAllowed = false;
                }
                break;
        }
    }

    private static void OnInteractedDoor(UDoorInteractionContext ctx)
    {
        switch (ctx.Door.Type)
        {
            case UDoorType.EzEvacuationShelter:
                if (!ShelterManager.FirstFlag && ShelterManager.LightIsOn)
                {
                    foreach (var light in ObjectPrefabInstances.GetByTag<ControllableLight>("EzShelter"))
                    {
                        light.Level = 0;
                    }

                    SpeakerApi.Play("Blackout.ogg", "EzShelter", ctx.Door.Position, maxDistance: 20f, minDistance: 0.1f);
                    ShelterManager.FirstFlag = true;
                    ShelterManager.LightIsOn = false;
                }
                break;
        }
    }
}