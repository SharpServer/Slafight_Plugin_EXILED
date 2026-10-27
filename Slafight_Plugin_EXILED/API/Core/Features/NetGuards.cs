using CentralAuth;
using Exiled.API.Features;
using Mirror;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// Mirror へ直接メッセージを送るときの受信可否判定です。
/// </summary>
/// <remarks>実クライアントは isReady に加えて <see cref="ClientInstanceMode.ReadyClient"/> である必要があります。</remarks>
public static class NetGuards
{
    /// <summary>
    /// 実クライアントとして受信できる状態かどうか。
    /// スポーン途中のハブや NPC・ダミーに送ると壊れます。
    /// </summary>
    public static bool IsReadyClient(ReferenceHub hub)
    {
        if (hub is null) return false;
        if (hub.Mode != ClientInstanceMode.ReadyClient) return false;
        if (hub.netId == 0) return false;

        return hub.connectionToClient is { isReady: true };
    }

    /// <inheritdoc cref="IsReadyClient(ReferenceHub)"/>
    public static bool IsReadyClient(Player player) =>
        player?.ReferenceHub is { } hub && IsReadyClient(hub);

    /// <summary>
    /// 未認証でなく接続が生きているかだけを見ます。<see cref="IsReadyClient(ReferenceHub)"/> より緩い判定です。
    /// </summary>
    public static bool IsValidReceiver(ReferenceHub hub)
    {
        if (hub is null) return false;
        if (hub.isLocalPlayer) return false;
        if (hub.Mode == ClientInstanceMode.Unverified) return false;

        return hub.connectionToClient is { isReady: true };
    }
}
