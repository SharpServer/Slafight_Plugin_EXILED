using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Exiled.API.Features;
using HarmonyLib;

namespace Slafight_Plugin_EXILED.Patches;

/// <summary>
/// Prevents the vanilla lobby coroutine from skipping its countdown when the server fills up.
/// </summary>
[HarmonyPatch]
internal static class FullServerRoundStartPatch
{
    private static readonly MethodInfo CapacityGetter =
        AccessTools.PropertyGetter(typeof(CustomNetworkManager), nameof(CustomNetworkManager.ReservedMaxPlayers));

    private static readonly MethodInfo CapacityReplacement =
        AccessTools.Method(typeof(FullServerRoundStartPatch), nameof(GetRoundStartCapacity));

    private static MethodBase TargetMethod()
        => AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(CharacterClassManager), nameof(CharacterClassManager.Init)));

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        bool replaced = false;

        foreach (CodeInstruction instruction in instructions)
        {
            if (!replaced && instruction.Calls(CapacityGetter))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = CapacityReplacement;
                replaced = true;
            }

            yield return instruction;
        }

        if (!replaced)
            Log.Error("[FullServerRoundStartPatch] CharacterClassManager.Init の満員判定を検出できませんでした。");
    }

    private static int GetRoundStartCapacity(CustomNetworkManager networkManager)
        => Plugin.Singleton.Config.DisableFullServerRoundStart
            ? int.MaxValue
            : networkManager.ReservedMaxPlayers;
}
