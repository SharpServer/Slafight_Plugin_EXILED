using System;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using Mirror;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

public class CreateSinkholeAbility : AbilityBase
{
    protected override float DefaultCooldown => 60f;
    protected override int DefaultMaxUses => -1;

    public CreateSinkholeAbility(Player owner) : base(owner) { }
    public CreateSinkholeAbility(Player owner, float cooldownSeconds) : base(owner, cooldownSeconds) { }
    public CreateSinkholeAbility(Player owner, float cooldownSeconds, int maxUses) : base(owner, cooldownSeconds, maxUses) { }

    protected override void ExecuteAbility(Player player)
    {
        try
        {
            const float forwardDistance = 12f;
            const float downDistance = 10f;
            const float spawnOffset = 0.01f;

            var position = player.Position + player.CameraTransform.forward * 3.5f;

            if (player.TryGetRaycast(forwardDistance, LayerMasks.OnlyWorldCollision, out var hit))
            {
                var probeStart = hit.point + Vector3.up * 3f;

                if (Physics.Raycast(probeStart, Vector3.down, out var groundHit, downDistance, (int)LayerMasks.OnlyWorldCollision))
                    position = groundHit.point + Vector3.up * spawnOffset;
                else
                    position = hit.point + Vector3.up * spawnOffset;
            }

            var sinkhole = PrefabHelper.Spawn(PrefabType.Sinkhole, position, Quaternion.identity);
            NetworkServer.Spawn(sinkhole);
            Timing.CallDelayed(10f, () => UnityEngine.Object.Destroy(sinkhole));
        }
        catch (Exception ex)
        {
            Log.Error($"Sinkhole Prefabスポーン失敗: {ex}");
        }
    }
}