using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using PlayerStatsSystem;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class SuspiciousTablet : CItemUsable
{
    public override string DisplayName => "怪しい薬";
    public override string Description =>
        "「誰でも100%安心・安全に一時的な死亡を体験することが可能です！」";

    protected override string UniqueKey => "SuspiciousTablet";
    protected override ItemType BaseItem => ItemType.Adrenaline;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.yellow;

    private const float RespawnTime = 120f;
    private const float RespawnPositionYOffset = 1.05f;
    private const float RestorePositionSafetyDelay = RoleSpawnTimings.RestoreRoleState - RoleSpawnTimings.AfterSpawnFinalize + 0.1f;

    private static readonly Dictionary<int, PendingRespawn> PendingRespawns = [];
    private static readonly Dictionary<int, CoroutineHandle> RespawnCoroutines = [];

    private sealed class PendingRespawn
    {
        public PendingRespawn(Player player)
        {
            PlayerId = player.Id;
            UserId = player.UserId ?? string.Empty;
            RoleInfo = player.GetRoleInfo();
            RespawnPosition = player.Position;
        }

        public int PlayerId { get; }
        public string UserId { get; }
        public PlayerRoleHelpers.PlayerRoleInfo RoleInfo { get; }
        public Vector3 RespawnPosition { get; }
        public Ragdoll? Ragdoll { get; set; }
    }

    protected override void OnWaitingForPlayers()
    {
        ClearRespawnState();
        base.OnWaitingForPlayers();
    }

    protected override void OnUsing(UsingItemEventArgs ev)
    {
        if (ev.Player.GetTeam() is CTeam.SCPs or CTeam.Warriors)
        {
            ev.IsAllowed = false;
        }
        base.OnUsing(ev);
    }

    protected override void OnUsedEffect(UsingItemCompletedEventArgs ev)
    {
        var player = ev.Player;
        if (player is null) return;

        StopRespawnCoroutine(player.Id);
        PendingRespawns[player.Id] = new PendingRespawn(player);

        player.Kill("薬によって仮死状態に至った！");

        if (player.IsAlive)
        {
            PendingRespawns.Remove(player.Id);
            return;
        }

        player.ShowHint($"<color=yellow><b>※{RespawnTime}秒後まで湧かなかった場合に復活します。</b></color>", 8f);
    }

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.Died += OnDied;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.Died -= OnDied;
        ClearRespawnState();
        base.UnregisterEvents();
    }

    private void OnDied(DiedEventArgs ev)
    {
        if (ev.Player is null || ev.Ragdoll is null) return;

        if (!PendingRespawns.TryGetValue(ev.Player.Id, out var pending))
            return;

        PendingRespawns.Remove(ev.Player.Id);

        ev.Ragdoll.DamageHandler = new CustomReasonDamageHandler("自害した");
        pending.Ragdoll = ev.Ragdoll;
        RespawnCoroutines[ev.Player.Id] = Timing.RunCoroutine(RespawnCoroutine(pending));
    }

    private static void ShowFailure(Player? player)
    {
        if (player is null) return;
        EffectedInfoTextProvider.Clear(player);
        player.ShowHint("<color=red><b>あなたが飲んだ薬は不良品だった！！！！！</b></color>");
    }

    private static bool TryGetPlayer(PendingRespawn pending, out Player? player)
    {
        player = Player.Get(pending.PlayerId);
        return player is not null &&
               player.IsConnected &&
               (string.IsNullOrEmpty(pending.UserId) || player.UserId == pending.UserId);
    }

    private static void RestoreRole(Player player, PlayerRoleHelpers.PlayerRoleInfo roleInfo)
    {
        if (roleInfo.Custom == CRoleTypeId.None)
        {
            player.SetRole(roleInfo.Vanilla, RoleSpawnFlags.None);
            return;
        }

        player.SetRole(roleInfo.Custom, RoleSpawnFlags.None);
    }

    private static void RestorePosition(Player player, Vector3 position)
    {
        bool oldGodMode = player.IsGodModeEnabled;
        player.IsGodModeEnabled = true;
        player.Position = position + Vector3.up * RespawnPositionYOffset;
        player.IsGodModeEnabled = oldGodMode;
    }

    private static void StopRespawnCoroutine(int playerId)
    {
        if (!RespawnCoroutines.TryGetValue(playerId, out var handle))
            return;

        Timing.KillCoroutines(handle);
        RespawnCoroutines.Remove(playerId);
    }

    private static void ClearRespawnState()
    {
        PendingRespawns.Clear();

        foreach (var handle in new List<CoroutineHandle>(RespawnCoroutines.Values))
            Timing.KillCoroutines(handle);

        RespawnCoroutines.Clear();
    }

    private IEnumerator<float> RespawnCoroutine(PendingRespawn pending)
    {
        try
        {
            float elapsedTime = 0f;

            while (elapsedTime < RespawnTime)
            {
                if (Round.IsLobby)
                    yield break;

                if (!TryGetPlayer(pending, out var player))
                    yield break;

                if (player!.IsAlive || pending.Ragdoll is null)
                {
                    ShowFailure(player);
                    yield break;
                }

                int secondsLeft = Mathf.CeilToInt(RespawnTime - elapsedTime);
                EffectedInfoTextProvider.Set(player, $"復活まで：{secondsLeft}");

                yield return Timing.WaitForSeconds(1f);
                elapsedTime += 1f;
            }

            if (!TryGetPlayer(pending, out var respawningPlayer))
                yield break;

            if (!respawningPlayer!.IsDead || pending.Ragdoll is null)
            {
                ShowFailure(respawningPlayer);
                yield break;
            }

            EffectedInfoTextProvider.Clear(respawningPlayer);
            RestoreRole(respawningPlayer, pending.RoleInfo);

            yield return Timing.WaitForSeconds(RoleSpawnTimings.AfterSpawnFinalize);

            if (!TryGetPlayer(pending, out var restoredPlayer))
                yield break;

            if (restoredPlayer!.IsDead)
            {
                ShowFailure(restoredPlayer);
                yield break;
            }

            RestorePosition(restoredPlayer, pending.RespawnPosition);

            yield return Timing.WaitForSeconds(RestorePositionSafetyDelay);

            if (!TryGetPlayer(pending, out restoredPlayer))
                yield break;

            if (restoredPlayer!.IsDead)
            {
                ShowFailure(restoredPlayer);
                yield break;
            }

            RestorePosition(restoredPlayer, pending.RespawnPosition);
            pending.Ragdoll.Destroy();
        }
        finally
        {
            RespawnCoroutines.Remove(pending.PlayerId);

            if (TryGetPlayer(pending, out var player))
                EffectedInfoTextProvider.Clear(player);
        }
    }
}
