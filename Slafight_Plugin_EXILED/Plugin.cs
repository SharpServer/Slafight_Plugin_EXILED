using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Exiled.CustomItems.API.Features;
using Exiled.CustomRoles.API.Features;
using Slafight_Plugin_EXILED.CustomItems;
using System.Text.Json;
using System.Threading;
using HarmonyLib;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Features.RoundVictory.Core;
using Slafight_Plugin_EXILED.Changes;
using Slafight_Plugin_EXILED.CustomEffects;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.CustomMaps.Entities;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;
using Slafight_Plugin_EXILED.Patches;
using UserSettings.ServerSpecific;

namespace Slafight_Plugin_EXILED;

using Exiled.API.Features;
public class Plugin : Plugin<Config>
{
    public static Plugin Singleton { get; set; } = null!;
    private CancellationTokenSource _playerCountCts;
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
    public override string Name => "Slafight_Plugin_EXILED";
    public override string Author => "org.sharp-server.jp.scpsl";
    public override string Prefix => "Slafight_Plugin_EXILED";
    public override Version Version => new(1, 8, 1, 3);
        
    public override Version RequiredExiledVersion { get; } = new(9, 14, 2);

    public Harmony HarmonyInstance { get; private set; }
    // Enable & Disable
    public override void OnEnabled()
    {
        Singleton = this;
        PlayerSpeakerManager.RegisterEvents();
        ProximityChat.Handler.RegisterEvents();
        VoiceRecordingApi.RegisterEvents();
        WaypointChunkStreamer.RegisterEvents();

        NetworkVisibilityManager.Register();
        NvgManager.Register();
            
        WearsHandler.Register();
        HIDTurretObject.RegisterEvents();
        Tentacle.RegisterEvents();
        KillCounter.Register();
        CRole.RegisterAllEvents();
        CItem.RegisterAllItems();
        Scp914ProcessorFix.Register();
        AbilityBase.RegisterEvents();
        AbilityManager.RegisterEvents();
        RoundVictoryEvents.Register();
        CustomRole.RegisterRoles(false);
        CustomItemsManager.RegisterAllItems();
        CustomStatusEffectsRegistry.AllRegister();
            
        Scp012_033.Register();
        CandyChanges.Register();
        MapGuardHandler.Register();
        TerminalRift.Register();
        VentControl.Register();
        FacilityLightHandler.Register();
        // GateAEnding.Register(); SCRAPPED
        WarheadBoomEffectHandler.Register();
        Communications.Register();
        Scp914Changes.Register();
        Scp513.Register();
            
        AutoHandlerBootstrapRegister.Register();
        ServerSpecificsHandler.Register();
        CustomShieldState.RegisterEvents();

        var Settings = ServerSpecifics.Settings();
        var a = Settings.ToList();
        ServerSpecificSettingsSync.DefinedSettings = a.ToArray();
        ServerSpecificSettingsSync.SendToAll();
        Log.Debug($"Settings List: \n{ServerSpecificSettingsSync.DefinedSettings}");
            
        HarmonyInstance = new Harmony($"{Name}.{DateTime.UtcNow.Ticks}");
        HarmonyInstance.PatchAll();

        // ここから差し替え
        _playerCountCts?.Cancel();                     // 念のため前回のを殺す
        _playerCountCts = new CancellationTokenSource();
        _ = SendPlayerCountLoop(_playerCountCts.Token);
        // ここまで差し替え

        base.OnEnabled();

    }

    public override void OnDisabled()
    {
        Singleton = null!;
        try
        {
            _playerCountCts?.Cancel();
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to cancel player count loop: {ex}");
        }
        finally
        {
            _playerCountCts = null;
        }
            
        PlayerSpeakerManager.UnregisterEvents();
        ProximityChat.Handler.UnregisterEvents();
        VoiceRecordingApi.UnregisterEvents();
        WaypointChunkStreamer.UnregisterEvents();

        NvgManager.Unregister();
        NetworkVisibilityManager.Unregister();
            
        WearsHandler.Unregister();
        HIDTurretObject.UnregisterEvents();
        Tentacle.UnregisterEvents();
        CRole.UnregisterAllEvents();
        CItem.UnregisterAllItems();
        Scp914ProcessorFix.Unregister();
        AbilityBase.UnregisterEvents();
        AbilityManager.UnregisterEvents();
        RoundVictoryEvents.Unregister();
        CustomItem.UnregisterItems();
        CustomRole.UnregisterRoles();
        CustomStatusEffectsRegistry.Unhook();
           
        Scp012_033.Unregister();
        CandyChanges.Unregister();
        MapGuardHandler.Unregister();
        TerminalRift.Unregister();
        VentControl.Unregister();
        FacilityLightHandler.Unregister();
        // GateAEnding.Unregister(); SCRAPPED
        WarheadBoomEffectHandler.Unregister();
        Communications.Unregister();
        Scp914Changes.Unregister();
        Scp513.Unregister();
        OmegaWarhead.Shutdown();
        
        AutoHandlerBootstrapRegister.Unregister();
        ServerSpecificsHandler.Unregister();
        CustomShieldState.UnregisterEvents();
        RoleSpecificTextProvider.ClearAll();
        DebugModeHandler.ClearAll();
        RPNameSetter.ClearAll();
            
        HarmonyInstance?.UnpatchAll(HarmonyInstance.Id);
        HarmonyInstance = null;
            
        ServerSpecificSettingsSync.DefinedSettings = [];
        ServerSpecificSettingsSync.SendToAll();
            
        base.OnDisabled();
    }
        
    private async Task SendPlayerCountLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await SendPlayerCountAsync(Player.List.Where(p => !p.IsNPC && p.IsNotHost()).ToList().Count);
            }
            catch (Exception ex)
            {
                Log.Error($"SendPlayerCountLoop error: {ex}");
            }

            try
            {
                await Task.Delay(60000, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task SendPlayerCountAsync(int count)
    {
        try
        {
            var data = new
            {
                server = "シャープ鯖",
                count = count,
                timestamp = DateTime.UtcNow
            };

            string json = JsonSerializer.Serialize(data);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            await HttpClient.PostAsync("http://localhost:5000/playercount", content);
        }
        catch (TaskCanceledException tce)
        {
            // 一時的なタイムアウトとして扱う
            Log.Debug($"SendPlayerCountAsync timeout: {tce.Message}");
        }
        catch (HttpRequestException hre)
        {
            Log.Debug($"SendPlayerCountAsync failure: {hre.Message}");
        }
        catch (Exception ex)
        {
            Log.Error($"SendPlayerCountAsync error: {ex}");
        }
    }
}
