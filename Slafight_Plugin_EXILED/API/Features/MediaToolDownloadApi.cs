using System;
using System.IO;
using System.Net;
using System.Net.Http;

namespace Slafight_Plugin_EXILED.API.Features;

internal static class MediaToolDownloadApi
{
    private static readonly HttpClient Client = CreateClient();

    public static string DownloadString(string url)
    {
        using var response = Client
            .GetAsync(url, HttpCompletionOption.ResponseContentRead)
            .GetAwaiter()
            .GetResult();
        response.EnsureSuccessStatusCode();
        return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    }

    public static void DownloadFile(string url, string destinationPath)
    {
        using var response = Client
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
            .GetAwaiter()
            .GetResult();
        response.EnsureSuccessStatusCode();

        using var input = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        input.CopyToAsync(output).GetAwaiter().GetResult();
        output.Flush(true);
    }

    private static HttpClient CreateClient()
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(15),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Slafight-Plugin-MediaTools/1.0");
        return client;
    }
}
