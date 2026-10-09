using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Exiled.API.Features;
using HarmonyLib;
using Respawning;
using Respawning.Waves;
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.Patches;

/// <summary>
/// RespawningTeam より先に送られる自動車両トリガーだけを抑えます。
/// OnWaveTrigger 自体は通し、タイマーの Pause とサーバーの演出待ちを維持します。
/// </summary>
[HarmonyPatch(typeof(WaveManager), nameof(WaveManager.RefreshNextWave))]
internal static class SpawnVehicleTriggerPatch
{
    [ThreadStatic]
    private static SpawnableWaveBase automaticWave;

    internal static bool IsAutomaticTrigger(SpawnableWaveBase wave) =>
        SpawnSystem.ControlsVehicleEffects && ReferenceEquals(automaticWave, wave);

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo invoke = AccessTools.Method(typeof(Action<SpawnableWaveBase>), nameof(Action<SpawnableWaveBase>.Invoke));
        MethodInfo replacement = AccessTools.Method(typeof(SpawnVehicleTriggerPatch), nameof(InvokeAutomaticTrigger));
        bool replaced = false;

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(invoke))
            {
                // 同じ引数スタックを使い、既存のラベル・例外ブロックも保持する。
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                replaced = true;
            }

            yield return instruction;
        }

        if (!replaced)
            Log.Error("[SpawnVehicleTriggerPatch] バニラの自動車両トリガーを検出できませんでした。");
    }

    private static void InvokeAutomaticTrigger(Action<SpawnableWaveBase> callback, SpawnableWaveBase wave)
    {
        SpawnableWaveBase previous = automaticWave;
        automaticWave = wave;

        try
        {
            callback.Invoke(wave);
        }
        finally
        {
            // 例外・再入でも抑制を残さない。SpawnSet の手動演出はこのスコープ外で送られる。
            automaticWave = previous;
        }
    }
}

[HarmonyPatch(typeof(WaveUpdateMessage), nameof(WaveUpdateMessage.ServerSendUpdate))]
internal static class SpawnVehicleUpdatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(SpawnableWaveBase wave, ref UpdateMessageFlags flags)
    {
        if (!SpawnVehicleTriggerPatch.IsAutomaticTrigger(wave))
            return true;

        // Timer / Pause / Tokens / Spawn などの同期は妨げない。
        flags &= ~UpdateMessageFlags.Trigger;
        return flags != 0;
    }
}
