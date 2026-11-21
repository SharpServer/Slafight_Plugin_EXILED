using System;
using System.ComponentModel;
using System.IO;
using Exiled.API.Interfaces;
using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED;

public class Config : IConfig
{
    [Description("プラグインの有効/無効を設定します")]
    public bool IsEnabled { get; set; } = true;

    [Description("デバッグログを出力するかどうか")]
    public bool Debug { get; set; } = true;

    [Description("サーバーが満員になった時に待機時間を飛ばしてラウンドを自動開始する処理を無効化します")]
    public bool DisableFullServerRoundStart { get; set; } = true;

    [Description("SeasonTypeIdを用いた季節指定をします。デフォルトはNoneです。")]
    public SeasonTypeId Season { get; set; } = SeasonTypeId.None;

    [Description("ベータモードを有効にするかどうか")]
    public bool IsBeta { get; set; } = false;

    [field: Description("音声ファイルのディレクトリパス。空欄の場合は EXILED/ServerContents をデフォルトとして使用します")]
    public string AudioReferences
    {
        get => string.IsNullOrEmpty(field)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EXILED",
                "ServerContents")
            : field;
        set;
    }

    [Description("ProximityChat のスピーカー音量倍率。1.0 が通常音量、2.0 で約2倍です")]
    public float ProximityChatVolume { get; set; } = 2f;




    [Description("Discord Bot連携(bot.py)のFlask APIエンドポイント。通常は変更不要")]
    public string DiscordBotApiUrl { get; set; } = "http://localhost:5000";

    [Description("Discord Bot連携(bot.py)のAPI認証用共有シークレット。bot.py側のAPI_SECRETと同じ値を設定してください。" +
                 "このリポジトリはGitHubで公開しているため、シークレットをソースコードに直書きせずここで設定します。" +
                 "空のままだとBot側で401拒否され、人数送信・モデレーション通知が届きません。")]
    public string DiscordBotApiSecret { get; set; } = string.Empty;

    [Description("ドアのアクセス拒否 Hint の文言です。")]
    public DoorAccessDeniedHintConfig DoorAccessDeniedHint { get; set; } = new();
}
