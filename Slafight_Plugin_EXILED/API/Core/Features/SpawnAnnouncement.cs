using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using PlayerRoles;
using Respawning.NamingRules;
using Slafight_Plugin_EXILED.API.Core.Structs;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomTeams;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 出撃放送の組み立て役です。読み上げと字幕を並べて積み、放送に使う数値と季節も渡します。
/// </summary>
public sealed class SpawnAnnouncement
{
    private readonly List<string> cassie = [];

    private string subtitle = string.Empty;

    private int? remainingScpCount;

    internal SpawnAnnouncement(SpawnSet wave, int spawnCount, IReadOnlyList<Player> players, string unitName)
    {
        Wave = wave;
        SpawnCount = spawnCount;
        Players = players ?? [];
        UnitName = unitName;
        UnitCassie = TranslateUnit(unitName);
        Season = CustomSeason.CurrentId;
    }

    // ===== 放送に使える値 =====

    /// <summary>放送している波です。</summary>
    public SpawnSet Wave { get; }

    /// <summary>実際に出た人数です。</summary>
    public int SpawnCount { get; }

    /// <summary>出たプレイヤーです (割り当て順)。プレビューでは空。</summary>
    public IReadOnlyList<Player> Players { get; }

    /// <summary>部隊名 (<c>ALPHA-01</c> 形式) です。無ければ null。</summary>
    public string UnitName { get; }

    /// <summary>部隊名の読み上げ (<c>NATO_A 01</c>) です。無ければ空文字。</summary>
    public string UnitCassie { get; }

    /// <summary>部隊名を字幕の名前の後ろに付ける形 (<c>-ALPHA-01</c>) です。無ければ空文字。</summary>
    public string UnitSuffix => string.IsNullOrEmpty(UnitName) ? string.Empty : $"-{UnitName}";

    /// <summary>現在の季節です。</summary>
    public SeasonTypeId Season { get; }

    /// <summary>残存 SCP 数です (SCP-049-2 を除く)。</summary>
    public int RemainingScpCount => remainingScpCount ??=
        CustomTeam.Get<ScpTeam>().Members.Count(player => player.Role.Type != RoleTypeId.Scp0492);

    /// <summary>いまの季節が <paramref name="season"/> か。</summary>
    public bool Is(SeasonTypeId season) => Season == season;

    /// <summary>
    /// 季節で差し替わる値を作ります。<c>a.Seasonal("通常").On(SeasonTypeId.Halloween, "ハロウィン")</c>
    /// </summary>
    /// <param name="normal">どの <c>On</c> にも当たらなかったときの値。</param>
    public Seasonal<T> Seasonal<T>(T normal = default) => Structs.Seasonal.Of(normal);

    // ===== 組み立て結果 =====

    /// <summary>ここまでの読み上げです。</summary>
    public string CassieText => string.Join(" ", cassie);

    /// <summary>ここまでの字幕です。</summary>
    public string SubtitleText => subtitle;

    /// <summary>何も積まれていないか。空なら放送しません。</summary>
    public bool IsEmpty => cassie.Count == 0;

    /// <summary>放送の前にチャイムを鳴らすか。</summary>
    public bool Bell { get; set; }

    // ===== 組み立て =====

    /// <summary>読み上げを後ろに足します。空の語は飛ばすので、無いかもしれない部隊名もそのまま渡せます。</summary>
    public SpawnAnnouncement Cassie(params string[] words)
    {
        cassie.AddRange(words.Where(word => !string.IsNullOrWhiteSpace(word)).Select(word => word.Trim()));

        return this;
    }

    /// <summary>読み上げを先頭に足します。</summary>
    public SpawnAnnouncement PrependCassie(params string[] words)
    {
        cassie.InsertRange(0, words.Where(word => !string.IsNullOrWhiteSpace(word)).Select(word => word.Trim()));

        return this;
    }

    /// <summary>字幕を今の行の後ろに続けます。</summary>
    public SpawnAnnouncement Subtitle(string text)
    {
        subtitle += text;

        return this;
    }

    /// <summary>字幕を改行 (<c>&lt;split&gt;</c>) してから足します。空なら何もしません。</summary>
    public SpawnAnnouncement Line(string text)
    {
        if (string.IsNullOrEmpty(text)) return this;

        subtitle = subtitle.Length == 0 ? text : $"{subtitle}<split>{text}";

        return this;
    }

    /// <summary>読み上げと字幕 (改行して) を一度に足します。</summary>
    public SpawnAnnouncement Say(string cassieText, string subtitleText) => Cassie(cassieText).Line(subtitleText);

    /// <summary>色付きの一言を改行して添えます。色を省くと季節の色 (<see cref="CustomSeason.AccentColor"/>)。空なら何もしません。</summary>
    public SpawnAnnouncement Note(string text, string color = null) =>
        string.IsNullOrEmpty(text) ? this : Line(Colored(text, color ?? CustomSeason.Current.AccentColor));

    /// <summary><paramref name="season"/> のときだけ一言を添えます。</summary>
    public SpawnAnnouncement Note(SeasonTypeId season, string text, string color = null) =>
        Is(season) ? Note(text, color) : this;

    /// <summary>積んだものを全部捨てます。</summary>
    public SpawnAnnouncement Clear()
    {
        cassie.Clear();
        subtitle = string.Empty;

        return this;
    }

    /// <summary><c>&lt;color&gt;</c> で囲みます。</summary>
    public static string Colored(string text, string color) => $"<color={color}>{text}</color>";

    private static string TranslateUnit(string unitName)
    {
        if (string.IsNullOrEmpty(unitName)) return string.Empty;
        if (!NamingRulesManager.TryGetNamingRule(Team.FoundationForces, out UnitNamingRule rule)) return string.Empty;

        return rule.TranslateToCassie(unitName);
    }
}
