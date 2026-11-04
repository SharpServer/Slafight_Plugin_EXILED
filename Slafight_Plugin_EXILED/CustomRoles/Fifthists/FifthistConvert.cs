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

public sealed class FifthistConvert : FifthistRole
{
    public override string Name => "Fifthist Convert";
    public override string Description => "第五の教えを広め、第五主義者を増やせ。";
    public override float? MaxHealth => 100f;
    public override IReadOnlyList<ItemType> Items => [ItemType.GunCOM18, ItemType.Medkit, ItemType.ArmorLight, ItemType.SCP500];
}
