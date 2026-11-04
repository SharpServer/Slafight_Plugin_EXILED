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

public sealed class FifthistGuidance : FifthistRole
{
    public override string Name => "Fifthist Guidance";
    public override string Description => "人々を第五世界へ導き、教会の仲間を守れ。";
    public override float? MaxHealth => 125f;
    public override IReadOnlyList<ItemType> Items => [.. base.Items, ItemType.SCP500];
}
