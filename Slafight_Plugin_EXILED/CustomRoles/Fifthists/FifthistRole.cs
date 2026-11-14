using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomTeams;
using UnityEngine;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthists;

/// <summary>Gate B から出撃する第五教会の人間役職が共有する設定です。</summary>
public abstract class FifthistRole : CustomRole
{
    public override CustomTeam Team => CustomTeam.Get<FifthistTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.Tutorial;
    public override Vector3? SpawnPosition => PositionProvider.GetExitBPosition(0f);

    /// <summary>ネームプレートに出す英名です。</summary>
    protected abstract string InfoName { get; }

    public override string CustomInfo => $"<color={ServerColors.Magenta}>{InfoName}</color>";
}
