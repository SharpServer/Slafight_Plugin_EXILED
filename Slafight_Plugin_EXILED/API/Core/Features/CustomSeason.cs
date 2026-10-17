using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Exiled.API.Features;
using PlayerRoles;
using Respawning.NamingRules;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 季節です。放送や HUD で使う呼び名・文面を、季節自身が持ちます。
///
/// 季節ごとの分岐表はありません。<see cref="Id"/> を名乗るサブクラスを置くだけで
/// <see cref="Current"/> から引かれます。既定値はバニラ日本語訳 (Subtitles.txt) の
/// NTF / カオス放送に合わせてあるので、季節側は変えたいところだけ override します。
/// </summary>
/// <example>
/// <code>
/// public sealed class HalloweenSeason : CustomSeason
/// {
///     public override SeasonTypeId Id => SeasonTypeId.Halloween;
///     public override string ScpLabel => "Specter";
///     public override string NtfName  => "機動部隊ミュー-13 \"ゴーストバスターズ\"";
/// }
///
/// // 使う側は今の季節に聞くだけ
/// string name = CustomSeason.Current.LocalizeScpText("SCP-173"); // ハロウィンなら "Specter-173"
/// </code>
/// </example>
public abstract class CustomSeason
{
    private static readonly Regex ScpCassieWord = new Regex(@"\bSCP\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static Dictionary<SeasonTypeId, CustomSeason> seasons;

    /// <summary>
    /// この季節が対応する <see cref="SeasonTypeId"/> です。
    /// </summary>
    public abstract SeasonTypeId Id { get; }

    /// <summary>
    /// 設定 (<see cref="Config.Season"/>) で選ばれている季節です。
    /// 対応するクラスが無ければ <see cref="NormalSeason"/> になります。
    /// </summary>
    public static CustomSeason Current =>
        Seasons.TryGetValue(Plugin.Singleton?.Config?.Season ?? SeasonTypeId.None, out CustomSeason season)
            ? season
            : Seasons[SeasonTypeId.None];

    private static Dictionary<SeasonTypeId, CustomSeason> Seasons => seasons ??= Build();

    // ===== SCP の呼び名 =====

    /// <summary>
    /// 季節仕様の呼び名・出撃放送を使うか。false なら各所は自分の既定文面のままです。
    /// </summary>
    public virtual bool IsSeasonal => true;

    /// <summary>C.A.S.S.I.E で SCP 番号の前に読ませる語です。</summary>
    public virtual string ScpCassie => "SCP";

    /// <summary>字幕・HUD で SCP 番号の前に付ける語です ("SCP-173" の "SCP")。</summary>
    public virtual string ScpLabel => "SCP";

    /// <summary>残存 SCP 数の読み上げ (単数) です。</summary>
    public virtual string ScpSubjectCassie => "ScpSubject";

    /// <summary>残存 SCP 数の読み上げ (複数) です。</summary>
    public virtual string ScpSubjectsCassie => "ScpSubjects";

    /// <summary>残存 SCP 数の字幕です。</summary>
    public virtual string RemainingScpSubtitle(int count) => $"現在、収容違反中のSCPは{count}体です。";

    /// <summary>残存 SCP が 0 体のときの字幕です。</summary>
    public virtual string NoScpLeftSubtitle => "施設内に<color=#008F1E>危険な勢力</color>が存在します。警戒してください。";

    // ===== 機動部隊 (九尾狐枠) =====

    /// <summary>NTF 本隊の部隊名の読み上げです。この後ろに部隊名 (NATO_A 01 など) が続きます。</summary>
    public virtual string NtfCassie => "MtfUnit Epsilon 11 Designated Ninetailedfox";

    /// <summary>NTF 本隊の部隊名の字幕です。</summary>
    public virtual string NtfName => "機動部隊Epsilon-11 \"九尾狐\"";

    /// <summary>NTF 予備部隊の読み上げです。</summary>
    public virtual string NtfBackupCassie => "NineTailedFoxBackupUnit";

    /// <summary>NTF 予備部隊の字幕です。</summary>
    public virtual string NtfBackupName => "九尾狐 予備部隊";

    /// <summary>NTF の部隊名に付ける色です。</summary>
    public virtual string NtfColor => "#5bc5ff";

    /// <summary>NTF 本隊到着時の避難指示です。</summary>
    public virtual string NtfArrivalBody => "残りの全職員は、機動部隊が目的地に到着するまで標準プロトコルに従い避難してください。";

    // ===== カオス・インサージェンシー =====

    /// <summary>カオス放送の読み上げの先頭に付ける演出 (ノイズなど) です。</summary>
    public virtual string ChaosCassiePrefix => string.Empty;

    // ===== 放送の末尾に添える季節の一言 (null なら付けない) =====

    /// <summary>季節の一言に付ける色です。</summary>
    public virtual string AccentColor => "#ffffff";

    public virtual string NtfArrivalNote => null;
    public virtual string NtfBackupNote => null;
    public virtual string ChaosArrivalNote => null;
    public virtual string ChaosBackupNote => null;

    // ===== チーム =====

    /// <summary>終了放送などに出すチーム名です。季節で呼び名を変えたいチームだけ差し替えます。</summary>
    public virtual string TeamName(CustomTeam team) => team.Name;

    /// <summary>終了放送などで読み上げるチーム名です。</summary>
    public virtual string TeamCassieName(CustomTeam team) => team.CassieName;

    // ===== 出撃放送 =====

    /// <summary>
    /// NTF 本隊の出撃放送です。null なら波自身の既定文面が使われます。
    /// </summary>
    /// <param name="unitName">この波に付いた部隊名 (<c>ALPHA-01</c> 形式)。無ければ null。</param>
    public virtual (string Cassie, string Subtitle)? NtfArrival(string unitName)
    {
        if (!IsSeasonal) return null;

        RemainingScpClause(CountScps(), out string scpCassie, out string scpSubtitle);
        string designation = string.IsNullOrEmpty(unitName) ? string.Empty : $"-{unitName}";

        return (
            $"{NtfCassie} {UnitCassie(unitName)} HasEntered AllRemaining {scpCassie}",
            $"<color={NtfColor}>{NtfName}{designation}</color>が施設に到着しました。<split>{NtfArrivalBody}<split>{scpSubtitle}" +
            Note(NtfArrivalNote));
    }

    /// <summary>
    /// NTF 予備部隊の出撃放送です。null なら波自身の既定文面が使われます。
    /// </summary>
    public virtual (string Cassie, string Subtitle)? NtfBackup()
    {
        if (!IsSeasonal) return null;

        RemainingScpClause(CountScps(), out string scpCassie, out string scpSubtitle);

        return (
            $"{NtfBackupCassie} HasEntered {scpCassie}",
            $"<color={NtfColor}>{NtfBackupName}</color>が施設に到着しました。<split>{scpSubtitle}" + Note(NtfBackupNote));
    }

    /// <summary>
    /// カオス本隊の出撃放送です。null なら波自身の既定文面が使われます。
    /// </summary>
    public virtual (string Cassie, string Subtitle)? ChaosArrival(int spawnCount)
    {
        if (!IsSeasonal) return null;

        return (
            $"{ChaosCassiePrefix} AttentionAllPersonnel . Detected {spawnCount} ChaosInsurgency Forces at Gate A . LethalForceAuthorized",
            $"全職員に通達。ゲートAにて、{spawnCount}体の<color=#228b22>カオス・インサージェンシー</color>を検出しました。" +
            "<split>殺傷を伴う武力行使を許可します。" + Note(ChaosArrivalNote));
    }

    /// <summary>
    /// カオス予備部隊の出撃放送です。null なら波自身の既定文面が使われます。
    /// </summary>
    public virtual (string Cassie, string Subtitle)? ChaosBackup(int spawnCount)
    {
        if (!IsSeasonal) return null;

        return (
            $"{ChaosCassiePrefix} Acquired {spawnCount} Additional HostileForces at Gate A . DefenseModuleUpdated",
            $"ゲートAにて、新たに{spawnCount}体の<color=#228b22>敵対勢力</color>を確認しました。<split>防衛モデルを更新します。" +
            Note(ChaosBackupNote));
    }

    // ===== 呼び名の置き換え =====

    /// <summary>C.A.S.S.I.E 用の SCP 呼称です。例: "173" → "SCP 1 7 3" / "HALLOWEEN_SPECTER 1 7 3"。</summary>
    public string ScpCassieName(string number) => $"{ScpCassie} {string.Join(" ", number.Where(char.IsDigit))}";

    /// <summary>字幕・HUD 用の SCP 呼称です。例: "173" → "SCP-173" / "Specter-173"。</summary>
    public string ScpName(string number) => $"{ScpLabel}-{number}";

    /// <summary>C.A.S.S.I.E 文字列中の "SCP" をこの季節の呼称に置き換えます。</summary>
    public string LocalizeScpCassie(string cassie)
    {
        if (!IsSeasonal || string.IsNullOrEmpty(cassie)) return cassie;

        return ScpCassieWord.Replace(cassie, ScpCassie);
    }

    /// <summary>字幕・HUD 文字列中の "SCP-" をこの季節の呼称に置き換えます ("SCP-173" → "Specter-173")。</summary>
    public string LocalizeScpText(string text)
    {
        if (!IsSeasonal || string.IsNullOrEmpty(text)) return text;

        return text.Replace("SCP-", $"{ScpLabel}-");
    }

    /// <summary>残存 SCP 数です。SCP-049-2 は数えません。</summary>
    public static int CountScps() =>
        CustomTeam.Get<ScpTeam>().Members.Count(player => player.Role.Type != RoleTypeId.Scp0492);

    /// <summary>バニラ NTF 放送と同じ形の、残存 SCP 数の読み上げと字幕です。</summary>
    protected void RemainingScpClause(int count, out string cassie, out string subtitle)
    {
        switch (count)
        {
            case 0:
                cassie = "NoScpsLeft";
                subtitle = NoScpLeftSubtitle;
                break;
            case 1:
                cassie = $"AwaitingRecontainment 1 {ScpSubjectCassie}";
                subtitle = RemainingScpSubtitle(1);
                break;
            default:
                cassie = $"AwaitingRecontainment {count} {ScpSubjectsCassie}";
                subtitle = RemainingScpSubtitle(count);
                break;
        }
    }

    /// <summary>部隊名 (<c>ALPHA-01</c>) を C.A.S.S.I.E 用の綴り (<c>NATO_A 01</c>) に直します。</summary>
    protected static string UnitCassie(string unitName)
    {
        if (string.IsNullOrEmpty(unitName)) return string.Empty;
        if (!NamingRulesManager.TryGetNamingRule(Team.FoundationForces, out UnitNamingRule rule)) return string.Empty;

        return rule.TranslateToCassie(unitName);
    }

    private string Note(string note) =>
        string.IsNullOrEmpty(note) ? string.Empty : $"<split><color={AccentColor}>{note}</color>";

    private static Dictionary<SeasonTypeId, CustomSeason> Build()
    {
        var result = new Dictionary<SeasonTypeId, CustomSeason>();

        foreach (Type type in typeof(CustomSeason).Assembly.GetTypes()
                     .Where(type => typeof(CustomSeason).IsAssignableFrom(type) && !type.IsAbstract))
        {
            try
            {
                var season = (CustomSeason)Activator.CreateInstance(type);

                if (result.ContainsKey(season.Id))
                {
                    Log.Warn($"[Slafight] 季節 {season.Id} が重複しています。{type.FullName} は無視します。");
                    continue;
                }

                result[season.Id] = season;
            }
            catch (Exception exception)
            {
                Log.Error($"[Slafight] 季節 {type.FullName} を生成できませんでした: {exception}");
            }
        }

        if (!result.ContainsKey(SeasonTypeId.None))
            result[SeasonTypeId.None] = new NormalSeason();

        return result;
    }
}

/// <summary>
/// 季節指定なしです。呼び名も放送も各所の既定のままにします。
/// </summary>
public sealed class NormalSeason : CustomSeason
{
    /// <inheritdoc />
    public override SeasonTypeId Id => SeasonTypeId.None;

    /// <inheritdoc />
    public override bool IsSeasonal => false;
}
