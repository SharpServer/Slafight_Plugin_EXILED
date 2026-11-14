using CustomPlayerEffects;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomTeams;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthists;

/// <summary>反ミーム部門を壊滅させた第五の存在。傷つかず、近づく者を少しずつ蝕みます。</summary>
public sealed class Scp3125 : CustomRole
{
    public override string Name => "SCP-3125";
    public override string Description =>
        "あなたは反ミーム部門を壊滅させる事に成功した！\n" +
        "残るはかの部門長、<color=#ffa500>マリオンホイーラー</color>を<color=red>殺すだけ</color>だ。";
    public override string HudLabel => $"<color={ServerColors.Magenta}>SCP-3125</color>";
    public override string Objective => "マリオン・ホイーラーを探し出し第五せよ";
    public override string CustomInfo => $"<color={ServerColors.Magenta}>SCP-3125</color>";
    public override CustomTeam Team => CustomTeam.Get<FifthistTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.Scp106;
    public override Vector3? SpawnPosition => PositionProvider.GetNtfSpawnPosition();
    public override float? MaxHealth => 55555f;
    public override RoleVoiceSettings Voice => RoleVoiceSettings.WithProximity();
    public override IReadOnlyList<RoleEffect> Effects => [RoleEffect.Of<Slowness>(30)];

    protected override void RegisterEvents()
    {
        Hook(
            () => Exiled.Events.Handlers.Player.Hurting += OnHurting,
            () => Exiled.Events.Handlers.Player.Hurting -= OnHurting);
        Hook(
            () => Exiled.Events.Handlers.Player.Dying += OnDying,
            () => Exiled.Events.Handlers.Player.Dying -= OnDying);
    }

    // 3 秒待ってから 2 秒ごとに蝕み始める。
    protected override void OnSpawned() => Scope.Delay(1f, _ => Scope.RunLoop(2f, ApplyAura));

    private void ApplyAura(Player source)
    {
        if (!Check(source) || !source.IsAlive) return;

        foreach (Player target in Player.List)
        {
            if (!target.IsSafePlayer() || target == source || !target.IsAlive ||
                target.IsInTeam<FifthistTeam>() || target.IsInTeam<ScpTeam>()) continue;

            if (Vector3.Distance(source.Position, target.Position) > 2.75f) continue;

            target.Hurt(source, 1f, DamageType.Unknown, null, $"<color={ServerColors.Magenta}>第五的</color>な力による影響");
            source.ShowHitMarker();
        }
    }

    private void OnHurting(HurtingEventArgs ev)
    {
        // 自分は一切傷つかず、与えるダメージも 10 以下に限る。
        if (IsMine(ev.Player) || (IsMine(ev.Attacker) && ev.Amount > 10f))
            ev.IsAllowed = false;
    }

    private void OnDying(DyingEventArgs ev)
    {
        if (!ev.IsAllowed || !IsMine(ev.Player)) return;

        CassieHelper.AnnounceTermination(ev, "SCP 3 1 2 5", $"{Team.Colorize(Name)}", true);
    }
}
