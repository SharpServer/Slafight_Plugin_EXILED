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

public sealed class FifthistMindblaster : FifthistRole
{
    public override string Name => "Fifthist Mindblaster";
    public override string Description => "第五の力で敵の思念を破壊し、進路を切り開け。";
    public override float? MaxHealth => 155f;
    public override IReadOnlyList<ItemType> Items => [.. base.Items, ItemType.GrenadeFlash];
}
