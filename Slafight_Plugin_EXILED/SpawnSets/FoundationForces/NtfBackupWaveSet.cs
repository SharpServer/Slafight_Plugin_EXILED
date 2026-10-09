using System.Collections.Generic;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;

namespace Slafight_Plugin_EXILED.SpawnSets.FoundationForces;

/// <summary>
/// 機動部隊 Epsilon-11 "九尾狐" の予備部隊 (ミニウェーブ) です。
/// </summary>
public sealed class NtfBackupWaveSet : SpawnSet
{
    public override string Name => "Nine-Tailed Fox Backup";

    public override string Description => "機動部隊 Epsilon-11 \"九尾狐\" の予備部隊です。";

    public override Faction RespawnFaction => Faction.FoundationStaff;

    /// <summary>
    /// ミニウェーブ枠で抽選されます。
    /// </summary>
    public override bool IsMiniWave => true;

    public override int RespawnWeight => 80;

    public override float RespawnRatio => 1.0f;

    public override string Theme => "./WaveThemes/_w_ntf.ogg";

    public override (string Cassie, string Subtitle) Announcement(int spawnCount, string unitName) =>
        ("Ninetailedfox Backup unit has entered the facility .",
         "<color=#5bc5ff>九尾狐 予備部隊</color>が施設に到着しました。");

    public override IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles =>
    [
        SpawnSetRoleDefinition.Custom<NtfSergeant>(1, true),
        SpawnSetRoleDefinition.Custom<NtfGenericSpecialist>(1, false, 1.5f),
        SpawnSetRoleDefinition.Custom<NtfPrivate>(99, false, 4f),
    ];

    protected override void OnSpawning()
    {
        Respawn.SummonNtfChopper();
        base.OnSpawning();
    }
}
