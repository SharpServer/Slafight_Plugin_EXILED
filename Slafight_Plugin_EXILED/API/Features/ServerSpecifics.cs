using System.Collections.Generic;
using UnityEngine;
using UserSettings.ServerSpecific;

namespace Slafight_Plugin_EXILED.API.Features;

public abstract class ServerSpecifics
{
    public const int HeaderSettingId = 0;
    public const int ProximityChatKeybindSettingId = 1;
    public const int RpNameSettingId = 2;
    public const int AbilityUseKeybindSettingId = 3;
    public const int AbilitySwitchKeybindSettingId = 4;
    public const int ItemModeSwitchKeybindSettingId = 5;
    public const int SuicideButtonKeybindSettingId = 6;
    public const int SecretPasscodeSettingId = 7;
    public const int DocumentHintDurationSettingId = 8;
    public const int AccessibilityModeSettingId = 9;
    public const int DebugModeSettingId = 10;
    public const float DefaultDocumentHintDuration = 10f;
    public const float MinDocumentHintDuration = 1f;
    public const float MaxDocumentHintDuration = 60f;

    public static ServerSpecificSettingBase[] Settings()
    {
        var settingsList = new List<ServerSpecificSettingBase>()
        {
            new SSGroupHeader(HeaderSettingId,"シャープ鯖"),
            new SSKeybindSetting(ProximityChatKeybindSettingId,"近接チャット",KeyCode.V,true,false,hint:"一部の利用可能ロールで、近接チャットを使用するのに必要です。Vを推奨します"),
            new SSPlaintextSetting(RpNameSettingId,"キャラクター名","",20,hint:"RPのキャラ名です。設定した名前の後に本当の名前が表示されます。"),
            new SSKeybindSetting(AbilityUseKeybindSettingId,"アビリティ使用",KeyCode.LeftAlt,true,false,hint:"HUDの Ability に表示されている選択中アビリティを発動します。左Altを推奨します。"),
            new SSKeybindSetting(AbilitySwitchKeybindSettingId,"アビリティ切り替え",KeyCode.Mouse2,true,false,hint:"HUDに Ability 1/2 のように表示されている場合、次のアビリティへ切り替えます。中マウスボタンを推奨します。"),
            new SSKeybindSetting(ItemModeSwitchKeybindSettingId,"アイテムモード切り替え",KeyCode.G,true,false,hint:"Hybridアイテムのモードを切り替えます。Gを推奨します"),
            new SSKeybindSetting(SuicideButtonKeybindSettingId,"自害ボタン",KeyCode.K,true,false,hint:"銃器や近接武器を所持しているときに自分に向けて攻撃します。Kを推奨します"),
            new SSPlaintextSetting(SecretPasscodeSettingId, "シークレットパスコード","00000", 5, hint:"特別な場面で必要となるかもしれません・・・"),
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
                "OFF",
                "ON",
                false,
                hint:"一部のモデルを見やすい表示へ切り替えます。"),
            new SSTwoButtonsSetting(DebugModeSettingId, "[ADMIN]デバッグモード", "ON", "OFF", true)
        };
        return settingsList.ToArray();
    }

    public static bool IsManagedSettingId(int settingId)
        => settingId is HeaderSettingId
            or ProximityChatKeybindSettingId
            or RpNameSettingId
            or AbilityUseKeybindSettingId
            or AbilitySwitchKeybindSettingId
            or ItemModeSwitchKeybindSettingId
            or SecretPasscodeSettingId
            or DocumentHintDurationSettingId
            or AccessibilityModeSettingId
            or DebugModeSettingId;
}
