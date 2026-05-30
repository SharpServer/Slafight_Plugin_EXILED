using Exiled.API.Enums;

namespace Slafight_Plugin_EXILED.API.Features.ScpHud;

public static class PingTranslate
{
    public static string TranslateZoneName(ZoneType zone)
    {
        switch (zone)
        {
            case ZoneType.Surface:
                return "地上";
            case ZoneType.Entrance:
                return "エントランス";
            case ZoneType.HeavyContainment:
                return "重度収容区画";
            case ZoneType.LightContainment:
                return "軽度収容区画";
            case ZoneType.Unspecified:
                return "不明";
            default:
                return zone.ToString();
        }
    }

    public static string TranslateZoneNameForShort(ZoneType zone)
    {
        switch (zone)
        {
            case ZoneType.Surface:
                return "地上";
            case ZoneType.Entrance:
                return "EZ";
            case ZoneType.HeavyContainment:
                return "HCZ";
            case ZoneType.LightContainment:
                return "LCZ";
            case ZoneType.Unspecified:
                return "不明";
            default:
                return zone.ToString();
        }
    }

    public static string TranslateRoomName(RoomType room)
    {
        switch (room)
        {
            case RoomType.Unknown:
                return "不明";
            case RoomType.LczArmory:
                return "武器庫";
            case RoomType.HczElevatorA:
                return "エレベーターホールA";
            case RoomType.HczElevatorB:
                return "エレベーターホールB";
            case RoomType.LczCurve:
                return "曲がり角";
            case RoomType.LczStraight:
                return "直線通路";
            case RoomType.Lcz914:
                return "SCP-914収容室";
            case RoomType.LczCrossing:
                return "交差点";
            case RoomType.LczTCross:
                return "三叉路";
            case RoomType.LczCafe:
                return "PCルーム";
            case RoomType.LczPlants:
                return "栽培室";
            case RoomType.LczToilets:
                return "トイレ";
            case RoomType.LczAirlock:
                return "エアロック";
            case RoomType.Lcz173:
                return "SCP-173収容室";
            case RoomType.LczClassDSpawn:
                return "Dクラス職員収容室";
            case RoomType.LczCheckpointB:
                return "チェックポイントB-L";
            case RoomType.LczGlassBox:
                return "SCP-372収容室";
            case RoomType.LczCheckpointA:
                return "チェックポイントA-L";
            case RoomType.Lcz330:
                return "SCP-330テストチェンバー";
            case RoomType.Hcz079:
                return "SCP-079収容室";
            case RoomType.EzCheckpointHallwayA:
                return "チェックポイントE-A";
            case RoomType.EzCheckpointHallwayB:
                return "チェックポイントE-B";
            case RoomType.HczArmory:
                return "武器庫";
            case RoomType.Hcz939:
                return "SCP-939収容室";
            case RoomType.HczTestRoom:
                return "テストルーム";
            case RoomType.HczHid:
                return "MicroHID格納庫";
            case RoomType.Hcz049:
                return "SCP-049収容室";
            case RoomType.HczEzCheckpointA:
                return "チェックポイントA-H";
            case RoomType.HczEzCheckpointB:
                return "チェックポイントB-H";
            case RoomType.HczCrossing:
                return "交差点";
            case RoomType.Hcz106:
                return "SCP-106収容室";
            case RoomType.HczNuke:
                return "AlphaWarhead格納庫";
            case RoomType.HczTesla:
                return "テスラゲート";
            case RoomType.HczCurve:
                return "曲がり角";
            case RoomType.Hcz096:
                return "SCP-096収容室";
            case RoomType.EzVent:
                return "搬出ゲート";
            case RoomType.EzIntercom:
                return "放送室";
            case RoomType.EzGateA:
                return "ゲートA";
            case RoomType.EzDownstairsPcs:
                return "通路横PCルーム";
            case RoomType.EzCurve:
                return "曲がり角";
            case RoomType.EzPcs:
                return "PCルーム";
            case RoomType.EzCrossing:
                return "三叉路";
            case RoomType.EzCollapsedTunnel:
                return "崩壊した通路";
            case RoomType.EzConference:
                return "VIPルーム";
            case RoomType.EzStraight:
                return "直線通路";
            case RoomType.EzCafeteria:
                return "ベンチ付き直線通路";
            case RoomType.EzUpstairsPcs:
                return "2階付きPCルーム";
            case RoomType.EzGateB:
                return "ゲートB";
            case RoomType.EzShelter:
                return "非常用シェルター";
            case RoomType.Pocket:
                return "[削除済み]";
            case RoomType.Surface:
                return "地上";
            case RoomType.HczStraight:
                return "直線通路";
            case RoomType.EzTCross:
                return "三叉路";
            default:
                return room.ToString();
        }
    }
}
