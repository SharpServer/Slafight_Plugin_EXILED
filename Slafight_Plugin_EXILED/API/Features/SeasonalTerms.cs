using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomMaps.Core;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// 季節ごとに差し替わる Cassie 放送・HUD 表示の用語と文面。
/// 既定値はバニラ日本語訳 (Subtitles.txt) の NTF / カオス放送に合わせてある。
/// 季節を追加するときは <see cref="SeasonalTerms"/> の登録表に 1 件足すだけでよい。
/// </summary>
public sealed class SeasonalTermSet
{
    // ===== SCP の呼称 =====

    /// <summary>Cassie で SCP 番号の前に読ませる語。例: "SCP" / "HALLOWEEN_SPECTER"。</summary>
    public string ScpCassie { get; set; } = "SCP";

    /// <summary>字幕で SCP 番号の前に付ける語。例: "SCP" ("SCP-173") / "Specter" ("Specter-173")。</summary>
    public string ScpLabel { get; set; } = "SCP";

    /// <summary>残存 SCP 数の読み上げ (単数)。</summary>
    public string ScpSubjectCassie { get; set; } = "ScpSubject";

    /// <summary>残存 SCP 数の読み上げ (複数)。</summary>
    public string ScpSubjectsCassie { get; set; } = "ScpSubjects";

    /// <summary>残存 SCP 数の字幕。{0} に数が入る。</summary>
    public string RemainingScpTranslated { get; set; } = "現在、収容違反中のSCPは{0}体です。";

    /// <summary>残存 SCP が 0 体のときの字幕。</summary>
    public string NoScpLeftTranslated { get; set; } = "施設内に<color=#008F1E>危険な勢力</color>が存在します。警戒してください。";

    // ===== 機動部隊 (九尾狐枠) =====

    public string NtfCassie { get; set; } = "MtfUnit Epsilon 11 Designated Ninetailedfox";
    public string NtfName { get; set; } = "機動部隊Epsilon-11 \"九尾狐\"";
    public string NtfBackupCassie { get; set; } = "NineTailedFoxBackupUnit";
    public string NtfBackupName { get; set; } = "九尾狐 予備部隊";
    public string NtfColor { get; set; } = "#5bc5ff";
    public string NtfArrivalBody { get; set; } = "残りの全職員は、機動部隊が目的地に到着するまで標準プロトコルに従い避難してください。";

    /// <summary>NTF 本隊の Cassie 全文を差し替える場合に指定する (引数は残存 SCP 数)。クリスマスの XMAS_* 音声など。</summary>
    public Func<int, string>? NtfArrivalCassieOverride { get; set; }

    // ===== カオス・インサージェンシー =====

    /// <summary>カオス放送の Cassie 先頭に付ける演出 (ノイズ等)。</summary>
    public string ChaosCassiePrefix { get; set; } = string.Empty;

    // ===== 各放送の末尾に付ける季節の一言 (null なら付けない) =====

    public string AccentColor { get; set; } = "#ffffff";
    public string? NtfArrivalNote { get; set; }
    public string? NtfBackupNote { get; set; }
    public string? ChaosArrivalNote { get; set; }
    public string? ChaosBackupNote { get; set; }

    /// <summary>終了放送 ("contained successfully by ～") でのチーム名の差し替え。</summary>
    public Dictionary<CTeam, (string Cassie, string Name)> TeamOverrides { get; } = new();
}

/// <summary>
/// 現在のシーズン (<see cref="MapFlags.GetSeason"/>) に応じた Cassie 放送・HUD 表示用語を提供する。
/// </summary>
public static class SeasonalTerms
{
    private static readonly Regex ScpCassieWord = new(@"\bSCP\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<SeasonTypeId, SeasonalTermSet> Seasons = new()
    {
        // バニラ 13.x ハロウィン: 機動部隊ミュー-13 "ゴーストバスターズ" / SCP は "Specter (幽霊)"
        [SeasonTypeId.Halloween] = CreateHalloween(),
        // バニラのクリスマス NTF 音声 (XMAS_*)
        [SeasonTypeId.Christmas] = CreateChristmas(),
    };

    /// <summary>季節仕様が無いときに使う既定の用語。</summary>
    public static SeasonalTermSet Default { get; } = new();

    /// <summary>現在のシーズンの用語。季節仕様が無ければ <see cref="Default"/>。</summary>
    public static SeasonalTermSet Current =>
        Seasons.TryGetValue(MapFlags.GetSeason(), out var terms) ? terms : Default;

    /// <summary>現在のシーズンに専用の放送仕様があるか。</summary>
    public static bool TryGetCurrent(out SeasonalTermSet terms) =>
        Seasons.TryGetValue(MapFlags.GetSeason(), out terms);

    /// <summary>Cassie 用の SCP 呼称。例: "173" → "SCP 1 7 3" / "HALLOWEEN_SPECTER 1 7 3"。</summary>
    public static string ScpCassie(string number) =>
        $"{Current.ScpCassie} {string.Join(" ", number.Where(char.IsDigit))}";

    /// <summary>字幕用の SCP 呼称。例: "173" → "SCP-173" / "Specter-173"。</summary>
    public static string ScpName(string number) => $"{Current.ScpLabel}-{number}";

    /// <summary>
    /// 既存の Cassie / 字幕文字列に含まれる "SCP" 呼称を現在のシーズンのものに置き換える。
    /// 季節仕様が無い場合は何もしない。
    /// </summary>
    public static void LocalizeScp(ref string cassie, ref string translated)
    {
        cassie = LocalizeScpCassie(cassie);
        translated = LocalizeScpText(translated);
    }

    /// <summary>Cassie 文字列中の "SCP" 単語を現在のシーズンの呼称に置き換える。</summary>
    public static string LocalizeScpCassie(string cassie)
    {
        var terms = Current;
        if (ReferenceEquals(terms, Default) || string.IsNullOrEmpty(cassie))
            return cassie;
        return ScpCassieWord.Replace(cassie, terms.ScpCassie);
    }

    /// <summary>字幕・HUD 文字列中の "SCP-" を現在のシーズンの呼称に置き換える (例: "SCP-173" → "Specter-173")。</summary>
    public static string LocalizeScpText(string text)
    {
        var terms = Current;
        if (ReferenceEquals(terms, Default) || string.IsNullOrEmpty(text))
            return text;
        return text.Replace("SCP-", $"{terms.ScpLabel}-");
    }

    /// <summary>残存 SCP 数 (SCP-049-2 を除く)。</summary>
    public static int CountScps() => Player.List.Count(p =>
        p.IsSafePlayer() && p.IsAlive && p.Role.Type != RoleTypeId.Scp0492 && p.GetTeam() == CTeam.SCPs);

    /// <summary>バニラ NTF 放送と同じ形式の残存 SCP 数クローズ。</summary>
    public static void GetRemainingScpClause(SeasonalTermSet terms, int count, out string cassie, out string translated)
    {
        switch (count)
        {
            case 0:
                cassie = "NoScpsLeft";
                translated = terms.NoScpLeftTranslated;
                break;
            case 1:
                cassie = $"AwaitingRecontainment 1 {terms.ScpSubjectCassie}";
                translated = string.Format(terms.RemainingScpTranslated, 1);
                break;
            default:
                cassie = $"AwaitingRecontainment {count} {terms.ScpSubjectsCassie}";
                translated = string.Format(terms.RemainingScpTranslated, count);
                break;
        }
    }

    private static SeasonalTermSet CreateHalloween()
    {
        var terms = new SeasonalTermSet
        {
            ScpCassie = "HALLOWEEN_SPECTER",
            ScpLabel = "Specter",
            ScpSubjectCassie = "HALLOWEEN_SPECTER",
            ScpSubjectsCassie = "HALLOWEEN_SPECTER",
            RemainingScpTranslated = "{0}体の幽霊は再収容が実施されていません。",
            NoScpLeftTranslated = "施設内に潜在的な<color=#008F1E>脅威</color>が存在します。警戒してください。",

            NtfCassie = "MtfUnit Mu 13 Designated HALLOWEEN_GHOSTBUSTERS",
            NtfName = "機動部隊ミュー-13 \"ゴーストバスターズ\"",
            NtfBackupCassie = "HALLOWEEN_GHOSTBUSTERS Backup Unit",
            NtfBackupName = "ゴーストバスターズ 予備部隊",
            NtfArrivalBody = "残りの全職員は、機動部隊が貴方の場所まで到着するまで「標準避難プロトコル」の続行を推奨します。",

            ChaosCassiePrefix = "$pitch_.3 .g4 .g6 $pitch_1",

            AccentColor = ServerColors.Pumpkin,
            ChaosArrivalNote = "彼らの目的はお菓子ではありません。",
            ChaosBackupNote = "仮装による識別回避に警戒してください。",
        };
        terms.TeamOverrides[CTeam.FoundationForces] = ("HALLOWEEN_GHOSTBUSTERS", "ゴーストバスターズ");
        return terms;
    }

    private static SeasonalTermSet CreateChristmas() => new()
    {
        // バニラ同様、XMAS 音声は部隊名と SCP 数のみを読み上げる (本来は部隊名の位置に designation が入る)
        NtfArrivalCassieOverride = count => $"XMAS_EPSILON11 Ninetailedfox XMAS_HASENTERED {count} XMAS_SCPSUBJECTS",

        AccentColor = ServerColors.Red,
        NtfArrivalNote = "メリークリスマス。サンタクロースを名乗る不審者を発見した場合は、直ちに機動部隊へ報告してください。",
        NtfBackupNote = "予備部隊はプレゼントの配達要員ではありません。",
        ChaosArrivalNote = "彼らはプレゼントを届けに来たのではありません。",
        ChaosBackupNote = "煙突からの侵入にも警戒してください。",
    };
}
