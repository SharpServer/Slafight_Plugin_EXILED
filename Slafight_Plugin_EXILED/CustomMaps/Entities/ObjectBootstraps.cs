using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;
using UnityEngine;
using Server = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.CustomMaps.Entities;

public class ObjectBootstraps : IBootstrapHandler
{
    private static CoroutineHandle _setupHandle;

    public static void Register()
    {
        Server.RoundStarted += OnRoundStarted;
    }

    public static void Unregister()
    {
        Server.RoundStarted -= OnRoundStarted;
        Timing.KillCoroutines(_setupHandle);
    }

    private static void OnRoundStarted()
    {
        Trashbox.ResetSharedRoundState();

        Timing.KillCoroutines(_setupHandle);
        _setupHandle = Timing.CallDelayed(2.25f, () =>
        {
            SetupObjectPrefabs();
            SetupTantrum();
        });
    }

    private static void SetupObjectPrefabs()
    {
        if (MapFlags.EzPcTentaclePoint != Vector3.zero)
        {
            new Tentacle { Position = MapFlags.EzPcTentaclePoint }.Create();
            Ragdoll.CreateAndSpawn(RoleTypeId.Scientist, "Dr. Kai", "触手に傷つけられた", MapFlags.EzPcTentaclePoint);
        }

        if (MapFlags.HczOverbeyondDocumentPoint != Vector3.zero)
            new Document { Position = MapFlags.HczOverbeyondDocumentPoint, DocumentType = DocumentType.Overbeyond }.Create();

        if (MapFlags.HczAboutSqDocumentPoint != Vector3.zero)
            new Document { Position = MapFlags.HczAboutSqDocumentPoint, DocumentType = DocumentType.AboutSQ }.Create();

        if (MapFlags.LczScp3005DocumentPoint != Vector3.zero)
            new Document { Position = MapFlags.LczScp3005DocumentPoint, DocumentType = DocumentType.Scp3005 }.Create();
    }

    private static void SetupTantrum()
    {
        if (MapFlags.Scp173SpawnPoint != Vector3.zero)
            PlaceTantrumAbility.ExecuteByApi(MapFlags.Scp173SpawnPoint + Vector3.down * 0.5f);
    }
}
