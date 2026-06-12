using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

public class Scp035TentacleAbility : AbilityBase
{
    protected override float DefaultCooldown => 10f;
    protected override int DefaultMaxUses => -1;

    public Scp035TentacleAbility(Player owner) : base(owner) { }
    public Scp035TentacleAbility(Player owner, float cooldownSeconds) : base(owner, cooldownSeconds) { }
    public Scp035TentacleAbility(Player owner, float cooldownSeconds, int maxUses) : base(owner, cooldownSeconds, maxUses) { }

    protected override void ExecuteAbility(Player player)
    {
        try
        {
            const float forwardDistance = 12f;
            const float downDistance = 10f;
            const float spawnOffset = 0.02f;

            var position = player.Position + player.CameraTransform.forward * 3f;

            if (player.TryGetRaycast(forwardDistance, LayerMasks.OnlyWorldCollision, out var hit))
            {
                var probeStart = hit.point + Vector3.up * 3f;

                if (Physics.Raycast(probeStart, Vector3.down, out var groundHit, downDistance, (int)LayerMasks.OnlyWorldCollision))
                    position = groundHit.point + Vector3.up * spawnOffset;
                else
                    position = hit.point + Vector3.up * spawnOffset;
            }

            new Tentacle
            {
                Position = position,
                AutoDestroyEnabled = true,
                AutoDestroyTime = 30f
            }.Create();
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to spawn Tentacle:\n{ex}");
        }
    }
}