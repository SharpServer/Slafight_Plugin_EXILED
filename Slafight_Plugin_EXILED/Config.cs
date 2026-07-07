using System;
using System.ComponentModel;
using System.IO;

namespace Slafight_Plugin_EXILED;

using Exiled.API.Interfaces;
public class Config : IConfig
{
    [Description("プラグインの有効/無効を設定します")]
    public bool IsEnabled { get; set; } = true;

    [Description("デバッグログを出力するかどうか")]
    public bool Debug { get; set; } = true;

    [Description("サーバーのシーズンを設定します。0=通常, 1=ハロウィン, 2=クリスマス, 3=エイプリルフール, 4=第五祭, 5=夏")]
    public int Season { get; set; } = 0;

    [Description("ベータモードを有効にするかどうか")]
    public bool IsBeta { get; set; } = false;

    [Description("音声ファイルのディレクトリパス。空欄の場合は EXILED/ServerContents をデフォルトとして使用します")]
    private string _audioReferences;
    public string AudioReferences
    {
        get => string.IsNullOrEmpty(_audioReferences)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EXILED", "ServerContents")
            : _audioReferences;
        set => _audioReferences = value;
    }

    [Description("ProximityChat のスピーカー音量倍率。1.0 が通常音量、2.0 で約2倍です")]
    public float ProximityChatVolume { get; set; } = 2f;

    [Description("SpecialEvent（ゲーム中に低確率で発生するイベント）の有効/無効を設定します")]
    public bool EventAllowed { get; set; } = true;

    [Description("Omega Warhead が爆発するまでの時間（秒）")]
    public float OwBoomTime { get; set; } = 160f;

    [Description("Delta Warhead が爆発するまでの時間（秒）")]
    public float DwBoomTime { get; set; } = 100f;

}
