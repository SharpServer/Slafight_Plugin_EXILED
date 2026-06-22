using System.Collections.Generic;
using UnityEngine;
using UserSettings.ServerSpecific;

namespace Slafight_Plugin_EXILED.API.Features;

public abstract class ServerSpecifics
{
    public const int DocumentHintDurationSettingId = 7;
    public const int AccessibilityModeSettingId = 8;
    public const int DebugModeSettingId = 9;
    public const float DefaultDocumentHintDuration = 10f;
    public const float MinDocumentHintDuration = 1f;
    public const float MaxDocumentHintDuration = 60f;

    public static ServerSpecificSettingBase[] Settings()
    {
        var settingsList = new List<ServerSpecificSettingBase>()
        {
            new SSGroupHeader(0,"シャープ鯖"),
            new SSKeybindSetting(1,"近接チャット",KeyCode.V,true,false,hint:"一部の利用可能ロールで、近接チャットを使用するのに必要です。Vを推奨します"),
            new SSPlaintextSetting(2,"キャラクター名","",20,hint:"RPのキャラ名です。設定した名前の後に本当の名前が表示されます。"),
            new SSKeybindSetting(3,"アビリティ使用",KeyCode.LeftAlt,true,false,hint:"HUDの Ability に表示されている選択中アビリティを発動します。左Altを推奨します。"),
            new SSKeybindSetting(4,"アビリティ切り替え",KeyCode.Mouse2,true,false,hint:"HUDに Ability 1/2 のように表示されている場合、次のアビリティへ切り替えます。中マウスボタンを推奨します。"),
            new SSKeybindSetting(5,"アイテムモード切り替え",KeyCode.G,true,false,hint:"Hybridアイテムのモードを切り替えます。Gを推奨します"),
            new SSPlaintextSetting(6, "シークレットパスコード","00000", 5, hint:"特別な場面で必要となるかもしれません・・・"),
            new SSSliderSetting(
                DocumentHintDurationSettingId,
                "資料表示時間",
                MinDocumentHintDuration,
                MaxDocumentHintDuration,
                DefaultDocumentHintDuration,
                false,
                "0.#",
                "{0}秒",
                hint:"Documentを調べた時に内容を表示する秒数です。"),
            new SSTwoButtonsSetting(
                AccessibilityModeSettingId,
                "アクセシビリティモード",
                "ON",
                "OFF",
                false,
                hint:"一部のモデルを見やすい表示へ切り替えます。"),
            new SSTwoButtonsSetting(DebugModeSettingId, "[ADMIN]デバッグモード", "ON", "OFF", true)
        };
        return settingsList.ToArray();
    }
}
