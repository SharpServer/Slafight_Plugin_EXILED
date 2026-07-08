using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Slafight_Plugin_EXILED.API.Features;

using Exiled.API.Features;

/// <summary>
/// Discord Bot 側 (bot.py の Flask エンドポイント /moderation_event) へ、
/// 通報・警告・Kick/Ban・FF 等のモデレーション関連イベントを通知するための橋渡し。
/// </summary>
public static class ModerationBridge
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    /// <summary>
    /// モデレーションイベントを非同期・Fire-and-forget で送信する。失敗しても呼び出し元には影響しない。
    /// </summary>
    /// <param name="type">イベント種別 (例: "warn", "kick", "ban", "report_cheater", "report_local", "friendly_fire")</param>
    /// <param name="data">イベント固有のデータ（匿名型など、JSON シリアライズ可能なもの）</param>
    public static void Send(string type, object data)
    {
        _ = SendAsync(type, data);
    }

    private static async Task SendAsync(string type, object data)
    {
        try
        {
            var payload = new
            {
                type,
                server = Plugin.ServerName,
                timestamp = DateTime.UtcNow,
                data,
            };

            var config = Plugin.Singleton?.Config;
            string baseUrl = config?.DiscordBotApiUrl ?? "http://localhost:5000";
            string url = $"{baseUrl.TrimEnd('/')}/moderation_event";

            string json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Api-Key", config?.DiscordBotApiSecret ?? string.Empty);

            await HttpClient.SendAsync(request);
        }
        catch (TaskCanceledException tce)
        {
            Log.Debug($"[ModerationBridge] Send({type}) timeout: {tce.Message}");
        }
        catch (HttpRequestException hre)
        {
            Log.Debug($"[ModerationBridge] Send({type}) failure: {hre.Message}");
        }
        catch (Exception ex)
        {
            Log.Error($"[ModerationBridge] Send({type}) error: {ex}");
        }
    }
}
