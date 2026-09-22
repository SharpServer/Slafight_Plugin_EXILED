using System.Collections.Generic;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.CustomRoles.FoundationForces.MTFs.Epsilon11;

namespace Slafight_Plugin_EXILED.SpawnSets.FoundationForces;

/// <summary>
/// 機動部隊 Epsilon-11 "九尾狐" の通常波です。
/// </summary>
/// <remarks>
/// <b>これが標準的な「スポーン部隊」の書き方の見本です。</b>
/// 波を足したいときは、このファイルを複製して中身を書き換えるだけで済みます。
/// <see cref="SpawnSystem"/> にも <see cref="SpawnContext"/> にも登録は要りません。
/// <see cref="SpawnSet"/> を継承して <see cref="RespawnWeight"/> を 0 より大きくすれば、
/// それだけで抽選対象の波になります。
///
/// 部隊システムとの連携も自動です。<see cref="RespawnFaction"/> が
/// <see cref="Faction.FoundationStaff"/> なので、この波が出ると
/// <c>ForceManager</c> が機動部隊の隊 (<c>MobileTaskForce</c>) を 1 つ作り、
/// 出た全員をその隊に入れて役職優先度の高い者を TopLead に据えます。
/// 部隊番号はバニラの採番をそのまま使うので、名札の <c>(ALPHA-01)</c> と一致します。
/// 独自の派生システムに乗せたい場合だけ <see cref="SpawnSet.CreateForce"/> を override してください。
///
/// 構成・重み・比率・アナウンス・テーマはすべて master の
/// <c>DefaultUnitPacks</c> / <c>SpawnConfig</c> / <c>SpawningHandler</c> に準拠しています。
/// </remarks>
public sealed class NtfWaveSet : SpawnSet
{
    /// <inheritdoc />
    public override string Name => "Nine-Tailed Fox";

    /// <inheritdoc />
    public override string Description => "機動部隊 Epsilon-11 \"九尾狐\" の通常波です。";

    /// <inheritdoc />
    public override Faction RespawnFaction => Faction.FoundationStaff;

    /// <summary>
    /// master の <c>FoundationStaffWaveWeights</c> に準拠しています。
    /// </summary>
    /// <remarks>
    /// 旧実装は NTF 80 / Hammer Down 20 の 2 択でした。
    /// Hammer Down はカスタム役職 (<c>HdInfantry</c> など) が要るのでまだ足していません。
    /// 重み 20 の波を足せば、そのまま 8:2 の比率で混ざります。
    /// </remarks>
    public override int RespawnWeight => 80;

    /// <summary>
    /// master の <c>SpawnRatios</c> に準拠して待機者を全員出します。
    /// </summary>
    public override float RespawnRatio => 1.0f;

    /// <inheritdoc />
    public override string Theme => "./WaveThemes/_w_ntf.ogg";

    /// <inheritdoc />
    public override (string Cassie, string Subtitle) Announcement(int spawnCount, string unitName) =>
        ("MtfUnit Epsilon 11 Designated Ninetailedfox HasEntered AllRemaining",
         "<color=#5bc5ff>機動部隊Epsilon-11 \"九尾狐\"</color>が施設に到着しました。" +
         "残存する全職員は、機動部隊が目的地に到着するまで、標準避難プロトコルに従って行動してください。");

    /// <summary>
    /// master の <c>MTF_NtfNormal</c> UnitPack に準拠した構成です。
    /// </summary>
    /// <remarks>
    /// いまはバニラ役職だけを有効にしてあります。
    /// コメントアウトしてある行が、旧実装が持っていたカスタム役職の枠です。
    /// 役職を実装したら <c>SpawnSetRoleDefinition.Custom&lt;T&gt;()</c> で差し替えてください。
    ///
    /// <c>count</c> が人数上限、<c>weight</c> が出やすさ、
    /// <c>isForced</c> が「他の行より先に必ず埋める」です。
    /// 隊長を 1 人必ず出し、残りを二等兵で埋めるのがこの構成の狙いです。
    /// </remarks>
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
