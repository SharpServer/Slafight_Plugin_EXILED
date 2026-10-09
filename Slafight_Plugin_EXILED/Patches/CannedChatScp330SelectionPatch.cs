using HarmonyLib;
using InventorySystem.Items.Usables.Scp330;
using Mirror;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.Accesibilities.InventoryChat;

namespace Slafight_Plugin_EXILED.Patches;

/// <summary>
/// 定型文メニュー用に一時生成したバッグの選択だけを横取りします。
/// 通常のSCP-330バッグは元のネットワーク処理へそのまま通します。
/// </summary>
[HarmonyPatch(typeof(Scp330NetworkHandler), nameof(Scp330NetworkHandler.ServerSelectMessageReceived))]
internal static class CannedChatScp330SelectionPatch
{
    private static bool Prefix(NetworkConnection conn, SelectScp330Message msg)
        => !CannedChatMenuApi.TryHandleSelection(conn, msg);
}
