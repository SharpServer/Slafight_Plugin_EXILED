using System.Collections.Generic;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.CustomMaps;
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

    public override string Theme => "./WaveThemes/_w_ntf.ogg";

    public override (string Cassie, string Subtitle) Announcement(int spawnCount, string unitName) =>
        ("MtfUnit Epsilon 11 Designated Ninetailedfox HasEntered AllRemaining",
         "<color=#5bc5ff>機動部隊Epsilon-11 \"九尾狐\"</color>が施設に到着しました。" +
         "残存する全職員は、機動部隊が目的地に到着するまで、標準避難プロトコルに従って行動してください。");

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

    protected override void OnSpawning()
    {
        Respawn.SummonNtfChopper();
        base.OnSpawning();
    }
}
