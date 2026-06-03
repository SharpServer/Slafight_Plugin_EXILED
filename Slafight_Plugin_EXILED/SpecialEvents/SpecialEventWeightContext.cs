using System;
using System.Collections.Generic;
using System.ComponentModel;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.SpecialEvents;

/// <summary>
/// 各 SpecialEvent の抽選重みを管理するコンテキスト。
/// 重み 0 = 抽選から除外、値が大きいほど選ばれやすい。
/// </summary>
public class SpecialEventWeightContext
{
    [Description("各イベントの抽選重み。0=除外、大きいほど出やすい（デフォルト10）")]
    public Dictionary<SpecialEventType, int> Weights { get; set; } = new()
    {
        [SpecialEventType.OmegaWarhead]          = 5,
        [SpecialEventType.EndlessCry]            = 7,
        [SpecialEventType.Scp1509BattleField]    = 6,
        [SpecialEventType.FifthistsRaid]         = 10,
        [SpecialEventType.NuclearAttack]         = 10,
        [SpecialEventType.ClassicEvent]          = 0,
        [SpecialEventType.OperationBlackout]     = 0,
        [SpecialEventType.SnowWarriorsAttack]    = 10,
        [SpecialEventType.FacilityTermination]   = 4,
        [SpecialEventType.RevolverBattles]       = 0,
        [SpecialEventType.SergeyMakarovReturns]  = 66,
        [SpecialEventType.SpeedUpEvent]          = 6,
        [SpecialEventType.DailyFoundation]       = 0,
        [SpecialEventType.CandyWarriorsAttack]   = 10,
        [SpecialEventType.CaseColourlessGreen]   = 10,
    };

    /// <summary>
    /// 指定イベントの重みを返す。未登録は 10、負数は 0 として扱う。
    /// </summary>
    public int GetWeight(SpecialEventType type)
    {
        if (!Weights.TryGetValue(type, out var w))
            return 10;
        return Math.Max(0, w);
    }
}
