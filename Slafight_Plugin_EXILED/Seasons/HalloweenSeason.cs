using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.Seasons;

/// <summary>
/// ハロウィンです。バニラ 13.x ハロウィンの放送 (機動部隊ミュー-13 "ゴーストバスターズ"、
/// SCP を "Specter (幽霊)" と呼ぶ) を再現します。読み上げには今もゲームに残っている
/// <c>HALLOWEEN_GHOSTBUSTERS</c> / <c>HALLOWEEN_SPECTER</c> 音声を使います。
/// </summary>
public sealed class HalloweenSeason : CustomSeason
{
    /// <inheritdoc />
    public override SeasonTypeId Id => SeasonTypeId.Halloween;

    /// <inheritdoc />
    public override string ScpCassie => "HALLOWEEN_SPECTER";

    /// <inheritdoc />
    public override string ScpLabel => "Specter";

    /// <inheritdoc />
    public override string ScpSubjectCassie => "HALLOWEEN_SPECTER";

    /// <inheritdoc />
    public override string ScpSubjectsCassie => "HALLOWEEN_SPECTER";

    /// <inheritdoc />
    public override string RemainingScpSubtitle(int count) => $"{count}体の幽霊は再収容が実施されていません。";

    /// <inheritdoc />
    public override string NoScpLeftSubtitle => "施設内に潜在的な<color=#008F1E>脅威</color>が存在します。警戒してください。";

    /// <inheritdoc />
    public override string NtfCassie => "MtfUnit Mu 13 Designated HALLOWEEN_GHOSTBUSTERS";

    /// <inheritdoc />
    public override string NtfName => "機動部隊ミュー-13 \"ゴーストバスターズ\"";

    /// <inheritdoc />
    public override string NtfBackupCassie => "HALLOWEEN_GHOSTBUSTERS Backup Unit";

    /// <inheritdoc />
    public override string NtfBackupName => "ゴーストバスターズ 予備部隊";

    /// <inheritdoc />
    public override string NtfArrivalBody => "残りの全職員は、機動部隊が貴方の場所まで到着するまで「標準避難プロトコル」の続行を推奨します。";

    /// <inheritdoc />
    public override string ChaosCassiePrefix => "$pitch_.3 .g4 .g6 $pitch_1";

    /// <inheritdoc />
    public override string AccentColor => ServerColors.Pumpkin;

    /// <inheritdoc />
    public override string ChaosArrivalNote => "彼らの目的はお菓子ではありません。";

    /// <inheritdoc />
    public override string ChaosBackupNote => "仮装による識別回避に警戒してください。";

    /// <inheritdoc />
    public override string TeamName(CustomTeam team) =>
        team is FoundationTeam ? "ゴーストバスターズ" : base.TeamName(team);

    /// <inheritdoc />
    public override string TeamCassieName(CustomTeam team) =>
        team is FoundationTeam ? "HALLOWEEN_GHOSTBUSTERS" : base.TeamCassieName(team);
}
