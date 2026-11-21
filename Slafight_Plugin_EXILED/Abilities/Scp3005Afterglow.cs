using System.Collections.Generic;
using CustomPlayerEffects;
using DrawableLine;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using LabApi.Events.Arguments.PlayerEvents;
using MEC;
using Mirror;
using NetworkManagerUtils.Dummies;
using PlayerRoles;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomRoles.SCPs;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

using LabPlayerEvents = LabApi.Events.Handlers.PlayerEvents;
using Light = Exiled.API.Features.Toys.Light;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using ServerHandlers = Exiled.Events.Handlers.Server;
using ShieldBreakMessage = PlayerRoles.PlayableScps.HumeShield.DynamicHumeShieldController.ShieldBreakMessage;

namespace Slafight_Plugin_EXILED.Abilities;

public sealed class Scp3005Afterglow : AbilityBase
{
    private const float PlacementCooldown = 5f;
    private const float ReturnCooldown = 150f;
    private const float AnchorHealth = 500f;
    private const float HealthDisplayInterval = 0.1f;
    private const float BreakSoundLifetime = 2f;
    private const float TrailDuration = 0.36f;
    private const int TrailSegments = 10;
    private const string AnchorName = "SCP-3005の残光";

    private static readonly Color TrailPurple = new Color(0.51f, 0.09f, 0.91f);
    private static readonly Color ArrivalPurple = new Color(0.86f, 0.48f, 1f);
    private static readonly Vector3 TrailHeight = Vector3.up * 0.35f;

    private Npc anchor;
    private Npc breakSoundSource;
    private SchematicObject model;
    private Light light;
    private Vector3 destination;

    public override string Name => "残光";
    public override string Description => "今いる場所に残光を置き、5秒後からその場所へ帰還できる。残光のHPは500。帰還または破壊から150秒後に再設置できる。";
    public override float Cooldown => anchor is null ? PlacementCooldown : ReturnCooldown;

    protected override void OnGranted()
    {
        LabPlayerEvents.ChangingRole += OnAnchorChangingRole;
        PlayerHandlers.Died += OnNpcDied;
        PlayerHandlers.SpawningRagdoll += OnSpawningRagdoll;
        ServerHandlers.RoundEnded += OnRoundEnded;
        Scope.OnDispose(_ => OnRevoked());
    }

    protected override void OnRevoked()
    {
        LabPlayerEvents.ChangingRole -= OnAnchorChangingRole;
        PlayerHandlers.Died -= OnNpcDied;
        PlayerHandlers.SpawningRagdoll -= OnSpawningRagdoll;
        ServerHandlers.RoundEnded -= OnRoundEnded;
        ClearAnchor();
    }

    protected override bool CanUse(out string failureReason)
    {
        if (!Round.IsEnded)
            return base.CanUse(out failureReason);

        failureReason = "ラウンド終了後は使えません。";
        return false;
    }

    protected override void OnUsed()
    {
        if (anchor is not { } point)
        {
            PlaceAnchor();
            return;
        }

        if (!point.IsConnected || !point.IsAlive || model == null)
        {
            BreakAnchor();
            return;
        }

        Scope.Track(Timing.RunCoroutine(ReturnToAnchor(point, Player.Position, destination)));
    }

    private void PlaceAnchor()
    {
        destination = Player.Position;
        bool placed = false;
        try
        {
            if (!ObjectSpawner.TrySpawnSchematic(Scp3005.NeutralizedSchematic, destination, Player.Rotation, out model))
                return;

            model.SetCollidable(false);
            light = Light.Create(destination, null, null, false, Color.magenta);
            light.Range = 4f;
            light.Intensity = 0.3f;
            light.Spawn();

            // Npc.Spawn の遅延役職設定を挟まず、透明化と座標・HPを同じフレームで確定させる。
            ReferenceHub hub = DummyUtils.SpawnDummy(AnchorName);
            if (hub == null)
                return;

            anchor = new Npc(hub);
            InternalNpcRegistry.Register(anchor, InternalNpcCategory.Generic);
            Round.IgnoredPlayers.Add(hub);
            Player.Dictionary[anchor.GameObject] = anchor;
            anchor.Role.Set(RoleTypeId.Scp0492, SpawnReason.ForceClass, RoleSpawnFlags.None);
            anchor.EnableEffect<Fade>(byte.MaxValue);
            anchor.Position = destination;
            anchor.IsNoclipEnabled = true;
            anchor.IsSpectatable = false;
            anchor.MaxHealth = AnchorHealth;
            anchor.Health = AnchorHealth;
            anchor.InfoArea = PlayerInfoArea.CustomInfo;
            Scope.Track(Timing.RunCoroutine(UpdateAnchorNameplate(anchor)));

            placed = true;
            Player.ShowHint("残光を設置しました。5秒後から帰還できます。", 3f);
        }
        finally
        {
            if (!placed)
            {
                ClearAnchor();
                RestartCooldown(0f);
                Player.ShowHint("残光を設置できませんでした。", 3f);
            }
        }
    }

    private IEnumerator<float> UpdateAnchorNameplate(Npc point)
    {
        while (ReferenceEquals(anchor, point) && point.IsConnected && point.IsAlive)
        {
            int current = Mathf.CeilToInt(point.Health);
            int maximum = Mathf.CeilToInt(point.MaxHealth);
            string info = $"{AnchorName}\n({current}/{maximum})";
            if (point.CustomInfo != info)
                point.CustomInfo = info;

            yield return Timing.WaitForSeconds(HealthDisplayInterval);
        }
    }

    private IEnumerator<float> ReturnToAnchor(Npc point, Vector3 origin, Vector3 target)
    {
        Vector3 start = origin + TrailHeight;
        Vector3 end = target + TrailHeight;
        Vector3 previous = start;
        float arc = Mathf.Min(Vector3.Distance(origin, target) * 0.12f, 1.2f);

        for (int i = 1; i <= TrailSegments; i++)
        {
            if (!CanFinishReturn(point)) yield break;

            float progress = (float)i / TrailSegments;
            Vector3 next = Vector3.Lerp(start, end, progress) + Vector3.up * (Mathf.Sin(progress * Mathf.PI) * arc);
            if ((next - previous).sqrMagnitude > 0.0001f)
                SendLine(0.16f, Color.Lerp(TrailPurple, ArrivalPurple, progress), [previous, next]);

            previous = next;
            yield return Timing.WaitForSeconds(TrailDuration / TrailSegments);
        }

        if (!CanFinishReturn(point)) yield break;
        if (!Player.IsSafePlayer() || !Player.IsAlive || RoleHold.IsHeld(Player)) yield break;

        ClearAnchor();
        Player.Position = target;
        RestartCooldown(ReturnCooldown);
        SendLine(0.22f, ArrivalPurple, DrawableLines.GetCircle(end, 0.55f, horizontalAxis: false, segments: 10));
        Player.ShowHint("残光に帰還しました。", 3f);
    }

    private bool CanFinishReturn(Npc point)
    {
        if (!ReferenceEquals(anchor, point))
            return false;

        if (point.IsConnected && point.IsAlive && model != null)
            return true;

        BreakAnchor();
        return false;
    }

    private static void SendLine(float duration, Color color, Vector3[] positions) =>
        SendToReadyClients(new DrawableLineMessage(duration, color, positions));

    private static void SendToReadyClients<T>(T message) where T : struct, NetworkMessage
    {
        foreach (Player viewer in Player.Enumerable)
        {
            if (NetGuards.IsReadyClient(viewer))
                viewer.ReferenceHub.connectionToClient.Send(message);
        }
    }

    private void OnAnchorChangingRole(PlayerChangingRoleEventArgs ev)
    {
        // シールド破壊音はクライアント側で元の役職に追従する。
        if (anchor is { } point && ev.ChangeReason == RoleChangeReason.Died &&
            ReferenceEquals(ev.Player?.ReferenceHub, point.ReferenceHub))
            ev.IsAllowed = false;
    }

    private void OnNpcDied(DiedEventArgs ev)
    {
        if (anchor is not { } point || !ReferenceEquals(ev.Player, point))
            return;

        anchor = null;
        breakSoundSource = point;
        RestartCooldown(ReturnCooldown);
        Scope.Delay(BreakSoundLifetime, _ =>
        {
            if (ReferenceEquals(breakSoundSource, point))
                ClearAnchor();
        });

        point.IsGodModeEnabled = true;
        point.CustomInfo = null;
        WearManager.SetBodyHitboxes(point, false);
        ClearAnchorVisuals();

        // PlayShieldBreakSound と同じメッセージを、受信可能な実クライアントに送る。
        SendToReadyClients(new ShieldBreakMessage { Target = point.ReferenceHub });
        if (Player.IsSafePlayer() && Player.IsAlive)
        {
            SendToReadyClients(new ShieldBreakMessage { Target = Player.ReferenceHub });
            Player.ShowHint("残光が破壊されました。再設置まで150秒。", 4f);
        }
    }

    private void OnSpawningRagdoll(SpawningRagdollEventArgs ev)
    {
        if (anchor is not null && ReferenceEquals(ev.Player, anchor))
            ev.IsAllowed = false;
    }

    private void OnRoundEnded(RoundEndedEventArgs ev) => ClearAnchor();

    private void BreakAnchor()
    {
        ClearAnchor();
        RestartCooldown(ReturnCooldown);
        if (Player.IsSafePlayer() && Player.IsAlive)
            Player.ShowHint("残光が破壊されました。再設置まで150秒。", 4f);
    }

    private void ClearAnchor()
    {
        Npc point = anchor ?? breakSoundSource;
        bool wasSoundSource = breakSoundSource is not null;
        anchor = null;
        breakSoundSource = null;
        try
        {
            if (point is not null)
            {
                InternalNpcRegistry.Unregister(point.Id);
                if (point.IsConnected)
                {
                    if (wasSoundSource)
                        WearManager.SetBodyHitboxes(point, true);

                    point.Destroy();
                }
                else
                    Round.IgnoredPlayers.Remove(point.ReferenceHub);
            }
        }
        finally
        {
            ClearAnchorVisuals();
        }
    }

    private void ClearAnchorVisuals()
    {
        try
        {
            if (light?.Base != null)
                light.Destroy();
        }
        finally
        {
            light = null;
            if (model != null)
                model.Destroy();
            model = null;
        }
    }
}
