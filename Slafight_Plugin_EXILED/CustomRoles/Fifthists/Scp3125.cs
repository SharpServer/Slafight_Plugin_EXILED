using System;
using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.Keycards;
using Slafight_Plugin_EXILED.CustomTeams;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthists;

/// <summary>反ミーム部門を壊滅させた第五の存在。近づく者を少しずつ傷つけます。</summary>
public sealed class Scp3125 : CustomRole
{
    public override string Name => "SCP-3125";
    public override string Description => "反ミーム部門を壊滅させた第五の存在。残る標的を追え。";
    public override string CustomInfo => "<color=#ff0090>SCP-3125</color>";
    public override CustomTeam Team => CustomTeam.Get<FifthistTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.Scp106;
    public override float? MaxHealth => 55555f;
    public override RoleVoiceSettings Voice => RoleVoiceSettings.WithProximity();
    public override IReadOnlyList<RoleEffect> Effects => [RoleEffect.Of<Slowness>(30)];

    protected override void OnSpawned()
    {
        Scope.RunLoop(2f, ApplyAura);
        Player.MaxHealth = 55555f;
        Player.Health = 55555f;
    }

    private void ApplyAura(Player source)
    {
        if (!Check(source) || !source.IsAlive) return;
        foreach (Player target in Player.List)
        {
            if (!target.IsSafePlayer() || target == source || !target.IsAlive ||
                target.IsInTeam<FifthistTeam>() || target.IsInTeam<ScpTeam>()) continue;
            if (Vector3.Distance(source.Position, target.Position) <= 2.75f)
                target.Hurt(source, 1f, Exiled.API.Enums.DamageType.Unknown, null, "<color=#ff00fa>第五的</color>な力による影響");
        }
    }

    protected override void RegisterEvents() => Hook(
        () => Exiled.Events.Handlers.Player.Hurting += OnHurting,
        () => Exiled.Events.Handlers.Player.Hurting -= OnHurting);

    private void OnHurting(Exiled.Events.EventArgs.Player.HurtingEventArgs ev)
    {
        if (IsMine(ev.Attacker) && ev.Amount > 10f)
            ev.IsAllowed = false;

        if (IsMine(ev.Player) && !ev.Player.IsEffectActive<Sinkhole>())
            ev.IsAllowed = false;
    }
}
