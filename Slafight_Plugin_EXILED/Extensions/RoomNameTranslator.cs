using Exiled.API.Enums;

namespace Slafight_Plugin_EXILED.Extensions;

internal static class RoomNameTranslator
{
    public static string TranslateZoneName(this ZoneType zone)
    {
        return zone switch
        {
            ZoneType.Surface => "地上",
            ZoneType.Entrance => "エントランス",
            ZoneType.HeavyContainment => "重度収容区画",
            ZoneType.LightContainment => "軽度収容区画",
            ZoneType.Unspecified => "不明",
            _ => "エラー",
        };
    }

    public static string TranslateZoneNameForShort(this ZoneType zone)
    {
        return zone switch
        {
            ZoneType.Surface => "地上",
            ZoneType.Entrance => "EZ",
            ZoneType.HeavyContainment => "HCZ",
            ZoneType.LightContainment => "LCZ",
            ZoneType.Unspecified => "不明",
            _ => "エラー",
        };
    }

    public static string TranslateRoomName(this RoomType room)
    {
        return room switch
        {
            RoomType.Unknown => "不明",
            RoomType.LczArmory => "武器庫",
            RoomType.HczElevatorA => "エレベーターホールA",
            RoomType.HczElevatorB => "エレベーターホールB",
            RoomType.LczCurve => "曲がり角",
            RoomType.LczStraight => "直線通路",
            RoomType.Lcz914 => "SCP-914収容室",
            RoomType.LczCrossing => "交差点",
            RoomType.LczTCross => "三叉路",
            RoomType.LczCafe => "PCルーム",
            RoomType.LczPlants => "栽培室",
            RoomType.LczToilets => "トイレ",
            RoomType.LczAirlock => "エアロック",
            RoomType.Lcz173 => "SCP-173収容室",
            RoomType.LczClassDSpawn => "Dクラス職員収容室",
            RoomType.LczCheckpointB => "チェックポイントB-L",
            RoomType.LczGlassBox => "SCP-372収容室",
            RoomType.LczCheckpointA => "チェックポイントA-L",
            RoomType.Lcz330 => "SCP-330テストチェンバー",
            RoomType.Hcz079 => "SCP-079収容室",
            RoomType.EzCheckpointHallwayA => "チェックポイントE-A",
            RoomType.HczArmory => "武器庫",
            RoomType.Hcz939 => "SCP-939収容室",
            RoomType.HczTestRoom => "テストルーム",
            RoomType.HczHid => "MicroHID格納庫",
            RoomType.Hcz049 => "SCP-049収容室",
            RoomType.HczEzCheckpointA => "チェックポイントA-H",
            RoomType.HczCrossing => "交差点",
            RoomType.Hcz106 => "SCP-106収容室",
            RoomType.HczNuke => "AlphaWarhead格納庫",
            RoomType.HczTesla => "テスラゲート",
            RoomType.HczEzCheckpointB => "チェックポントB-H",
            RoomType.HczCurve => "曲がり角",
            RoomType.Hcz096 => "SCP-096収容室",
            RoomType.EzVent => "搬出ゲート",
            RoomType.EzIntercom => "放送室",
            RoomType.EzGateA => "ゲートA",
            RoomType.EzDownstairsPcs => "通路横PCルーム",
            RoomType.EzCurve => "曲がり角",
            RoomType.EzPcs => "PCルーム",
            RoomType.EzCrossing => "三叉路",
            RoomType.EzCollapsedTunnel => "崩壊した通路",
            RoomType.EzConference => "VIPルーム",
            RoomType.EzStraight => "直線通路",
            RoomType.EzCafeteria => "ベンチ付き直線通路",
            RoomType.EzUpstairsPcs => "2階付きPCルーム",
            RoomType.EzGateB => "ゲートB",
            RoomType.EzShelter => "非常用シェルター",
            RoomType.Pocket => "[削除済み]",
            RoomType.Surface => "地上",
            RoomType.HczStraight => "直線通路",
            RoomType.EzTCross => "三叉路",
            RoomType.EzChef => "直線通路",
            RoomType.EzStraightColumn => "直線通路",
            RoomType.EzCheckpointHallwayB => "チェックポイントB-H",
            RoomType.HczDss08 => "DSS-08 玄妙除却室",
            RoomType.HczCornerDeep => "曲がり角",
            RoomType.HczIntersectionJunk => "三叉路",
            RoomType.HczIntersection => "三叉路",
            RoomType.HczStraightC => "直線通路 (トイレ)",
            RoomType.HczStraightPipeRoom => "直線通路",
            RoomType.HczStraightVariant => "直線通路",
            RoomType.EzSmallrooms => "直線通路 (EV)",
            RoomType.Hcz127 => "SCP-127収容室",
            RoomType.HczServerRoom => "サーバー室",
            RoomType.HczIncineratorWayside => "焼却炉",
            RoomType.HczLoadingBay => "三叉路",
            _ => "エラー"
        };
    }
}
