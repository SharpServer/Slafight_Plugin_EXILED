using System.Collections.Generic;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp0492;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomRoles.Fifthists;
using Slafight_Plugin_EXILED.CustomTeams;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

/// <summary>第五教会を導く SCP。普通の攻撃では傷つかず、近づく敵を第五の力で侵食します。</summary>
public sealed class Scp3005 : CustomRole
{
    public override string Name => "SCP-3005";
    public override string Description =>
        "第五的なピンクの光を放つ、謎に包まれた存在。\n" +
        "普通はダメージを受けることはなく、アビリティを使用することで\n" +
        "第五的なミサイルや閃光を引き起こせる。\n" +
        $"<color={ServerColors.Magenta}>第五教会に道を示し、施設を第五せよ！</color>";
    protected override float HintDuration => 8f;
    public override string CustomInfo => "SCP-3005";
    public override CustomTeam Team => CustomTeam.Get<ScpTeam>();
    public override string HudLabel => $"<color={ServerColors.Magenta}>SCP-3005</color>";
    public override string TeamLabel => $"{CustomTeam.Get<ScpTeam>().HudName} - {CustomTeam.Get<FifthistTeam>().HudName}";
    public override string Objective => "第五教会に道を示し、施設を占領せよ";
    public override RoleTypeId BaseRole => RoleTypeId.Scp0492;
    public override Vector3? SpawnPosition => PositionProvider.GetNtfSpawnPosition();
    internal const string NeutralizedSchematic = "SCP3005_N";
    public override string DeathSchematic => NeutralizedSchematic;
    public override float? MaxHealth => 55556f;
    public override float? Health => 55555f;
    public override IReadOnlyList<RoleEffect> Effects => [RoleEffect.Of<Slowness>(25)];

    /// <summary>脱出すると第五教会 司祭になります。</summary>
    public override SpawnSetRoleDefinition? Escape(EscapeContext escape) => SpawnSetRoleDefinition.Custom<FifthistPriest>();

    protected override void RegisterEvents()
    {
        Hook(
            () => Exiled.Events.Handlers.Player.Hurting += OnHurting,
            () => Exiled.Events.Handlers.Player.Hurting -= OnHurting);
        Hook(
            () => Exiled.Events.Handlers.Player.Dying += OnDying,
            () => Exiled.Events.Handlers.Player.Dying -= OnDying);
        Hook(
            () => Exiled.Events.Handlers.Scp0492.ConsumedCorpse += OnConsumedCorpse,
            () => Exiled.Events.Handlers.Scp0492.ConsumedCorpse -= OnConsumedCorpse);
    }

    protected override void OnSpawned()
    {
        // 能力は役職と寿命を共にする。役職が外れると PlayerScope が自動で取り上げる。
        AbilityBase.Give<Scp3005Mindblast>(Player);
        AbilityBase.Give<Scp3005Sound>(Player);
        AbilityBase.Give<Scp3005Afterglow>(Player);

        WearModels();

        ApplyAura(Player);
        Scope.RunLoop(1.5f, ApplyAura);
    }

    /// <summary>
    /// 光の強い通常モデルと、アクセシビリティ用の控えめなモデルを閲覧者の設定で出し分けます。
    /// 素体は隠し、通常モデルの Sphere を体の当たり判定にします。アクセシビリティ用も同じ形です。
    /// </summary>
    private void WearModels()
    {
        Player.Wear("SCP3005", new WearOptions
        {
            Slot = "scp3005-default",
            Visibility = viewer => !ServerSpecificUserSettings.IsAccessibilityModeEnabled(viewer),
            ReplaceBody = true,
            Hitboxes = [new WearHitboxOptions()],
        });
        Player.Wear("SCP3005_A", new WearOptions
        {
            Slot = "scp3005-accessibility",
            Visibility = ServerSpecificUserSettings.IsAccessibilityModeEnabled,
            IgnoreInvisibility = true,
        });

        Light light = Light.Create(Player.Position, null, null, true, Color.magenta);
        light.Range = 10f;
        light.Intensity = 1.25f;
        Player.Wear(light, new WearOptions
        {
            Slot = "scp3005-light",
            Offset = new Vector3(0f, -0.08f, 0f),
            IgnoreInvisibility = true,
        });
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
                target.Hurt(source, 25f, DamageType.Unknown, null, $"<color={ServerColors.Magenta}>第五的</color>な力による影響");
                source.ShowHitMarker();
            }
        }
    }

    private void OnHurting(HurtingEventArgs ev)
    {
        // 反ミーム保護中の相手には、3005 からのダメージを一切通さない。
        if (IsMine(ev.Attacker) && ev.Player.HasFlag(SpecificFlagType.AntiMemeEffectDisabled))
        {
            ev.IsAllowed = false;
            return;
        }

        if (!IsMine(ev.Player) || ev.Attacker is null || Is<Scp3005>(ev.Attacker))
            return;

        // 陥没穴に落ちている間だけは普通に傷つく。
        if (ev.Player.IsEffectActive<Sinkhole>())
            return;

        ev.IsAllowed = false;
        if (!ev.Attacker.IsSafePlayer()) return;

        ev.Attacker.Hurt(Player, 20f, DamageType.Unknown, null, $"<color={ServerColors.Magenta}>第五的</color>な力による影響");
        if (ev.Attacker.IsInTeam<FifthistTeam>())
            ev.Attacker.ShowHint("第五に反逆するとは何事か！？");
    }

    private void OnDying(DyingEventArgs ev)
    {
        if (!ev.IsAllowed || !IsMine(ev.Player)) return;

        CassieHelper.AnnounceTermination(ev, "SCP 3 0 0 5", $"<color={ServerColors.Magenta}>SCP-3005</color>", true);
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
