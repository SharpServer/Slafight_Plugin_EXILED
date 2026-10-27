using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 現在の接続人数を Discord Bot へ定期送信します。
/// </summary>
public sealed class PlayerCountReporter : EventHandlerBase
{
    private static readonly HttpClient HttpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    private const int IntervalMilliseconds = 60000;

    private CancellationTokenSource cancellation;

    /// <inheritdoc />
    protected override void OnEnabled()
    {
        if (string.IsNullOrEmpty(Plugin.Singleton?.Config?.DiscordBotApiSecret))
        {
            Log.Warn(
                "[Slafight] Config.DiscordBotApiSecret が未設定です。Discord Bot 連携 (人数送信) は " +
                "Bot 側に 401 で拒否されます。bot.py の API_SECRET と同じ値を設定してください。");

            return;
        }

        cancellation = new CancellationTokenSource();
        _ = ReportLoop(cancellation.Token);
    }

    /// <inheritdoc />
    protected override void OnDisposed()
    {
        cancellation?.Cancel();
        cancellation?.Dispose();
        cancellation = null;
    }

    private static async Task ReportLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Report(Player.List.Count(player => player.IsSafePlayer() && !player.IsNPC));
            }
            catch (Exception exception)
            {
                Log.Error($"[Slafight] 人数送信で例外が発生しました: {exception}");
            }

            try
            {
                await Task.Delay(IntervalMilliseconds, token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private static async Task Report(int count)
    {
        Config config = Plugin.Singleton?.Config;

        if (config is null) return;

        try
        {
            string json = JsonSerializer.Serialize(new
            {
                server = Plugin.ServerName,

                // Bot 側 (bot.py) は同一ホストで動く複数サーバーをポート番号で識別するため、
                // 人数の表示先チャンネルの振り分けに必要。
                port = Server.Port,
                count,
                timestamp = DateTime.UtcNow,
            });

            string url = $"{config.DiscordBotApiUrl.TrimEnd('/')}/playercount";

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            request.Headers.Add("X-Api-Key", config.DiscordBotApiSecret ?? string.Empty);

            await HttpClient.SendAsync(request);
        }
        catch (TaskCanceledException exception)
        {
            Log.Debug($"[Slafight] 人数送信がタイムアウトしました: {exception.Message}");
        }
        catch (HttpRequestException exception)
        {
            Log.Debug($"[Slafight] 人数送信に失敗しました: {exception.Message}");
        }
    }
}
