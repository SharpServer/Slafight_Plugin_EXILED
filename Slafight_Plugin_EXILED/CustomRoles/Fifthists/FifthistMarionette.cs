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

/// <summary>SCP-3005 に死体を作り替えられた人間。第五教会の一員として振る舞います。</summary>
public sealed class FifthistMarionette : CustomRole
{
    public override string Name => "Fifthist Marionette";
    public override string Description => "ピンクの光に作り替えられた元人間。第五教会に従い、生存者を欺け。";
    public override CustomTeam Team => CustomTeam.Get<FifthistTeam>();
    public override RoleTypeId BaseRole => RoleTypeId.Scp0492;
    public override float? MaxHealth => 100f;
    public override string CustomInfo => "<color=#ff00fa>Fifthist Marionette</color>";
    public override RoleVoiceSettings Voice => RoleVoiceSettings.WithProximity();
}
