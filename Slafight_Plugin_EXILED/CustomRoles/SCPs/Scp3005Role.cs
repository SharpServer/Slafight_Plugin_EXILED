using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp0492;
using Exiled.Events.Handlers;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Player = Exiled.API.Features.Player;
using Scp1344 = Exiled.API.Features.Items.Scp1344;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp3005Role : CRole
{
    protected override string RoleName { get; set; } = "SCP-3005";
    protected override string Description { get; set; } =
        "第五的なピンクの光を放つ、謎に包まれた存在。\n" +
        "普通はダメージを受けることはなく、アビリティを使用することで\n" +
        "第五的なミサイルや閃光を引き起こせる。\n" +
        "<color=#ff00fa>第五教会に道を示し、施設を第五せよ！</color>";
    protected override float DescriptionDuration { get; set; } = 8f;
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp3005;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "SCP-3005";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp0492;
    protected override float? SpawnMaxHealth => 55556f;
    protected override string SpawnCustomInfo => "SCP-3005";

    public override void RegisterEvents()
    {
        Scp0492.ConsumedCorpse += OnConsumed;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Scp0492.ConsumedCorpse -= OnConsumed;
        base.UnregisterEvents();
    }

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.Health = player.MaxHealth - 1;
        player.EnableEffect(EffectType.MovementBoost, 50);

        RoleSchematicWears.WearScp3005(player);

        player.AddAbility<MagicMissileAbility>();
        player.AddAbility<SoundOfFifthAbility>();

        Timing.RunCoroutine(WaitAndTeleport(player));
        Timing.RunCoroutine(Scp3005Coroutine(player));
    }
    
    protected override void OnRoleDying(DyingEventArgs ev)
    {
        RoleSchematicWears.SpawnScp3005DeathModel(ev.Player);

        if (FacilityControlRoom.IsAntiMemeProtocolActive && ev.Attacker is null)
        {
            CassieHelper.AnnounceTermination(ev, "SCP 3 0 0 5", $"<color={Team.GetTeamColor()}>{RoleName}</color>", TerminationCause.AntiMeme(), true);
        }
        else
        {
            CassieHelper.AnnounceTermination(ev, "SCP 3 0 0 5", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        }
        base.OnRoleDying(ev);
    }

    protected override void OnRoleHurting(HurtingEventArgs ev)
    {
        if (ev.Attacker != null && ev.Attacker?.GetCustomRole() != CRoleTypeId)
        {
            var hasGoggles = ev.Attacker != null && ev.Attacker.Items
                .OfType<Scp1344>()
                .Any(i => CItem.TryGet(i, out var ci) && ci is AntiMemeGoggle && i.IsWorn);
            if (ev.Player.IsEffectActive<Sinkhole>() || hasGoggles) return;
            ev.IsAllowed = false;
            ev.Attacker?.Hurt(ev.Player, 20f, DamageType.Unknown,null,  "<color=#ff00fa>第五的</color>な力による影響");

            if (ev.Attacker != null && ev.Attacker.GetTeam() == CTeam.Fifthists)
                ev.Attacker.ShowHint("第五に反逆するとは何事か！？");
        }
    }

    private void OnConsumed(ConsumedCorpseEventArgs ev)
    {
        if (!Check(ev.Player) || ev.Ragdoll.Owner.IsAlive) return;
        ev.ConsumeHeal = 0f;
        var target = ev.Ragdoll.Owner;
        target?.SetRole(CRoleTypeId.FifthistMarionette);
        Timing.CallDelayed(RoleSpawnTimings.FastSpawnFinalize, () =>
            TrySetPosition(target, ev.Ragdoll.Position + Vector3.up * 0.15f, "consumed corpse finalize"));
    }

    protected override void OnRoleSpawningRagdoll(SpawningRagdollEventArgs ev)
    {
        ev.IsAllowed = false;
    }
    
    private IEnumerator<float> WaitAndTeleport(Player player)
    {
        // スポーンポイントが初期化されるまで待機（最大10秒）
        float elapsed = 0f;
        while (MapFlags.Scp3005SpawnPoint == Vector3.zero && elapsed < 10f)
        {
            yield return Timing.WaitForSeconds(RoleSpawnTimings.SpawnPointPollInterval);
            elapsed += RoleSpawnTimings.SpawnPointPollInterval;
            if (!Check(player)) yield break;
        }

        yield return Timing.WaitForSeconds(RoleSpawnTimings.AfterRoleSet);
        if (!Check(player))
            yield break;

        TrySetPosition(player, MapFlags.Scp3005SpawnPoint, nameof(WaitAndTeleport));
    }

    private static bool IsSafePlayerTarget(Player player)
    {
        try
        {
            return player?.ReferenceHub != null &&
                   (player.IsNPC || player.IsConnected) &&
                   player.Role.Type != RoleTypeId.Destroyed;
        }
        catch
        {
            return false;
        }
    }

    private static void TrySetPosition(Player player, Vector3 position, string context)
    {
        if (!IsSafePlayerTarget(player))
        {
            Log.Warn($"[Scp3005Role] Skipped teleport during {context}: target is no longer valid.");
            return;
        }

        try
        {
            player.Position = position;
        }
        catch (Exception ex)
        {
            Log.Warn($"[Scp3005Role] Skipped teleport during {context}: {ex.Message}");
        }
    }

    private static IEnumerator<float> Scp3005Coroutine(Player player)
    {
        for (;;)
        {
            if (player.GetCustomRole() != CRoleTypeId.Scp3005)
                yield break;

            foreach (var target in Player.List)
            {
                if (target == null || target == player || !target.IsAlive)
                    continue;

                if (target.GetTeam() == CTeam.SCPs || target.GetTeam() == CTeam.Fifthists)
                    continue;
                    
                var hasGoggles = target.Items
                    .OfType<Scp1344>()
                    .Any(i => CItem.TryGet(i, out var ci) && ci is AntiMemeGoggle && i.IsWorn);
                if (hasGoggles)  continue;

                var distance = Vector3.Distance(player.Position, target.Position);
                if (!(distance <= 2.75f)) continue;
                target.Hurt(player, 25f, DamageType.Unknown,null,  "<color=#ff00fa>第五的</color>な力による影響");
                player.ShowHitMarker();
            }

            if (FacilityControlRoom.HasAntiMemeProtocolActivatedInPast)
            {
                player.DisableEffect(EffectType.Slowness);
                player.EnableEffect(EffectType.MovementBoost, 25);
            }
            else
            {
                player.DisableEffect(EffectType.MovementBoost);
                player.EnableEffect(EffectType.Slowness, 25);
            }

            if (FacilityControlRoom.IsAntiMemeProtocolActive)
                player.Hurt(100f, "<color=#ff00fa>アンチミームプロトコロル</color>により終了された");

            yield return Timing.WaitForSeconds(1.5f);
        }
    }
}
