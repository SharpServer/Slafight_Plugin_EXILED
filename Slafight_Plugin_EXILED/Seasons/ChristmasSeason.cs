using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.Seasons;

/// <summary>
/// クリスマスです。NTF 本隊はバニラのクリスマス専用音声 (<c>XMAS_*</c>) で読み上げ、
/// それ以外の放送は通常の読み上げに季節の一言を添えます。
/// </summary>
public sealed class ChristmasSeason : CustomSeason
{
    /// <inheritdoc />
    public override SeasonTypeId Id => SeasonTypeId.Christmas;

    /// <inheritdoc />
    public override string AccentColor => ServerColors.Red;

    /// <inheritdoc />
    public override string NtfArrivalNote => "メリークリスマス。サンタクロースを名乗る不審者を発見した場合は、直ちに機動部隊へ報告してください。";

    /// <inheritdoc />
    public override string NtfBackupNote => "予備部隊はプレゼントの配達要員ではありません。";

    /// <inheritdoc />
    public override string ChaosArrivalNote => "彼らはプレゼントを届けに来たのではありません。";

    /// <inheritdoc />
    public override string ChaosBackupNote => "煙突からの侵入にも警戒してください。";

    /// <inheritdoc />
    /// <remarks>
    /// 読み上げはバニラ <c>NtfWaveAnnouncement</c> のクリスマス分岐と同じ形です。
    /// 字幕は通常の季節放送のものをそのまま使います。
    /// </remarks>
    public override (string Cassie, string Subtitle)? NtfArrival(string unitName)
    {
        if (base.NtfArrival(unitName) is not { } announcement) return null;

        return ($"XMAS_EPSILON11 {UnitCassie(unitName)} XMAS_HASENTERED {CountScps()} XMAS_SCPSUBJECTS", announcement.Subtitle);
    }
}
