using System;
using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp0492;
using MEC;
using PlayerRoles;
using ProjectMER.Features;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomRoles.Fifthists;
using Slafight_Plugin_EXILED.CustomTeams;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

/// <summary>第五教会の守護者。強い耐久力と近距離の第五汚染を持つ。</summary>
public sealed class Scp3005 : CustomRole
{
    public override string Name => "SCP-3005";
    public override string Description =>
        "第五的なピンクの光を放つ、謎に包まれた存在。\n" +
        "近づく敵を第五の力で侵食する。二つの能力を使い、第五教会を導け。";
    public override string CustomInfo => "<color=#ff00fa>SCP-3005</color>";
    public override CustomTeam Team => CustomTeam.Get<ScpTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.Scp0492;
    public override Vector3? SpawnPosition => PositionProvider.GetNtfSpawnPosition();
    public override float? MaxHealth => 55556f;
    public override float? Health => 55555f;
    public override IReadOnlyList<RoleEffect> Effects => [RoleEffect.Of<MovementBoost>(50)];
    public override string Objective => "第五教会を守り、施設に第五を広げろ。";

    protected override void RegisterEvents()
    {
        Hook(
            () => Exiled.Events.Handlers.Player.Hurting += OnHurting,
            () => Exiled.Events.Handlers.Player.Hurting -= OnHurting);
        Hook(
            () => Exiled.Events.Handlers.Scp0492.ConsumedCorpse += OnConsumedCorpse,
            () => Exiled.Events.Handlers.Scp0492.ConsumedCorpse -= OnConsumedCorpse);
    }

    protected override void OnSpawned()
    {
        // 能力は役職と寿命を共にする。役職が外れると PlayerScope が自動で取り上げる。
        AbilityBase.Give<Scp3005Mindblast>(Player);
        AbilityBase.Give<Scp3005Sound>(Player);

        try
        {
            var schematic = ObjectSpawner.SpawnSchematic("SCP3005", Player.Position, Player.Rotation);
            Scope.OnDispose(_ =>
            {
                if (schematic != null)
                    schematic.Destroy();
            });
            Scope.RunLoop(0.05f, owner =>
            {
                if (schematic != null && Check(owner))
                {
                    schematic.Position = owner.Position;
                    schematic.Rotation = owner.Rotation;
                }
            });
        }
        catch (System.Exception exception)
        {
            Log.Warn($"[Scp3005] SCP3005 schematic could not be spawned: {exception.Message}");
        }

        Scope.RunLoop(1.5f, ApplyAura);
    }

    private void ApplyAura(Player source)
    {
        if (!Check(source) || !source.IsAlive) return;
        foreach (Player target in Player.List)
        {
            if (!target.IsSafePlayer() || target == source || !target.IsAlive ||
                target.IsInTeam<ScpTeam>() || target.IsInTeam<FifthistTeam>()) continue;

            if (Vector3.Distance(source.Position, target.Position) <= 2.75f)
            {
                target.Hurt(source, 25f, DamageType.Unknown, null, "<color=#ff00fa>第五的</color>な力による影響");
                source.ShowHitMarker();
            }
        }
    }

    private void OnHurting(HurtingEventArgs ev)
    {
        if (!IsMine(ev.Player) || ev.Attacker is null || ev.Attacker == Player || ev.Attacker.IsInTeam<ScpTeam>())
            return;

        ev.IsAllowed = false;
        if (ev.Attacker.IsSafePlayer())
        {
            ev.Attacker.Hurt(Player, 20f, DamageType.Unknown, null, "<color=#ff00fa>第五的</color>な力による反撃");
            if (ev.Attacker.IsInTeam<FifthistTeam>())
                ev.Attacker.ShowHint("第五に反逆するとは何事か？", 2f);
        }
    }

    private void OnConsumedCorpse(ConsumedCorpseEventArgs ev)
    {
        if (!Check(ev.Player) || ev.Ragdoll?.Owner is not { IsAlive: false } victim)
            return;

        ev.ConsumeHeal = 0f;
        Vector3 position = ev.Ragdoll.Position + Vector3.up * 0.15f;
        Scope.Delay(0.15f, _ =>
        {
            if (!victim.IsSafePlayer()) return;
            if (CustomRole.Spawn<FifthistMarionette>(victim) is { } role)
                Scope.Delay(0.1f, __ =>
                {
                    if (role.Check(victim)) victim.Position = position;
                });
        });
    }
}
