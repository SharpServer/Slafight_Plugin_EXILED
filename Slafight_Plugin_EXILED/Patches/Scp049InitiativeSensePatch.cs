using System.Collections.Generic;
using System.Reflection.Emit;
using Exiled.API.Features;
using HarmonyLib;
using Mirror;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps;
using PlayerRoles.PlayableScps.Scp049;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Patches;

[HarmonyPatch(typeof(Scp049SenseAbility))]
public static class Scp049InitiativeSensePatch
{
    private const float IndicatorRefreshInterval = 3f;
    private static readonly Dictionary<Scp049SenseAbility, float> NextIndicatorRefresh = new();

    [HarmonyPatch(nameof(Scp049SenseAbility.ServerProcessCmd), typeof(NetworkReader))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ServerProcessCmdTranspiler(
        IEnumerable<CodeInstruction> instructions)
        => ReplaceEnemyCheck(instructions, nameof(Scp049SenseAbility.ServerProcessCmd));

    [HarmonyPatch(nameof(Scp049SenseAbility.Update))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> UpdateTranspiler(
        IEnumerable<CodeInstruction> instructions)
        => ReplaceEnemyCheck(instructions, nameof(Scp049SenseAbility.Update));

    [HarmonyPatch(nameof(Scp049SenseAbility.Update))]
    [HarmonyPostfix]
    private static void UpdatePostfix(Scp049SenseAbility __instance)
    {
        if (!NetworkServer.active)
            return;

        if (!__instance.HasTarget || __instance.Target == null || __instance.Duration.IsReady)
        {
            NextIndicatorRefresh.Remove(__instance);
            return;
        }

        float now = Time.realtimeSinceStartup;
        if (!NextIndicatorRefresh.TryGetValue(__instance, out float nextRefresh))
        {
            NextIndicatorRefresh[__instance] = now + IndicatorRefreshInterval;
            return;
        }

        if (now < nextRefresh)
            return;

        __instance.ServerSendRpc(toAll: true);
        NextIndicatorRefresh[__instance] = now + IndicatorRefreshInterval;
    }

    [HarmonyPatch(nameof(Scp049SenseAbility.ResetObject))]
    [HarmonyPostfix]
    private static void ResetObjectPostfix(Scp049SenseAbility __instance)
    {
        NextIndicatorRefresh.Remove(__instance);
    }

    public static bool TryFindInitiativeTarget(Scp049SenseAbility ability, out Player target)
    {
        target = null;

        if (ability?.Owner == null)
            return false;

        Player owner = Player.Get(ability.Owner);
        if (owner == null || owner.GetCustomRole() != CRoleTypeId.InitiativeWolf)
            return false;

        Transform camera = ability.Owner.PlayerCameraReference;
        if (camera == null)
            return false;

        float maximumDistanceSqr = ability._distanceThreshold * ability._distanceThreshold;
        float minimumDot = ability._dotThreshold;
        Vector3 ownerPosition = ability.CastRole.FpcModule.Position;

        foreach (Player candidate in Player.List)
        {
            if (candidate == null ||
                candidate.ReferenceHub == null ||
                candidate.ReferenceHub == ability.Owner ||
                !candidate.IsAlive ||
                candidate.GetTeam() != CTeam.SCPs ||
                candidate.ReferenceHub.roleManager.CurrentRole is not FpcStandardRoleBase candidateRole)
            {
                continue;
            }

            Vector3 candidatePosition = candidateRole.FpcModule.Position;
            Vector3 directionFromCamera = candidatePosition - camera.position;
            Vector3 forward = camera.forward;

            if (Mathf.Abs((candidatePosition - ownerPosition).y) < Scp049SenseAbility.HeightDiffIgnoreY &&
                directionFromCamera.sqrMagnitude < Scp049SenseAbility.NearbyDistanceSqr)
            {
                forward.y = 0f;
                forward.Normalize();
                directionFromCamera.y = 0f;
            }

            float dot = Vector3.Dot(forward, directionFromCamera.normalized);
            if (dot < minimumDot)
                continue;

            float distanceSqr = (candidatePosition - ownerPosition).sqrMagnitude;
            if (distanceSqr > maximumDistanceSqr)
                continue;

            float radius = candidateRole.FpcModule.CharacterControllerSettings.Radius;
            if (!VisionInformation.GetVisionInformation(
                    ability.Owner,
                    camera,
                    candidateRole.CameraPosition,
                    radius,
                    ability._distanceThreshold,
                    checkFog: true,
                    checkLineOfSight: true,
                    maskLayer: 0,
                    checkInDarkness: false)
                .IsLooking)
            {
                continue;
            }

            maximumDistanceSqr = distanceSqr;
            minimumDot = dot;
            target = candidate;
        }

        return target != null;
    }

    public static bool IsInitiativeSenseTargetAllowed(ReferenceHub ownerHub, ReferenceHub targetHub)
    {
        Player owner = Player.Get(ownerHub);
        if (owner == null || owner.GetCustomRole() != CRoleTypeId.InitiativeWolf)
            return HitboxIdentity.IsEnemy(ownerHub, targetHub);

        Player target = Player.Get(targetHub);
        return target != null &&
               targetHub != ownerHub &&
               target.IsAlive &&
               target.GetTeam() == CTeam.SCPs;
    }

    internal static IEnumerable<CodeInstruction> ReplaceEnemyCheck(
        IEnumerable<CodeInstruction> instructions,
        string targetMethod)
    {
        var replacement = AccessTools.Method(
            typeof(Scp049InitiativeSensePatch),
            nameof(IsInitiativeSenseTargetAllowed));
        var original = AccessTools.Method(
            typeof(HitboxIdentity),
            nameof(HitboxIdentity.IsEnemy),
            [typeof(ReferenceHub), typeof(ReferenceHub)]);

        bool replaced = false;

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                replaced = true;
            }

            yield return instruction;
        }

        if (!replaced)
            Log.Error($"[Scp049InitiativeSensePatch] IsEnemy call was not found in {targetMethod}; patch was not applied.");
    }
}

[HarmonyPatch(typeof(Scp049AttackAbility), nameof(Scp049AttackAbility.IsTargetValid))]
public static class Scp049InitiativeAttackPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> IsTargetValidTranspiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var replacement = AccessTools.Method(
            typeof(Scp049InitiativeAttackPatch),
            nameof(IsInitiativeAttackTargetAllowed));
        var original = AccessTools.Method(
            typeof(HitboxIdentity),
            nameof(HitboxIdentity.IsEnemy),
            [typeof(ReferenceHub), typeof(ReferenceHub)]);

        bool replaced = false;

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                replaced = true;
            }

            yield return instruction;
        }

        if (!replaced)
        {
            Log.Error(
                $"[Scp049InitiativeAttackPatch] IsEnemy call was not found in " +
                $"{nameof(Scp049AttackAbility)}.{nameof(Scp049AttackAbility.IsTargetValid)}; patch was not applied.");
        }
    }

    public static bool IsInitiativeAttackTargetAllowed(ReferenceHub ownerHub, ReferenceHub targetHub)
    {
        Player owner = Player.Get(ownerHub);
        if (owner == null || owner.GetCustomRole() != CRoleTypeId.InitiativeWolf)
            return HitboxIdentity.IsEnemy(ownerHub, targetHub);

        Player target = Player.Get(targetHub);
        return target != null &&
               targetHub != ownerHub &&
               target.IsAlive &&
               target.GetTeam() != CTeam.Initiative;
    }
}
