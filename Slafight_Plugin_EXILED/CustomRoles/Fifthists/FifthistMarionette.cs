using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.CustomTeams;
using UnityEngine;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.CustomRoles.Fifthists;

/// <summary>ピンクの光に作り替えられた元人間。第五教会の一員として振る舞います。</summary>
public sealed class FifthistMarionette : CustomRole
{
    public override string Name => "Fifthist Marionette";
    public override string Description => "ピンクの光によって作り替えられてしまった人間の成れの果て。\n第五教会に従い、生存者どもを騙しながら第五しろ！";
    public override string HudLabel => $"<color={ServerColors.Magenta}>Fifthist: Marionette</color>";
    public override string Objective => "第五教会に従い、生存者どもを騙しながら第五しろ！";
    public override CustomTeam Team => CustomTeam.Get<FifthistTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.Scp0492;
    public override float? MaxHealth => 100f;
    public override string CustomInfo => $"<color={ServerColors.Magenta}>Fifthist Marionette</color>";
    public override RoleVoiceSettings Voice => RoleVoiceSettings.WithProximity();

    /// <summary>SCP-3125 が居ればその足元、部屋の外なら重度収容区画のどこか、それ以外はその場に立たせます。</summary>
    public override Vector3? SpawnPosition
    {
        get
        {
            if (CustomRole.PlayersOf<Scp3125>().FirstOrDefault() is { } scp3125)
                return scp3125.Position + Vector3.up * 0.15f;

            if (Player?.CurrentRoom is null)
                return Room.Random(ZoneType.HeavyContainment).WorldPosition(Vector3.up * 1.05f);

            return Player.Position + Vector3.up * 0.85f;
        }
    }
}
