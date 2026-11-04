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

/// <summary>第五教会の役職が共有する装備と配置です。</summary>
public abstract class FifthistRole : CustomRole
{
    public override CustomTeam Team => CustomTeam.Get<FifthistTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.Tutorial;
    public override Vector3? SpawnPosition => PositionProvider.GetExitBPosition();
    public override string CustomInfo => $"<color=#ff00fa>{Name}</color>";
    public override IReadOnlyList<ItemType> Items =>
    [
        ItemType.GunCOM18,
        ItemType.ArmorLight,
        ItemType.Medkit,
        ItemType.Adrenaline,
        ItemType.GrenadeHE,
    ];
    public override IReadOnlyList<Type> CustomItems => [typeof(FifthistKeycard)];
    public override IReadOnlyDictionary<ItemType, ushort> Ammo => new Dictionary<ItemType, ushort>
    {
        [ItemType.Ammo9x19] = 90,
        [ItemType.Ammo556x45] = 30,
    };
}
