using System;
using System.Collections.Generic;
using System.Linq;
using CentralAuth;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using HarmonyLib;
using MEC;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl.NetworkMessages;
using PlayerRoles.SpawnData;
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

        foreach (var receiverHub in ReferenceHub.AllHubs.ToArray())
        {
            TrySendToReceiver(manager, targetHub, receiverHub);
        }
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

            if (targetRole is RoleTypeId.None or RoleTypeId.Destroyed)
                return false;

            var connection = receiverHub.connectionToClient;
            connection.Send(
                new RoleSyncInfo(targetHub, targetRole, receiverHub, writer),
                channelId: 0);

            manager.PreviouslySentRole[receiverHub.netId] = targetRole;
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

            if (targetHub.isLocalPlayer)
                return false;

            if (targetHub.Mode == ClientInstanceMode.Unverified)
                return false;

            var currentRole = targetHub.roleManager.CurrentRole;
            if (currentRole == null || currentRole.RoleTypeId is RoleTypeId.None or RoleTypeId.Destroyed)
                return false;

            targetNetId = targetHub.netId;
            return targetNetId != 0;
        }
        catch (Exception ex)
        {
            Log.Warn($"[RoleSyncGuard] Invalid role sync target: {ex.Message}");
            return false;
        }
    }

    private static bool IsValidReceiver(ReferenceHub receiverHub)
    {
        try
        {
            if (receiverHub == null)
                return false;

            if (receiverHub.isLocalPlayer)
                return false;

            if (receiverHub.Mode == ClientInstanceMode.Unverified)
                return false;

            var connection = receiverHub.connectionToClient;
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
            return $"{hub.nicknameSync?.MyNick ?? hub.ToString()}#{hub.PlayerId}/{hub.netId}";
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
    private static readonly HashSet<ReferenceHub> PendingInitialPacks = [];

    [HarmonyPrefix]
    private static bool HandleSpawnedPlayerPrefix(ReferenceHub hub)
    {
        if (!NetworkServer.active)
            return true;

        TrySendInitialRolePack(hub, Time.realtimeSinceStartup + RoleSpawnTimings.RoleSyncInitialPackMaxWait, 0f);
        return false;
    }

    private static void TrySendInitialRolePack(ReferenceHub hub, float deadline, float readySince)
    {
        if (hub == null)
            return;

        if (hub.isLocalPlayer)
            return;

        if (!IsReadyForInitialRolePack(hub))
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                Log.Warn($"[RoleSyncGuard] Initial role pack skipped; receiver did not become ready: {DescribeHub(hub)}");
                return;
            }

            if (!PendingInitialPacks.Add(hub))
                return;

            Timing.CallDelayed(RoleSpawnTimings.RoleSyncInitialPackRetryInterval, () =>
            {
                PendingInitialPacks.Remove(hub);
                TrySendInitialRolePack(hub, deadline, 0f);
            });

            return;
        }

        if (readySince <= 0f)
            readySince = Time.realtimeSinceStartup;

        if (Time.realtimeSinceStartup - readySince < RoleSpawnTimings.RoleSyncInitialPackReadySettle)
        {
            if (!PendingInitialPacks.Add(hub))
                return;

            Timing.CallDelayed(RoleSpawnTimings.RoleSyncInitialPackRetryInterval, () =>
            {
                PendingInitialPacks.Remove(hub);
                TrySendInitialRolePack(hub, deadline, readySince);
            });

            return;
        }

        try
        {
            hub.connectionToClient.Send(
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

            var connection = hub.connectionToClient;
            return hub.netId != 0 &&
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
            return $"{hub.nicknameSync?.MyNick ?? hub.ToString()}#{hub.PlayerId}/{hub.netId}";
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
            writer.WriteUShort(0);
        }

        return false;
    }

    private static void WritePlayersSafely(NetworkWriter writer, ReferenceHub receiverHub)
    {
        if (!TryGetReceiverNetId(receiverHub, out var receiverNetId))
        {
            writer.WriteUShort(0);
            return;
        }

        var payloads = new List<RoleSyncPayload>();

        foreach (var targetHub in ReferenceHub.AllHubs.ToArray())
        {
            if (!TryCreatePayload(receiverHub, targetHub, out var payload))
                continue;

            payloads.Add(payload);
        }

        writer.WriteUShort((ushort)payloads.Count);

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
            if (!IsValidPackTarget(receiverHub, targetHub, out _))
                return false;

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

    private static bool IsHiddenFromReceiver(ReferenceHub receiverHub, ReferenceHub targetHub)
    {
        try
        {
            Player receiverPlayer = Player.Get(receiverHub);
            Player targetPlayer = Player.Get(targetHub);

            if (receiverPlayer?.ReferenceHub == null || targetPlayer?.ReferenceHub == null)
                return false;

            return targetPlayer.Role is FpcRole fpcRole &&
                   (fpcRole.IsInvisible || fpcRole.IsInvisibleFor.Contains(receiverPlayer));
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetPackRole(ReferenceHub receiverHub, ReferenceHub targetHub, out RoleTypeId targetRole)
    {
        targetRole = RoleTypeId.None;

        try
        {
            if (!IsValidPackTarget(receiverHub, targetHub, out _))
                return false;

            var currentRole = targetHub.roleManager.CurrentRole;
            if (currentRole == null || currentRole.RoleTypeId is RoleTypeId.None or RoleTypeId.Destroyed)
                return false;

            targetRole = currentRole is IObfuscatedRole obfuscatedRole
                ? obfuscatedRole.GetRoleForUser(receiverHub)
                : currentRole.RoleTypeId;

            if (targetRole is not (RoleTypeId.None or RoleTypeId.Destroyed or RoleTypeId.Spectator) &&
                IsHiddenFromReceiver(receiverHub, targetHub))
            {
                // RoleSyncInfo.Write always serializes currentRole's IPublicSpawnDataWriter data
                // regardless of the advertised RoleTypeId, but PlayerRoleManager.InitializeNewRole
                // skips reading any spawn data whenever the advertised role is Spectator. Spoofing
                // to Spectator here without also suppressing that write would leave the receiver's
                // NetworkReader out of sync for the rest of the batched packet (manifests as
                // "Unknown message id" kicks / unrelated OnDeserialize failures). Roles without
                // public spawn data are unaffected, so only skip the entry when it would actually
                // corrupt the stream.
                if (currentRole is IPublicSpawnDataWriter)
                    return false;

                targetRole = RoleTypeId.Spectator;
            }

            return targetRole is not RoleTypeId.None and not RoleTypeId.Destroyed;
        }
        catch (Exception ex)
        {
            Log.Warn($"[RoleSyncGuard] Failed to resolve pack role for {DescribeHub(targetHub)}: {ex.Message}");
            return false;
        }
    }

    private static bool IsValidPackTarget(ReferenceHub receiverHub, ReferenceHub targetHub, out string reason)
    {
        reason = string.Empty;

        try
        {
            if (receiverHub == null)
            {
                reason = "receiver is null";
                return false;
            }

            if (targetHub == null)
            {
                reason = "target is null";
                return false;
            }

            if (ReferenceEquals(receiverHub, targetHub))
            {
                reason = "target is receiver";
                return false;
            }

            if (targetHub.roleManager == null)
            {
                reason = "target roleManager is null";
                return false;
            }

            var targetBehaviour = (NetworkBehaviour)targetHub;
            if (targetBehaviour.isLocalPlayer || targetBehaviour.netId == 0)
            {
                reason = "target has no network identity";
                return false;
            }

            if (targetHub.Mode == ClientInstanceMode.Unverified)
            {
                reason = "target is unverified";
                return false;
            }

            if (targetHub.Mode != ClientInstanceMode.DedicatedServer)
            {
                var targetConnection = targetBehaviour.connectionToClient;
                if (targetConnection == null || !targetConnection.isReady)
                {
                    reason = "target connection is not ready";
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            reason = ex.Message;
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

            receiverNetId = receiverHub.netId;
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
            return $"{hub.nicknameSync?.MyNick ?? hub.ToString()}#{hub.PlayerId}/{hub.netId}";
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
