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
/// <remarks>
/// <b>機能の登録をここに並べてはいけません。</b>
/// <see cref="EventHandlerBase"/> の派生クラスを書けば
/// <see cref="EventHandlerRegistry"/> が自動的に生成・購読します。
///
/// ここに書いてよいのは、自動登録では表現できない次の 3 つだけです。
/// <list type="number">
/// <item>Harmony のパッチ適用 / 解除</item>
/// <item>外部ツール (ffmpeg / yt-dlp) の初期化</item>
/// <item>Server Specific Settings の配布</item>
/// </list>
///
/// 以前の <c>OnEnabled</c> / <c>OnDisabled</c> は手書きの
/// <c>Register()</c> / <c>Unregister()</c> を約 45 対抱えており、
/// 反射式の <c>AutoHandlerBootstrapRegister</c> と 2 系統が併存していました。
/// ここに <c>Register()</c> を足したくなったら、それは
/// <see cref="EventHandlerBase"/> で書くべきものだという合図です。
/// </remarks>
public class Plugin : Plugin<Config>
{
    public const string ServerName = "シャープ鯖";

    public static Plugin Singleton { get; set; } = null!;

    public override string Name => "Slafight_Plugin_EXILED";

    public override string Author => "org.sharp-server.jp.scpsl";

    public override string Prefix => "Slafight_Plugin_EXILED";

    public override Version Version => new(2, 0, 0, 0);

    public List<string> SpecificVersionTags => ["261009a"];

    public override Version RequiredExiledVersion { get; } = new(9, 14, 2);

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

        ServerSpecificSettingsSync.DefinedSettings = ServerSpecifics.Settings().ToArray();
        ServerSpecificSettingsSync.SendToAll();

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
