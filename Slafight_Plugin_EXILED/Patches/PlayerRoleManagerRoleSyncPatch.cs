using System;
using System.Collections.Generic;
using System.Linq;
using CentralAuth;
using Exiled.API.Features;
using HarmonyLib;
using MEC;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl.NetworkMessages;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Patches;

[HarmonyPatch(typeof(PlayerRoleManager), nameof(PlayerRoleManager.SendNewRoleInfo))]
public static class PlayerRoleManagerRoleSyncPatch
{
    private static readonly Dictionary<uint, float> LastRoleSyncTimeByTarget = new();
    private static readonly HashSet<uint> PendingRoleSyncTargets = [];

    [HarmonyPrefix]
    private static bool SendNewRoleInfoPrefix(PlayerRoleManager __instance)
    {
        try
        {
            SendOrDefer(__instance);
        }
        catch (Exception ex)
        {
            Log.Warn($"[RoleSyncGuard] SendNewRoleInfo guard failed: {ex}");
        }

        return false;
    }

    private static void SendOrDefer(PlayerRoleManager manager)
    {
        if (!NetworkServer.active)
            return;

        if (!TryGetTarget(manager, out var targetHub, out var targetNetId))
            return;

        var now = Time.realtimeSinceStartup;
        if (LastRoleSyncTimeByTarget.TryGetValue(targetNetId, out var lastSyncTime))
        {
            var elapsed = now - lastSyncTime;
            if (elapsed < RoleSpawnTimings.RoleSyncMinSendInterval)
            {
                DeferLatestRoleSync(targetNetId, RoleSpawnTimings.RoleSyncMinSendInterval - elapsed);
                return;
            }
        }

        SendNow(manager, targetHub, targetNetId);
    }

    private static void DeferLatestRoleSync(uint targetNetId, float delay)
    {
        if (!PendingRoleSyncTargets.Add(targetNetId))
            return;

        delay = Mathf.Max(RoleSpawnTimings.NextFrame, delay);
        Log.Debug($"[RoleSyncGuard] Deferred role sync for netId={targetNetId} by {delay:0.###}s.");

        Timing.CallDelayed(delay, () =>
        {
            PendingRoleSyncTargets.Remove(targetNetId);

            try
            {
                if (!ReferenceHub.TryGetHubNetID(targetNetId, out var targetHub))
                    return;

                if (targetHub?.roleManager == null)
                    return;

                SendOrDefer(targetHub.roleManager);
            }
            catch (Exception ex)
            {
                Log.Warn($"[RoleSyncGuard] Deferred role sync failed for netId={targetNetId}: {ex}");
            }
        });
    }

    private static void SendNow(PlayerRoleManager manager, ReferenceHub targetHub, uint targetNetId)
    {
        LastRoleSyncTimeByTarget[targetNetId] = Time.realtimeSinceStartup;
        bool sentToTargetClient = false;

        foreach (var receiverHub in ReferenceHub.AllHubs.ToArray())
        {
            if (!TrySendToReceiver(manager, targetHub, receiverHub))
                continue;

            if (TryGetHubNetId(receiverHub, out var receiverNetId) && receiverNetId == targetNetId)
                sentToTargetClient = true;
        }

        if (sentToTargetClient)
            RoleSyncReadiness.MarkSelfRoleSyncSent(targetHub);
    }

    private static bool TrySendToReceiver(PlayerRoleManager manager, ReferenceHub targetHub, ReferenceHub receiverHub)
    {
        if (!IsValidReceiver(receiverHub))
            return false;

        NetworkWriterPooled writer = null;
        try
        {
            var targetRole = FpcServerPositionDistributor.GetVisibleRole(receiverHub, targetHub);
            writer = NetworkWriterPool.Get();

            var spoofedRole = FpcServerPositionDistributor.InvokeRoleSyncEvent(
                targetHub,
                receiverHub,
                targetRole,
                writer);

            if (spoofedRole.HasValue)
                targetRole = spoofedRole.Value;

            var connection = ((NetworkBehaviour)receiverHub).connectionToClient;
            ((NetworkConnection)connection).Send(
                new RoleSyncInfo(targetHub, targetRole, receiverHub, writer),
                channelId: 0);

            manager.PreviouslySentRole[((NetworkBehaviour)receiverHub).netId] = targetRole;
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn(
                $"[RoleSyncGuard] Failed to send role sync target={DescribeHub(targetHub)} receiver={DescribeHub(receiverHub)}: {ex}");
            return false;
        }
        finally
        {
            if (writer != null)
                NetworkWriterPool.Return(writer);
        }
    }

    private static bool TryGetTarget(PlayerRoleManager manager, out ReferenceHub targetHub, out uint targetNetId)
    {
        targetHub = null;
        targetNetId = 0;

        try
        {
            targetHub = manager?.Hub;
            if (targetHub?.roleManager == null)
                return false;

            targetNetId = ((NetworkBehaviour)targetHub).netId;
            return targetNetId != 0;
        }
        catch (Exception ex)
        {
            Log.Warn($"[RoleSyncGuard] Invalid role sync target: {ex.Message}");
            return false;
        }
    }

    private static bool TryGetHubNetId(ReferenceHub hub, out uint netId)
    {
        netId = 0;

        try
        {
            if (hub == null)
                return false;

            netId = ((NetworkBehaviour)hub).netId;
            return netId != 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidReceiver(ReferenceHub receiverHub)
    {
        try
        {
            if (receiverHub == null)
                return false;

            if (((NetworkBehaviour)receiverHub).isLocalPlayer)
                return false;

            if (receiverHub.Mode == ClientInstanceMode.Unverified)
                return false;

            var connection = ((NetworkBehaviour)receiverHub).connectionToClient;
            return connection != null && connection.isReady;
        }
        catch
        {
            return false;
        }
    }

    private static string DescribeHub(ReferenceHub hub)
    {
        if (hub == null)
            return "<null>";

        try
        {
            return $"{hub.nicknameSync?.MyNick ?? hub.ToString()}#{hub.PlayerId}/{((NetworkBehaviour)hub).netId}";
        }
        catch
        {
            return hub.ToString();
        }
    }
}

[HarmonyPatch(typeof(PlayerRolesNetUtils), nameof(PlayerRolesNetUtils.HandleSpawnedPlayer))]
public static class PlayerRolesNetUtilsHandleSpawnedPlayerPatch
{
    private const int MaxPackSendAttempts = 15;
    private static readonly HashSet<ReferenceHub> PendingInitialPacks = [];

    [HarmonyPrefix]
    private static bool HandleSpawnedPlayerPrefix(ReferenceHub hub)
    {
        if (!NetworkServer.active)
            return true;

        TrySendInitialRolePack(hub, 0);
        return false;
    }

    private static void TrySendInitialRolePack(ReferenceHub hub, int attempt)
    {
        if (hub == null)
            return;

        if (((NetworkBehaviour)hub).isLocalPlayer)
            return;

        if (!IsReadyForInitialRolePack(hub))
        {
            if (attempt >= MaxPackSendAttempts)
            {
                Log.Warn($"[RoleSyncGuard] Initial role pack skipped; receiver did not become ready: {DescribeHub(hub)}");
                return;
            }

            if (!PendingInitialPacks.Add(hub))
                return;

            Timing.CallDelayed(RoleSpawnTimings.RoleSyncInitialPackRetryInterval, () =>
            {
                PendingInitialPacks.Remove(hub);
                TrySendInitialRolePack(hub, attempt + 1);
            });

            return;
        }

        try
        {
            ((NetworkConnection)((NetworkBehaviour)hub).connectionToClient).Send(
                new RoleSyncInfoPack(hub),
                channelId: 0);
        }
        catch (Exception ex)
        {
            Log.Warn($"[RoleSyncGuard] Initial role pack send failed for {DescribeHub(hub)}: {ex}");
        }
    }

    private static bool IsReadyForInitialRolePack(ReferenceHub hub)
    {
        try
        {
            if (hub.Mode == ClientInstanceMode.Unverified)
                return false;

            var connection = ((NetworkBehaviour)hub).connectionToClient;
            return ((NetworkBehaviour)hub).netId != 0 &&
                   connection != null &&
                   connection.isReady;
        }
        catch
        {
            return false;
        }
    }

    private static string DescribeHub(ReferenceHub hub)
    {
        if (hub == null)
            return "<null>";

        try
        {
            return $"{hub.nicknameSync?.MyNick ?? hub.ToString()}#{hub.PlayerId}/{((NetworkBehaviour)hub).netId}";
        }
        catch
        {
            return hub.ToString();
        }
    }
}

[HarmonyPatch(typeof(PlayerRolesNetUtils), nameof(PlayerRolesNetUtils.WriteRoleSyncInfoPack))]
public static class PlayerRolesNetUtilsWriteRoleSyncInfoPackPatch
{
    [HarmonyPrefix]
    private static bool WriteRoleSyncInfoPackPrefix(NetworkWriter writer, RoleSyncInfoPack info)
    {
        try
        {
            WritePlayersSafely(writer, info._receiverHub);
        }
        catch (Exception ex)
        {
            Log.Warn($"[RoleSyncGuard] RoleSyncInfoPack safe writer failed: {ex}");
            NetworkWriterExtensions.WriteUShort(writer, 0);
        }

        return false;
    }

    private static void WritePlayersSafely(NetworkWriter writer, ReferenceHub receiverHub)
    {
        if (!TryGetReceiverNetId(receiverHub, out var receiverNetId))
        {
            NetworkWriterExtensions.WriteUShort(writer, 0);
            return;
        }

        var payloads = new List<RoleSyncPayload>();

        foreach (var targetHub in ReferenceHub.AllHubs.ToArray())
        {
            if (!TryCreatePayload(receiverHub, targetHub, out var payload))
                continue;

            payloads.Add(payload);
        }

        NetworkWriterExtensions.WriteUShort(writer, (ushort)payloads.Count);

        foreach (var payload in payloads)
        {
            foreach (var b in payload.Bytes)
                writer.WriteByte(b);

            payload.TargetHub.roleManager.PreviouslySentRole[receiverNetId] = payload.Role;
        }
    }

    private static bool TryCreatePayload(ReferenceHub receiverHub, ReferenceHub targetHub, out RoleSyncPayload payload)
    {
        payload = default;
        NetworkWriterPooled payloadWriter = null;

        try
        {
            if (!TryGetPackRole(receiverHub, targetHub, out var targetRole))
                return false;

            payloadWriter = NetworkWriterPool.Get();
            new RoleSyncInfo(targetHub, targetRole, receiverHub, null).Write(payloadWriter);

            var segment = payloadWriter.ToArraySegment();
            var copy = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array, segment.Offset, copy, 0, segment.Count);

            payload = new RoleSyncPayload(targetHub, targetRole, copy);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warn(
                $"[RoleSyncGuard] Skipping RoleSyncInfoPack entry target={DescribeHub(targetHub)} receiver={DescribeHub(receiverHub)}: {ex}");
            return false;
        }
        finally
        {
            if (payloadWriter != null)
                NetworkWriterPool.Return(payloadWriter);
        }
    }

    private static bool TryGetPackRole(ReferenceHub receiverHub, ReferenceHub targetHub, out RoleTypeId targetRole)
    {
        targetRole = RoleTypeId.None;

        try
        {
            if (targetHub == null || targetHub.roleManager == null)
                return false;

            if (((NetworkBehaviour)targetHub).isLocalPlayer || ((NetworkBehaviour)targetHub).netId == 0)
                return false;

            var currentRole = targetHub.roleManager.CurrentRole;
            if (currentRole == null || currentRole.RoleTypeId == RoleTypeId.Destroyed)
                return false;

            targetRole = currentRole is IObfuscatedRole obfuscatedRole
                ? obfuscatedRole.GetRoleForUser(receiverHub)
                : currentRole.RoleTypeId;

            return true;
        }
        catch (Exception ex)
        {
            Log.Warn($"[RoleSyncGuard] Failed to resolve pack role for {DescribeHub(targetHub)}: {ex.Message}");
            return false;
        }
    }

    private static bool TryGetReceiverNetId(ReferenceHub receiverHub, out uint receiverNetId)
    {
        receiverNetId = 0;

        try
        {
            if (receiverHub == null)
                return false;

            receiverNetId = ((NetworkBehaviour)receiverHub).netId;
            return receiverNetId != 0;
        }
        catch
        {
            return false;
        }
    }

    private static string DescribeHub(ReferenceHub hub)
    {
        if (hub == null)
            return "<null>";

        try
        {
            return $"{hub.nicknameSync?.MyNick ?? hub.ToString()}#{hub.PlayerId}/{((NetworkBehaviour)hub).netId}";
        }
        catch
        {
            return hub.ToString();
        }
    }

    private readonly struct RoleSyncPayload
    {
        public RoleSyncPayload(ReferenceHub targetHub, RoleTypeId role, byte[] bytes)
        {
            TargetHub = targetHub;
            Role = role;
            Bytes = bytes;
        }

        public ReferenceHub TargetHub { get; }
        public RoleTypeId Role { get; }
        public byte[] Bytes { get; }
    }
}
