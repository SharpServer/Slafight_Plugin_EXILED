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

public sealed class FifthistPriest : FifthistRole
{
    public override string Name => "Fifthist Priest";
    public override string Description => "第五的な存在の恩寵を受けた司祭。施設を占領せよ。";
    public override float? MaxHealth => 555f;
    public override Vector3? Scale => new(1.1f, 1.1f, 1.1f);
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.GunCOM18,
        ItemType.ArmorHeavy,
        ItemType.SCP207,
        ItemType.SCP500,
        ItemType.Adrenaline,
        ItemType.GrenadeHE,
    ];
    public override IReadOnlyList<Type> CustomItems => [typeof(FifthistPriestKeycard)];
    public override string HudLabel => "<color=#ff00fa>Fifthist Priest</color>";
    public override string Objective => "第五の教えを広め、第五教会を勝利へ導け。";

    protected override void OnSpawned()
    {
        Scope.RunLoop(1.5f, ApplyAura);
    }

    private void ApplyAura(Player priest)
    {
        if (!Check(priest) || !priest.IsAlive) return;

        foreach (Player target in Player.List)
        {
            if (!target.IsSafePlayer() || target == priest || !target.IsAlive ||
                target.IsInTeam<FifthistTeam>() || target.IsInTeam<ScpTeam>())
                continue;

            if (Vector3.Distance(priest.Position, target.Position) <= 2.75f)
            {
                target.Hurt(priest, 25f, Exiled.API.Enums.DamageType.Unknown, null, "<color=#ff00fa>第五的</color>な力による影響");
                priest.ShowHitMarker();
            }
        }
    }
}
