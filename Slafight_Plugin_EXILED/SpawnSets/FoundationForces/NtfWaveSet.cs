using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Enums;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;

namespace Slafight_Plugin_EXILED.SpawnSets.FoundationForces;

/// <summary>
/// 機動部隊 Epsilon-11 "九尾狐" の通常波です。
/// </summary>
public sealed class NtfWaveSet : SpawnSet
{
    public override string Name => "Nine-Tailed Fox";

    public override string Description => "機動部隊 Epsilon-11 \"九尾狐\" の通常波です。";

    public override Faction RespawnFaction => Faction.FoundationStaff;

    public override int RespawnWeight => 80;

    public override float RespawnRatio => 1.0f;

    public override SpawnVehicle Vehicle => SpawnVehicle.NtfChopper;

    public override string Theme => "./WaveThemes/_w_ntf.ogg";

    protected override void BuildAnnouncement(SpawnAnnouncement a)
    {
        string unitCassie = a.Seasonal("MtfUnit Epsilon 11 Designated Ninetailedfox")
            .On(SeasonTypeId.Halloween, "MtfUnit Mu 13 Designated HALLOWEEN_GHOSTBUSTERS")
            .On(SeasonTypeId.Christmas, "XMAS_EPSILON11");

        string hasEntered = a.Seasonal("HasEntered AllRemaining")
            .On(SeasonTypeId.Christmas, "XMAS_HASENTERED");

        string unitName = a.Seasonal("機動部隊Epsilon-11 \"九尾狐\"")
            .On(SeasonTypeId.Halloween, "機動部隊ミュー-13 \"ゴーストバスターズ\"");

        string body = a.Seasonal("残存する全職員は、機動部隊が目的地に到着するまで、標準避難プロトコルに従って行動してください。")
            .On(SeasonTypeId.Halloween, "残りの全職員は、機動部隊が貴方の場所まで到着するまで「標準避難プロトコル」の続行を推奨します。");

        a.Cassie(unitCassie, a.UnitCassie, hasEntered)
         .Subtitle($"{SpawnAnnouncement.Colored(unitName + a.UnitSuffix, "#5bc5ff")}が施設に到着しました。")
         .Line(body)
         .Note(SeasonTypeId.Christmas, "メリークリスマス。サンタクロースを名乗る不審者を発見した場合は、直ちに機動部隊へ報告してください。");
    }

    public override IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles =>
    [
        SpawnSetRoleDefinition.Custom<NtfGeneral>(1, false, 0.5f),
        SpawnSetRoleDefinition.Custom<NtfCaptain>(1, true),
        SpawnSetRoleDefinition.Custom<NtfSergeant>(2, false, 1.5f),
        SpawnSetRoleDefinition.Custom<NtfLieutenant>(1, false, 1.42f),
        SpawnSetRoleDefinition.Custom<NtfMedicalSpecialist>(1, false, 1.15f),
        SpawnSetRoleDefinition.Custom<NtfContainmentSpecialist>(1, false, 1.15f),
        SpawnSetRoleDefinition.Custom<NtfCombatSpecialist>(1, false, 1.15f),
        SpawnSetRoleDefinition.Custom<NtfPrivate>(99, false, 4f),
    ];
}
