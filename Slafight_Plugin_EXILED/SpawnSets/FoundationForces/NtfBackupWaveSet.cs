using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;

namespace Slafight_Plugin_EXILED.SpawnSets.FoundationForces;

/// <summary>
/// 機動部隊 Epsilon-11 "九尾狐" の予備部隊 (ミニウェーブ) です。
/// </summary>
/// <remarks>
/// 通常波との違いは <see cref="IsMiniWave"/> が true であることだけです。
/// これが true の波はバニラのミニウェーブ枠から抽選され、
/// 通常波とは別の重み表 (master の <c>FoundationStaffMiniWaveWeights</c>) で競合します。
///
/// 予備部隊も 1 つの独立した隊として扱われます。本隊とは別の部隊番号が振られるので、
/// 合流するまでは HUD 上でも別の隊として並びます。
/// </remarks>
public sealed class NtfBackupWaveSet : SpawnSet
{
    /// <inheritdoc />
    public override string Name => "Nine-Tailed Fox Backup";

    /// <inheritdoc />
    public override string Description => "機動部隊 Epsilon-11 \"九尾狐\" の予備部隊です。";

    /// <inheritdoc />
    public override Faction RespawnFaction => Faction.FoundationStaff;

    /// <summary>
    /// ミニウェーブ枠で抽選されます。
    /// </summary>
    public override bool IsMiniWave => true;

    /// <summary>
    /// master の <c>FoundationStaffMiniWaveWeights</c> に準拠しています。
    /// </summary>
    public override int RespawnWeight => 80;

    /// <inheritdoc />
    public override float RespawnRatio => 1.0f;

    /// <inheritdoc />
    public override string Theme => "./WaveThemes/_w_ntf.ogg";

    /// <inheritdoc />
    public override (string Cassie, string Subtitle) Announcement(int spawnCount, string unitName) =>
        ("Ninetailedfox Backup unit has entered the facility .",
         "<color=#5bc5ff>九尾狐 予備部隊</color>が施設に到着しました。");

    /// <summary>
    /// master の <c>MTF_NtfBackup</c> UnitPack に準拠した構成です。
    /// </summary>
    /// <remarks>予備部隊なので隊長は出ず、軍曹が率います。</remarks>
    public override IReadOnlyList<SpawnSetRoleDefinition> SpawnRoles =>
    [
        SpawnSetRoleDefinition.Custom<NtfSergeant>(1, true),
        SpawnSetRoleDefinition.Custom<NtfGenericSpecialist>(1, false, 1.5f),
        SpawnSetRoleDefinition.Custom<NtfPrivate>(99, false, 4f),
    ];
}
