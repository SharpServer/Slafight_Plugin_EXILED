using System;

#nullable disable

namespace Slafight_Plugin_EXILED.API.Enums;

public enum SpecialEventType
{
    /// <summary>No Events!Yeah</summary>
    None,
    
    /// <summary>OMEGA! BOOM!!!</summary>
    OmegaWarhead,
    
    /// <summary>DELTA CHAOS</summary>
    [Obsolete("このイベントは面白みがなかったので、FacilityTerminationイベントに置き換えられました。")]
    OldDeltaWarhead,
    
    /// <summary>CRYYYYYYYYYYY</summary>
    EndlessCry,
    
    /// <summary>BattleField 4</summary>
    Scp1509BattleField,
    
    /// <summary>THE FIFTHIST'S RAID</summary>
    FifthistsRaid,
    
    /// <summary>Nuclear Attack</summary>
    NuclearAttack,
    
    /// <summary>Classic SCRAPPED. IT'S VERY F**</summary>
    ClassicEvent,
    
    /// <summary>It's very tired</summary>
    OperationBlackout,
    
    /// <summary>MEGABALL ATTACK YEAHHHH</summary>
    SnowWarriorsAttack,
    
    /// <summary>DELTA 2.0</summary>
    FacilityTermination,
    
    /// <summary>Revolver is Very Fuckin Good Weapon.</summary>
    RevolverBattles,
    
    /// <summary>セルゲーーーーーイ</summary>
    SergeyMakarovReturns,
    
    /// <summary>255倍速 & 255本コーラ lmao</summary>
    SpeedUpEvent,
    
    /// <summary>何かに使う予定だったような何か</summary>
    DailyFoundation,
    
    /// <summary>Candyyyyyy!!! AprilとHalloweenで仕様が異なる</summary>
    CandyWarriorsAttack,
    
    /// <summary>CASE COLOURLESS GREEN</summary>
    CaseColourlessGreen,
    
    /// <summary>WTF</summary>
    DanteBattle,
}