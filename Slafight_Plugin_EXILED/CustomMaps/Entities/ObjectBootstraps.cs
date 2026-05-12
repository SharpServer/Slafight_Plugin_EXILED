using Exiled.API.Features;
using MEC;
using PlayerRoles;
using ProjectMER.Features;
using ProjectMER.Features.Serializable;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;
using Slafight_Plugin_EXILED.MainHandlers;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.Entities;

public class ObjectBootstraps : IBootstrapHandler
{
    public static void Register()
    {
        Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted;
    }
    ////////////////////////////////
    private static void OnRoundStarted()
    {
        SetupObjectPrefabs();
        SetupTantrum();
    }

    private static void SetupObjectPrefabs()
    {
        foreach (var point in TriggerPointManager.GetAll())
        {
            if (point.Base is not SerializableCustomTriggerPoint trig || string.IsNullOrEmpty(trig.Tag))
                continue;
            var pos = TriggerPointManager.GetWorldPosition(point);
            switch (trig.Tag)
            {
                case "EzPcTentaclePoint":
                    new Tentacle(){ Position = pos }.Create();
                    Ragdoll.CreateAndSpawn(RoleTypeId.Scientist, "Dr. Kai", "触手に傷つけられた", pos);
                    break;
                case "HczSQ":
                    new Document(){ Position = pos, DocumentType = DocumentType.Overbeyond }.Create();
                    break;
                case "HczASQ":
                    new Document(){ Position = pos, DocumentType = DocumentType.AboutSQ }.Create();
                    break;
                case "LczS3005D":
                    new Document(){ Position = pos, DocumentType = DocumentType.Scp3005 }.Create();
                    break;
            }
        }
    }

    private static void SetupTantrum()
    {
        Timing.CallDelayed(2f, () =>
        {
            PlaceTantrumAbility.ExecuteByApi(EventHandler.Scp173SpawnPoint + Vector3.down * 0.75f);
        });
    }
}