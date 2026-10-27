using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.CustomRoles.FoundationForces.Guards.SecurityTeams;
using Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.SpawnSets.FoundationForces;

/// <summary>
/// 財団側の初回の波でだけ抽選に出る保安部隊です。
/// </summary>
public sealed class SecurityTeamWaveSet : SpawnSet
{
    public override string Name => "Security Team";

    public override string Description => "保安部隊です。";

    public override Faction RespawnFaction => Faction.FoundationStaff;

    public override int RespawnWeight => 999;

    public override float RespawnRatio => 1.0f;

    public override SpawnVehicle Vehicle => SpawnVehicle.NtfChopper;

    protected override bool TriggerCondition() => WaveCount(Faction.FoundationStaff) == 0;

    public override string Theme => "./WaveThemes/_w_ntf.ogg";

    protected override void BuildAnnouncement(SpawnAnnouncement a) =>
        a.Cassie("Attention All personnel . Security Team has entered the facility .")
         .Subtitle($"全職員に通達。{SpawnAnnouncement.Colored("保安部隊", CustomTeam.Get<GuardTeam>().Color)}が施設に到着しました。");

    public override IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles =>
    [
        SpawnSetRoleDefinition.Custom<NtfCadet>(1, true),
        SpawnSetRoleDefinition.Custom<SecurityTeamGuard>(99, false),
    ];
}
