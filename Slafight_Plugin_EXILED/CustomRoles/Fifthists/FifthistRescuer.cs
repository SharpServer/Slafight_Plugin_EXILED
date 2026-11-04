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

public sealed class FifthistRescuer : FifthistRole
{
    public override string Name => "Fifthist Rescuer";
    public override string Description => "第五的な存在を見つけ出し、救出して施設を占領せよ。";
    public override float? MaxHealth => 135f;
    public override IReadOnlyList<ItemType> Items => [.. base.Items, ItemType.SCP500, ItemType.ArmorCombat];
}
