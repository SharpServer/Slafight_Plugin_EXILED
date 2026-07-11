using LabApi.Events.Arguments.PlayerEvents;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class AntiMemeBomb : ObjectPrefab
{
    protected override string SchematicName => "AntiMemeBomb";

    protected override void OnSetup()
    {
        AddInteractable(duration: 5f, offset: Vector3.up * 2.05f, scale: Vector3.one * 3f);
    }

    protected override void OnToySearchedNearby(PlayerSearchedToyEventArgs ev)
    {
        foreach (var p in Player.List)
        {
            if (p is null || !p.IsAlive) continue;
            p.SendWarheadExplosionEffect();
            p.Kill("反ミーム爆弾により爆破された");
        }
    }
}
