using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Core.Structs;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 季節です。SCP の呼び名と色など、各所が共通で使う最低限の情報だけを持ちます。
/// 文面や呼び名の季節差し替えは、それを持つ側 (SpawnSet・CustomTeam など) が <see cref="Seasonal.Of{T}"/> で書きます。
/// </summary>
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
        Seasons.TryGetValue(CurrentId, out CustomSeason season)
            ? season
            : Seasons[SeasonTypeId.None];

    /// <summary>
    /// 設定で選ばれている季節の ID です。対応するクラスが無い季節でもそのまま返します。
    /// </summary>
    public static SeasonTypeId CurrentId => Plugin.Singleton?.Config?.Season ?? SeasonTypeId.None;

    private static Dictionary<SeasonTypeId, CustomSeason> Seasons => seasons ??= Build();

    // ===== SCP の呼び名 =====

    /// <summary>
    /// SCP の呼び名を置き換えるか。false なら <see cref="LocalizeScpText"/> などは何もしません。
    /// </summary>
    public virtual bool IsSeasonal => true;

    /// <summary>C.A.S.S.I.E で SCP 番号の前に読ませる語です。</summary>
    public virtual string ScpCassie => "SCP";

    /// <summary>字幕・HUD で SCP 番号の前に付ける語です ("SCP-173" の "SCP")。</summary>
    public virtual string ScpLabel => "SCP";

    /// <summary>季節の一言に付ける色です。</summary>
    public virtual string AccentColor => "#ffffff";

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
