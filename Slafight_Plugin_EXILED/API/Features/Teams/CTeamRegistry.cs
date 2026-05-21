using System;
using System.Collections.Generic;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Features.Teams;

/// <summary>
/// <see cref="CTeam"/> の静的定義を管理します。
/// </summary>
public static class CTeamRegistry
{
    private static readonly IReadOnlyDictionary<CTeam, CTeamDefinition> Definitions =
        new Dictionary<CTeam, CTeamDefinition>
        {
            [CTeam.Null] = new(CTeam.Null, "不明な勢力", "Unknown Forces", "#ffffff", true),
            [CTeam.FoundationForces] = new(CTeam.FoundationForces, "機動部隊", "MtfUnit", "#00b7eb", false),
            [CTeam.Scientists] = new(CTeam.Scientists, "科学者", "Scientist Personnel", "#faff86", false),
            [CTeam.ClassD] = new(CTeam.ClassD, "Dクラス職員", "Class D Personnel", "#ee7600", true),
            [CTeam.Guards] = new(CTeam.Guards, "警備員", "Facility Guard Personnel", "#00b7eb", false),
            [CTeam.ChaosInsurgency] = new(CTeam.ChaosInsurgency, "カオス・インサージェンシー", "Chaos Insurgency", "#228b22", true),
            [CTeam.Fifthists] = new(CTeam.Fifthists, "第五教会", "$pitch_1.05 5 5 5 $pitch_1 Forces", "#ff00fa", true),
            [CTeam.GoC] = new(CTeam.GoC, "世界オカルト連合", "G O C", "#0000c8", true),
            [CTeam.UIU] = new(CTeam.UIU, "連邦捜査局(FBI)異常事件課", "U I U", "", true),
            [CTeam.SerpentsHand] = new(CTeam.SerpentsHand, "サーペント・ハンド", "Serpents Hand", "", true),
            [CTeam.SCPs] = new(CTeam.SCPs, "SCP", "SCP", "#c50000", true),
            [CTeam.Others] = new(CTeam.Others, "不明な勢力", "Unknown Forces", "#ffffff", true),
            [CTeam.BrokenGodChurch] = new(CTeam.BrokenGodChurch, "壊れた神の教会", "Black God Charge", "", true),
            [CTeam.O5] = new(CTeam.O5, "O5評議会", "O5 Command", "#000000", true),
            [CTeam.Sarkic] = new(CTeam.Sarkic, "サーキック・カルト", "SAW KEY CARD", "", true),
            [CTeam.AWCY] = new(CTeam.AWCY, "Are We Cool Yet?", "Are were code yet", "", true),
            [CTeam.BlackQueen] = new(CTeam.BlackQueen, "黒の女王", "Black Q been", "#000000", true),
        };

    /// <summary>
    /// 指定された <see cref="CTeam"/> の定義を取得します。
    /// </summary>
    /// <param name="team">取得対象のチーム。</param>
    /// <returns>チームに対応する定義。</returns>
    /// <exception cref="ArgumentOutOfRangeException">未定義の <see cref="CTeam"/> が指定された場合。</exception>
    public static CTeamDefinition Get(CTeam team)
    {
        if (Definitions.TryGetValue(team, out var definition))
            return definition;

        throw new ArgumentOutOfRangeException(nameof(team), team, null);
    }
}
