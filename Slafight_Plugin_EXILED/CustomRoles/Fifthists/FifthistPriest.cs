using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomItems.Keycards;
using Slafight_Plugin_EXILED.CustomItems.Utils;
using Slafight_Plugin_EXILED.CustomRoles.SCPs;
using Slafight_Plugin_EXILED.CustomTeams;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthists;

/// <summary>第五の恩寵を受けた司祭。ピンクの光をまとい、近づく敵を第五の力で傷つけます。</summary>
public sealed class FifthistPriest : FifthistRole
{
    public override string Name => "第五教会 司祭";
    public override string Description => $"非常に<color={ServerColors.Magenta}>第五的</color>な存在の恩寵を受けた第五主義者。\n施設を占領せよ！";
    protected override string InfoName => "Fifthist Priest";
    public override string HudLabel => $"<color={ServerColors.Magenta}>Fifthist: Priest</color>";
    public override string Objective => "あなたは幸福な事に第五の加護を受けている。全てを第五せよ！";
    public override float? MaxHealth => 555f;
    public override Vector3? Scale => new(1.1f, 1.1f, 1.1f);
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.GunCOM18,
        ItemType.ArmorHeavy,
        ItemType.SCP500,
        ItemType.Adrenaline,
        ItemType.GrenadeHE,
    ];
    public override IReadOnlyList<Type> CustomItems => [typeof(FifthistPriestKeycard), typeof(LsdPillSafe)];

    protected override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.Hurting += OnHurting;

        base.RegisterEvents();
    }

    protected override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.Hurting -= OnHurting;

        base.UnregisterEvents();
    }

    protected override void OnSpawned()
    {
        Light light = Light.Create(Player.Position, null, null, true, Color.magenta);
        light.Range = 10f;
        light.Intensity = 1.25f;
        Player.Wear(light, new WearOptions
        {
            Slot = "fifthist-priest-light",
            Offset = new Vector3(0f, -0.08f, 0f),
            IgnoreInvisibility = true,
        });

        ApplyAura(Player);
        Scope.RunLoop(1.5f, ApplyAura);
    }

    private void ApplyAura(Player priest)
    {
        if (!Check(priest) || !priest.IsAlive) return;

        foreach (Player target in Player.List)
        {
            if (!target.IsSafePlayer() || target == priest || !target.IsAlive ||
                target.IsInTeam<FifthistTeam>() || CustomRole.Is<Scp3005>(target))
                continue;

            if (Vector3.Distance(priest.Position, target.Position) <= 2.75f)
            {
                target.Hurt(priest, 25f, DamageType.Unknown, null, $"<color={ServerColors.Magenta}>第五的</color>な力による影響");
                priest.ShowHitMarker();
            }
        }
    }

    // 反ミーム保護中の相手には、司祭からのダメージを一切通さない。
    private void OnHurting(HurtingEventArgs ev)
    {
        if (IsMine(ev.Attacker) && ev.Player.HasFlag(SpecificFlagType.AntiMemeEffectDisabled))
            ev.IsAllowed = false;
    }
}
