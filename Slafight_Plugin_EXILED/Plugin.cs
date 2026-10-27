using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Exiled.API.Features;
using HarmonyLib;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Features;
using UserSettings.ServerSpecific;

namespace Slafight_Plugin_EXILED;

/// <summary>
/// Slafight 本体です。
/// </summary>
/// <remarks>機能は <see cref="EventHandlerBase"/> 派生で書き、ここには Harmony・外部ツール・SSS の初期化だけを置きます。</remarks>
public class Plugin : Plugin<Config>
{
    public const string ServerName = "シャープ鯖";

    public static Plugin Singleton { get; set; } = null!;

    public override string Name => "Slafight_Plugin_EXILED";

    public override string Author => "org.sharp-server.jp.scpsl";

    public override string Prefix => "Slafight_Plugin_EXILED";

    public override Version Version => new(2, 0, 0, 0);

    public List<string> SpecificVersionTags => ["261009a"];

    public override Version RequiredExiledVersion { get; } = new(9, 15, 2);

    public Harmony HarmonyInstance { get; private set; }

    public override void OnEnabled()
    {
        Singleton = this;

        InitializeMediaTools();

        // 自動登録の起動。EventHandlerBase の派生クラスはこれだけで購読される。
        EventHandlerRegistry.Initialize();

        // 走査は PluginsEnabled 時点の AppDomain を見るので本来これは不要だが、
        // それはローダーの実行順に依存しているため名指しでも登録しておく。二重検出はしない。
        EventHandlerRegistry.RegisterAssembly(Assembly.GetExecutingAssembly());

        // 参加時の一括送信は止め、ServerSpecificsPageSync が人ごとのページを送る。
        ServerSpecificSettingsSync.DefinedSettings = [.. ServerSpecifics.Settings()];
        ServerSpecificSettingsSync.SendOnJoinFilter = _ => false;
        foreach (Player player in Player.List)
            ServerSpecifics.SendAll(player);

        // 再読込で id が衝突しないよう毎回変える。
        HarmonyInstance = new Harmony($"{Name}.{DateTime.UtcNow.Ticks}");
        HarmonyInstance.PatchAll();

        base.OnEnabled();
    }

    public override void OnDisabled()
    {
        HarmonyInstance?.UnpatchAll(HarmonyInstance.Id);
        HarmonyInstance = null;

        EventHandlerRegistry.Shutdown();
        GameMode.Shutdown();
        CustomRole.Shutdown();
        CustomItem.Shutdown();
        PlayerScope.Shutdown();
        RoundScope.Shutdown();

        InternalNpcRegistry.Clear();

        ServerSpecificSettingsSync.SendOnJoinFilter = null;
        ServerSpecificSettingsSync.DefinedSettings = [];
        ServerSpecificSettingsSync.SendToAll();

        Singleton = null!;

        base.OnDisabled();
    }

    /// <summary>
    /// 外部メディアツールを起こします。失敗してもプラグインの読み込みは続けます。
    /// 実際に使うときに再試行されるためです。
    /// </summary>
    private static void InitializeMediaTools()
    {
        try
        {
            FfmpegAudioDecoder.Initialize();
        }
        catch (Exception ex)
        {
            Log.Error($"[MediaTools] ffmpeg initialization failed. Audio/video features will retry on use, but the plugin will continue loading: {ex}");
        }

        try
        {
            YtDlpApi.Initialize();
        }
        catch (Exception ex)
        {
            Log.Error($"[MediaTools] yt-dlp initialization failed. URL media features will retry on use, but the plugin will continue loading: {ex}");
        }
    }
}
