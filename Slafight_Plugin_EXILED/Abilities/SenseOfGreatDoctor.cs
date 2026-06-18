using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

public class SenseOfGreatDoctor : AbilityBase
{
    protected override float DefaultCooldown => 200f;
    protected override int DefaultMaxUses => -1;

    public SenseOfGreatDoctor(Player owner) : base(owner) { }

    public SenseOfGreatDoctor(Player owner, float cooldownSeconds) : base(owner, cooldownSeconds) { }

    public SenseOfGreatDoctor(Player owner, float cooldownSeconds, int maxUses) : base(owner, cooldownSeconds, maxUses) { }

    protected override void ExecuteAbility(Player player)
    {
        if (player == null || !player.IsAlive)
            return;

        if (player.Role is not Scp049Role role)
            return;

        if (role.RemainingGoodSenseDuration > 0f || role.GoodSenseCooldown > 0f)
            return;

        role.GoodSenseCooldown = 120f;

        string clipId = $"Scp049Sense_{player.NetId}";
        SpeakerApi.PlayLoop("megasense.ogg", clipId, player.Position, player.Transform, maxDistance: 1f, minDistance: 1f);
        Timing.RunCoroutine(Coroutine(player, clipId));
    }

    private static IEnumerator<float> Coroutine(Player player, string clipId)
    {
        float elapsedTime = 0f;
        const float duration = 60f;
        const float interval = 0.1f;
        const float rangeSqr = 35f * 35f;

        while (elapsedTime < duration)
        {
            if (player == null || !player.IsAlive || player.Role is not Scp049Role)
                break;

            player.EnableEffect<MovementBoost>(10, 1f);

            Vector3 center = player.Position;

            foreach (Player target in Player.List)
            {
                if (!target.IsAlive)
                    continue;

                if (target.GetTeam() is CTeam.SCPs)
                    continue;

                if ((target.Position - center).sqrMagnitude > rangeSqr)
                    continue;

                target.EnableEffect<AnomalousTarget>(1f);
            }

            elapsedTime += interval;
            yield return Timing.WaitForSeconds(interval);
        }

        SpeakerApi.StopClip(clipId, "megasense.ogg");
    }
}