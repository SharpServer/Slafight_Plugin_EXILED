using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Exiled.API.Features;
using HarmonyLib;
using Interactables.Interobjects.DoorUtils;
using LabApi.Events.Arguments.PlayerEvents;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using ExiledInteractingDoorEventArgs = Exiled.Events.EventArgs.Player.InteractingDoorEventArgs;

namespace Slafight_Plugin_EXILED.Patches;

[HarmonyPatch(typeof(DoorVariant), nameof(DoorVariant.ServerInteract))]
internal static class DoorAccessHintPatch
{
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.Last)]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        List<CodeInstruction> code = instructions.ToList();
        MethodInfo resolveLock = AccessTools.Method(typeof(DoorVariant), nameof(DoorVariant.TryResolveLock));
        MethodInfo allowInteraction = AccessTools.Method(typeof(DoorVariant), nameof(DoorVariant.AllowInteracting));
        MethodInfo labApiAllowed = AccessTools.PropertyGetter(typeof(PlayerInteractingDoorEventArgs), nameof(PlayerInteractingDoorEventArgs.IsAllowed));
        MethodInfo exiledAllowed = AccessTools.PropertyGetter(typeof(ExiledInteractingDoorEventArgs), nameof(ExiledInteractingDoorEventArgs.CanInteract));
        MethodInfo setTargetState = AccessTools.PropertySetter(typeof(DoorVariant), nameof(DoorVariant.NetworkTargetState));
        FieldInfo cooldown = AccessTools.Field(typeof(DoorVariant), "_remainingDeniedCooldown");

        int toggle = code.FindIndex(instruction => instruction.Calls(setTargetState));
        int resolve = code.FindIndex(instruction => instruction.Calls(resolveLock));
        int locked = resolve < 0 ? -1 : resolve + 2;
        int allow = code.FindIndex(instruction => instruction.Calls(allowInteraction));
        int gate = toggle > 0
            ? code.FindLastIndex(toggle - 1, instruction => instruction.opcode == OpCodes.Brfalse || instruction.opcode == OpCodes.Brfalse_S)
            : -1;
        int denied = gate >= 0 && code[gate].operand is Label deniedLabel
            ? code.FindIndex(instruction => instruction.labels.Contains(deniedLabel))
            : -1;

        // Only modify this method if all the expected native paths are present.
        // EXILED may inject its own event handlers before the first cooldown check.
        if (gate <= 0 || code[gate - 1].opcode != OpCodes.Ldloc_0 || denied <= gate ||
            !code[denied + 1].LoadsField(cooldown) || locked < 0 || allow <= locked || allow >= gate)
        {
            Log.Error("[DoorAccessHintPatch] Door interaction branches could not be located.");
            return code;
        }

        LocalBuilder codeDenied = generator.DeclareLocal(typeof(bool));
        // Insert from back to front so branch indices remain valid.
        for (int i = code.Count - 1; i >= 0; i--)
        {
            if (code[i].Calls(exiledAllowed) || (i > allow && i < gate && code[i].Calls(labApiAllowed)))
            {
                code[i].operand = Method(code[i].Calls(exiledAllowed) ? nameof(CheckExiledEventAllowed) : nameof(CheckLabApiEventAllowed));
                code[i].opcode = OpCodes.Call;
                InsertBefore(code, i, new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1));
            }
            else if (i == denied)
            {
                InsertBefore(code, i, new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldloc, codeDenied), new CodeInstruction(OpCodes.Call, Method(nameof(ShowPermissionOrCodeDenial))));
            }
            else if (i == gate)
            {
                InsertBefore(code, i, new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldloca, codeDenied), new CodeInstruction(OpCodes.Call, Method(nameof(CheckCode))),
                    new CodeInstruction(OpCodes.Dup), new CodeInstruction(OpCodes.Stloc_0));
            }
            else if (i == locked)
            {
                // This path is entered only on a failed lock resolution, even when native audio is on cooldown.
                code.InsertRange(i, new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Call, Method(nameof(ShowLockDenial))) });
            }
        }
        return code;
    }

    private static MethodInfo Method(string name) => AccessTools.Method(typeof(DoorAccessHintPatch), name);

    private static void InsertBefore(List<CodeInstruction> code, int index, params CodeInstruction[] insert)
    {
        insert[0].labels.AddRange(code[index].labels);
        code[index].labels.Clear();
        code.InsertRange(index, insert);
    }

    private static bool CheckCode(bool canOpen, DoorVariant door, ReferenceHub hub, out bool codeDenied)
    {
        codeDenied = canOpen && !DoorAccessManager.HasCorrectCode(hub, door.GetComponent<DoorAccessFeedback>()?.SecretCode);
        return canOpen && !codeDenied;
    }

    private static bool CheckLabApiEventAllowed(PlayerInteractingDoorEventArgs ev, DoorVariant door, ReferenceHub hub) =>
        CheckEventAllowed(ev.IsAllowed, door, hub);

    private static bool CheckExiledEventAllowed(ExiledInteractingDoorEventArgs ev, DoorVariant door, ReferenceHub hub) =>
        CheckEventAllowed(ev.CanInteract, door, hub);

    private static bool CheckEventAllowed(bool isAllowed, DoorVariant door, ReferenceHub hub)
    {
        if (isAllowed)
            return true;
        DoorAccessManager.Show(Player.Get(hub), door, DoorAccessDenialReason.Custom);
        return false;
    }

    private static void ShowLockDenial(DoorVariant door, ReferenceHub hub) =>
        DoorAccessManager.Show(Player.Get(hub), door, DoorAccessDenialReason.Locked);

    private static void ShowPermissionOrCodeDenial(DoorVariant door, ReferenceHub hub, bool codeDenied)
    {
        DoorAccessDenialReason reason = DoorAccessDenialReason.IncorrectCode;
        if (!codeDenied)
        {
            DoorPermissionsPolicy policy = door.RequiredPermissions;
            reason = policy.RequiredPermissions == DoorPermissionFlags.None ||
                policy.CheckPermissions(DoorAccessManager.AvailablePermissions(hub, door))
                    ? DoorAccessDenialReason.Custom
                    : DoorAccessDenialReason.MissingPermissions;
        }
        DoorAccessManager.Show(Player.Get(hub), door, reason);
    }
}
