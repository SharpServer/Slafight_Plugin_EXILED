using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp0492;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;
using UnityEngine;

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
        Exiled.Events.Handlers.Scp0492.ConsumedCorpse += OnConsumed;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Scp0492.ConsumedCorpse -= OnConsumed;
        base.UnregisterEvents();
    }

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.Health = player.MaxHealth - 1;
        player.EnableEffect(EffectType.MovementBoost, 50);

        // ★ Scale は触らない
        LabApiHandler.Schem3005(LabApi.Features.Wrappers.Player.Get(player.ReferenceHub));

        player.AddAbility(new MagicMissileAbility(player));
        player.AddAbility(new SoundOfFifthAbility(player));

        Timing.RunCoroutine(WaitAndTeleport(player));
        Timing.RunCoroutine(Scp3005Coroutine(player));
    }
    
    protected override void OnRoleDying(DyingEventArgs ev)
    {
        if (FacilityControlRoom.IsAntiMemeProtocolActive && ev.Attacker is null)
        {
            Exiled.API.Features.Cassie.MessageTranslated("SCP 3 0 0 5 Successfully neutralized by $pitch_.85 Anti- $pitch_1 Me mu Protocol.", $"<color={Team.GetTeamColor()}>{RoleName}</color> は<color={CTeam.Fifthists.GetTeamColor()}>アンチミームプロトコル</color>により正常に無効化されました。");
        }
        else
        {
            CassieHelper.AnnounceTermination(ev, "SCP 3 0 0 5", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        }
        base.OnRoleDying(ev);
    }

    protected override void OnRoleHurting(HurtingEventArgs ev)
    {
        if (ev.Attacker != null && ev.Attacker?.GetCustomRole() != this.CRoleTypeId)
        {
            var hasGoggles = ev.Attacker != null && ev.Attacker.Items
                .OfType<Scp1344>()
                .Any(i => CItem.TryGet(i, out var ci) && ci is AntiMemeGoggle && i.IsWorn);
            if (ev.Player.IsEffectActive<CustomPlayerEffects.Sinkhole>() || hasGoggles) return;
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
        Timing.CallDelayed(RoleSpawnTimings.FastSpawnFinalize, () => target?.Position = ev.Ragdoll.Position + Vector3.up * 0.15f);
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
        player.Position = MapFlags.Scp3005SpawnPoint;
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
