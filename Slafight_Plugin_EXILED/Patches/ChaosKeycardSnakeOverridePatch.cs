using HarmonyLib;
using InventorySystem.Items.Keycards;
using InventorySystem.Items.Keycards.Snake;
using Mirror;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.Patches;

/// <summary>
/// Prevents the inspected card's original client-local SnakeEngine from
/// modifying a server-authoritative image session. Direction commands remain
/// available for Entertainment games; only native Snake state deltas are
/// discarded while an owner-session takeover is active.
/// </summary>
[HarmonyPatch(
    typeof(ChaosKeycardItem),
    nameof(ChaosKeycardItem.ServerProcessCustomCmd),
    typeof(NetworkReader))]
internal static class ChaosKeycardSnakeOverridePatch
{
    [HarmonyPrefix]
    private static bool Prefix(ChaosKeycardItem __instance, NetworkReader reader)
    {
        if (__instance == null ||
            reader == null ||
            !SnakeImageApi.BlocksClientSnakeSync(__instance.ItemSerial))
        {
            return true;
        }

        int originalPosition = reader.Position;
        try
        {
            return reader.Remaining == 0 ||
                   (ChaosKeycardItem.ChaosMsgType)reader.ReadByte() !=
                   ChaosKeycardItem.ChaosMsgType.SnakeMsgSync;
        }
        finally
        {
            reader.Position = originalPosition;
        }
    }
}

/// <summary>
/// Covers the score delta that is already being processed when an image
/// takeover starts from inside SNAPI's Score event.
/// </summary>
[HarmonyPatch(
    typeof(ChaosKeycardItem),
    nameof(ChaosKeycardItem.ServerSendMessage),
    typeof(SnakeNetworkMessage))]
internal static class ChaosKeycardSnakeBroadcastOverridePatch
{
    [HarmonyPrefix]
    private static bool Prefix(ChaosKeycardItem __instance, SnakeNetworkMessage msg)
        => !SnakeImageApi.BlocksClientSnakeSync(__instance.ItemSerial) ||
           !msg.HasFlag(SnakeNetworkMessage.SyncFlags.Delta);
}
