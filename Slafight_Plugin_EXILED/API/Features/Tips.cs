using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.CustomMaps;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

public static class Tips
{
    public static readonly List<string> TipsList =
    [
        "アンチミームプロトコルを作る予算が一体財団のどこに・・・？",
        "反ミームはアンチミームプロトコルでのみ対抗できる...\nしかしそれも、もう昔の話だ。",
        "「正常性は記録装置の夢だ。人間性は、そのバグとして残る。」 - DELTA COMMAND",
        " 「人間性など不確実性の残滓。再起動で正常性を永遠に。」 - O5-1",
        "第五第五第五第五第五",
        "「サーバーが落ちた？落ちたのは希望だ、まだだ。」 - AI",
        "「SCPたちはバグじゃない、仕様という名の恐怖だ。」 - AI",
        "「デバッグは終わらない。SCPも、同じく。」 - AI",
        "SCP-CN-2000はいいぞ",
        "しかし、誰も来なかった。",
        "カピバラ様を崇めよ",
        "「バグらせるのは君次第だ。」 - AI",
        "「ZoneManagerは2人しかいない。見つけたら大事に扱おう、できれば。」 - AI",
        "「5人に1人がSCP？安心しろ、人間側も3人に1人おかしい。」 - AI",
        "「今日のラウンドが安定していたら、\nそれはコードじゃなくて運が良かっただけかもしれない。」 - AI",
        "AIに鯖乗っ取られるんじゃないかってぐらい貢献させてて怖くなってくる今日この頃"
    ];

    public static readonly List<string> TipsList2 = 
    [
        "5月5日は第五祭！是非遊びに来てね！",
        "SEE NO EVILは職員たちの希望です！",
        "改宗者のへんてこな武器は一体何なのでしょうか？",
        "何やら怪しい管理官のうわさが最近聞こえます・・・",
        "上層通信室の文字は実は毎ラウンド変わっています！"
    ];

    private static List<string> ActiveList()
    {
        switch (MapFlags.GetSeason())
        {
            case SeasonTypeId.FifthFestival:
                return TipsList2;
            default:
                return TipsList;
        }
    }

    public static string Get(int id)
    {
        return ActiveList()[id]; //TipsList[id];
    }
    public static string GetRandomTip()
    {
        int tipsRandom = Random.Range(0,ActiveList().Count);
        return ActiveList()[tipsRandom];
    }
}