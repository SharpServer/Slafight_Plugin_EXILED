using System.Collections.Generic;
using PlayerRoles;
using Respawning.NamingRules;
using Random = UnityEngine.Random;

namespace Slafight_Plugin_EXILED.ForceSystem;

/// <summary>
/// 隊の名前を作ります。バニラが配った部隊名と衝突しない組み合わせだけを使います。
/// </summary>
public static class ForceNaming
{
    /// <summary>
    /// バニラと同じ NATO コードです。見た目を揃えるために同じ表を使います。
    /// </summary>
    private static readonly string[] Codes = NineTailedFoxNamingRule.PossibleCodes;

    /// <summary>
    /// バニラの採番が使えないときに使う連番です。
    /// </summary>
    private static int fallbackIndex;

    /// <summary>
    /// 既にこのラウンドで配った名前です。同じ名前を 2 度出さないために持ちます。
    /// </summary>
    private static readonly HashSet<string> Issued = [];

    /// <summary>
    /// バニラが採番した部隊名をそのまま採用します。名前が無ければこちらで作ります。
    /// </summary>
    public static string Adopt(string vanillaName)
    {
        if (string.IsNullOrEmpty(vanillaName))
            return IssueLocalName();

        Issued.Add(vanillaName);

        return vanillaName;
    }

    /// <summary>
    /// バニラの採番を消費せずに、空いている名前を作ります。分隊と非 NTF の隊に使います。
    /// </summary>
    public static string IssueLocalName()
    {
        SyncVanillaNames();

        // バニラと同じ体系からランダムに選ぶ。見た目を揃えるため。
        for (int attempt = 0; attempt < RandomAttempts; attempt++)
        {
            string candidate = Compose(
                Random.Range(0, Codes.Length),
                Random.Range(1, MaxUnitNumber + 1));

            if (Issued.Add(candidate))
                return candidate;
        }

        // 運が悪かっただけかもしれないので、最後は総当たりで空きを探す。
        for (int nato = 0; nato < Codes.Length; nato++)
        {
            for (int number = 1; number <= MaxUnitNumber; number++)
            {
                string candidate = Compose(nato, number);

                if (Issued.Add(candidate))
                    return candidate;
            }
        }

        // 表を使い切った。ここまで来ることはまず無いが、名前を空にはしない。
        fallbackIndex++;

        return $"UNIT-{fallbackIndex:00}";
    }

    /// <summary>
    /// 名前のタグ記号を全角にして、リッチテキストとして解釈されないようにします。
    /// </summary>
    internal static string Sanitize(string name) =>
        string.IsNullOrEmpty(name) ? name : name.Replace("<", "＜").Replace(">", "＞");

    /// <summary>
    /// バニラが既に配った部隊名を取り込みます。
    /// </summary>
    private static void SyncVanillaNames()
    {
        if (!NamingRulesManager.GeneratedNames.TryGetValue(Team.FoundationForces, out List<string> names))
            return;

        foreach (string name in names)
        {
            if (!string.IsNullOrEmpty(name))
                Issued.Add(name);
        }
    }

    /// <summary>
    /// ラウンドをまたいで名前を持ち越さないようにします。
    /// </summary>
    internal static void Reset()
    {
        Issued.Clear();
        fallbackIndex = 0;
    }

    /// <summary>
    /// バニラと同じ <c>ALPHA-01</c> 形式に組みます。
    /// </summary>
    private static string Compose(int nato, int number) => $"{Codes[nato]}-{number:00}";

    /// <summary>
    /// バニラの <c>NineTailedFoxNamingRule</c> が使う番号の上限です。
    /// </summary>
    private const int MaxUnitNumber = NineTailedFoxNamingRule.MaxUnitNumber;

    /// <summary>
    /// ランダムに空きを探す回数です。これを超えたら総当たりに切り替えます。
    /// </summary>
    private const int RandomAttempts = 32;
}
