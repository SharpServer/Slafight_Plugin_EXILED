using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;
using Slafight_Plugin_EXILED.CustomRoles.Specials;

namespace Slafight_Plugin_EXILED.SpawnSets.Specials;

/// <summary>
/// 機動部隊 Epsilon-11 "九尾狐" の通常波です。
/// </summary>
public sealed class __Test__DaisukeWaveSet : SpawnSet
{
    public override string Name => "DAISUKE";
    public override string Description => "機動部隊 Epsilon-999 \"DAISUKE\" の通常波です。";
    public override Faction RespawnFaction => Faction.FoundationEnemy;
    public override int RespawnWeight => 0;
    public override float RespawnRatio => 1.0f;

    public override SpawnVehicle Vehicle => SpawnVehicle.None;

    public override string Theme => null;

    public override (string Cassie, string Subtitle) Announcement(int spawnCount, string unitName) =>
        CustomSeason.Current.NtfArrival(unitName) ??
        ("MtfUnit Epsilon 999 Designated D A I S U K E HasEntered AllRemaining",
         "<color=#999999>機動部隊Epsilon-999 \"DAISUKE\"</color>が施設に到着しました。" +
         "残存する全職員は、機動部隊が目的地に到着するまで、標準DAISUKE避難プロトコルに従って行動してください。");

    public override IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles =>
    [
        SpawnSetRoleDefinition.Custom<DAISUKE>(999),
    ];
}
