using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CustomPlayerEffects;
using Exiled.API.Features;
using HarmonyLib;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp096;
using PlayerRoles.PlayableScps.Scp173;
using PlayerStatsSystem;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Patches;

internal static class ScpCrossFactionCombat
{
    public static bool IsCrossFactionScpCombat(ReferenceHub attackerHub, ReferenceHub victimHub)
    {
        if (attackerHub == null || victimHub == null || attackerHub == victimHub)
            return false;

        if (attackerHub.GetTeam() != Team.SCPs || victimHub.GetTeam() != Team.SCPs)
            return false;

        Player attacker = Player.Get(attackerHub);
        Player victim = Player.Get(victimHub);
        if (attacker == null || victim == null || !attacker.IsAlive || !victim.IsAlive)
            return false;

        CTeam attackerTeam = attacker.GetTeam();
        CTeam victimTeam = victim.GetTeam();
        return attackerTeam == CTeam.SCPs && victimTeam == CTeam.Fifthists ||
               attackerTeam == CTeam.Fifthists && victimTeam == CTeam.SCPs;
    }

    public static bool IsEnemyForDamage(
        RoleTypeId attackerRole,
        RoleTypeId victimRole,
        AttackerDamageHandler handler,
        ReferenceHub victimHub)
    {
        if (handler != null && IsCrossFactionScpCombat(handler.Attacker.Hub, victimHub))
            return true;

        return HitboxIdentity.IsEnemy(attackerRole, victimRole);
    }

    public static bool TryGetOwnerFromCamera(Transform camera, out ReferenceHub ownerHub)
    {
        ownerHub = null;
        if (camera == null)
            return false;

        foreach (ReferenceHub hub in ReferenceHub.AllHubs)
        {
            if (hub == null || hub.PlayerCameraReference != camera)
                continue;

            ownerHub = hub;
            return true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(HitboxIdentity), nameof(HitboxIdentity.IsEnemy), typeof(ReferenceHub), typeof(ReferenceHub))]
public static class ScpCrossFactionHitboxEnemyPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ReferenceHub attacker, ReferenceHub victim, ref bool __result)
    {
        if (!ScpCrossFactionCombat.IsCrossFactionScpCombat(attacker, victim))
            return true;

        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(AttackerDamageHandler), nameof(AttackerDamageHandler.ProcessDamage))]
public static class ScpCrossFactionDamageFriendlyFirePatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ProcessDamageTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo original = AccessTools.Method(
            typeof(HitboxIdentity),
            nameof(HitboxIdentity.IsEnemy),
            [typeof(RoleTypeId), typeof(RoleTypeId)]);
        MethodInfo replacement = AccessTools.Method(
            typeof(ScpCrossFactionCombat),
            nameof(ScpCrossFactionCombat.IsEnemyForDamage));

        bool replaced = false;

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                CodeInstruction loadHandler = new(OpCodes.Ldarg_0);
                loadHandler.labels.AddRange(instruction.labels);
                instruction.labels.Clear();

                yield return loadHandler;
                yield return new CodeInstruction(OpCodes.Ldarg_1);

                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                replaced = true;
            }

            yield return instruction;
        }

        if (!replaced)
        {
            Log.Error(
                $"[{nameof(ScpCrossFactionDamageFriendlyFirePatch)}] " +
                $"RoleTypeId IsEnemy call was not found in {nameof(AttackerDamageHandler)}.{nameof(AttackerDamageHandler.ProcessDamage)}; patch was not applied.");
        }
    }
}

[HarmonyPatch(typeof(Scp173SnapAbility), nameof(Scp173SnapAbility.TryHitTarget))]
public static class Scp173CrossFactionSnapPatch
{
    [HarmonyPostfix]
    private static void TryHitTargetPostfix(Transform origin, ref ReferenceHub target, ref bool __result)
    {
        if (__result || origin == null)
            return;

        if (!ScpCrossFactionCombat.TryGetOwnerFromCamera(origin, out ReferenceHub ownerHub))
            return;

        if (!TryGetHitbox(origin, out HitboxIdentity hitbox))
            return;

        ReferenceHub targetHub = hitbox.TargetHub;
        if (!ScpCrossFactionCombat.IsCrossFactionScpCombat(ownerHub, targetHub))
            return;

        target = targetHub;
        __result = true;
    }

    private static bool TryGetHitbox(Transform origin, out HitboxIdentity hitbox)
    {
        hitbox = null;
        if (!Physics.Raycast(origin.position, origin.forward, out RaycastHit hit, 1.5f, Scp173SnapAbility.SnapMask))
            return false;

        if (!hit.collider.TryGetComponent(out IDestructible destructible) || destructible is not HitboxIdentity found)
            return false;

        hitbox = found;
        return true;
    }
}

[HarmonyPatch(typeof(Scp096HitHandler), nameof(Scp096HitHandler.ProcessHits))]
public static class Scp096CrossFactionHitPatch
{
    [HarmonyPostfix]
    private static void ProcessHitsPostfix(Scp096HitHandler __instance, int count, ref Scp096HitResult __result)
    {
        if (__instance == null || __instance._scpRole == null || !__instance._scpRole.TryGetOwner(out ReferenceHub ownerHub))
            return;

        Scp096HitResult extraResult = Scp096HitResult.None;
        HashSet<uint> processedTargets = new();

        for (int i = 0; i < count && i < Scp096HitHandler.Hits.Length; i++)
        {
            Collider hit = Scp096HitHandler.Hits[i];
            if (hit == null || !hit.TryGetComponent(out IDestructible destructible) || destructible is not HitboxIdentity hitbox)
                continue;

            ReferenceHub targetHub = hitbox.TargetHub;
            if (!ScpCrossFactionCombat.IsCrossFactionScpCombat(ownerHub, targetHub))
                continue;

            if (!processedTargets.Add(hitbox.NetworkId))
                continue;

            int blockerMask = (int)Scp096HitHandler.SolidObjectMask & ~(1 << hit.gameObject.layer);
            if (Physics.Linecast(__instance._scpRole.CameraPosition, destructible.CenterOfMass, blockerMask))
                continue;

            bool is096Target = __instance._targetCounter.HasTarget(targetHub);
            float damage = is096Target ? __instance._humanTargetDamage : __instance._humanNontargetDamage;
            if (!__instance.DealDamage(hitbox, damage))
                continue;

            ApplyScp096HitEffect(__instance, targetHub, is096Target);
            extraResult |= Scp096HitResult.Human;
            if (!targetHub.IsAlive())
                extraResult |= Scp096HitResult.Lethal;
        }

        if (extraResult == Scp096HitResult.None)
            return;

        __instance.HitResult |= extraResult;
        __result |= extraResult;
    }

    private static void ApplyScp096HitEffect(Scp096HitHandler handler, ReferenceHub targetHub, bool is096Target)
    {
        if (targetHub == null)
            return;

        switch (handler._damageType)
        {
            case Scp096DamageHandler.AttackType.SlapLeft:
            case Scp096DamageHandler.AttackType.SlapRight:
                targetHub.playerEffectsController.EnableEffect<Concussed>(2.5f);
                break;
            case Scp096DamageHandler.AttackType.Charge:
                targetHub.playerEffectsController.EnableEffect<Concussed>(is096Target ? 10f : 4f);
                break;
        }
    }
}
