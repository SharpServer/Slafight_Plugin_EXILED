using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 波が出たときのアナウンスと出撃曲を流します。内容は <see cref="SpawnSet"/> が名乗ります。
/// </summary>
public sealed class SpawnAnnouncer : EventHandlerBase
{
    /// <summary>
    /// 出撃曲を流すオーディオプレイヤーの名前です。
    /// </summary>
    private const string ThemeChannel = "WaveTheme";

    /// <inheritdoc />
    public override void RegisterEvents()
    {
        SpawnSystem.Spawned += OnSpawned;
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        SpawnSystem.Spawned -= OnSpawned;
    }

    /// <summary>
    /// 出撃曲を全体に流します。
    /// </summary>
    private static void PlayTheme(string theme)
    {
        try
        {
            SpeakerApi.Play(
                theme,
                ThemeChannel,
                Vector3.zero,
                destroyOnEnd: true,
                parent: null,
                isSpatial: false,
                maxDistance: 999999999f,
                minDistance: 0f);
        }
        catch (System.Exception exception)
        {
            // 曲が無い・壊れているだけで出撃そのものを巻き添えにしない。
            Log.Error($"[Slafight] ウェーブのテーマ '{theme}' を再生できませんでした: {exception}");
        }
    }

    private static void OnSpawned(SpawnedEventArgs ev)
    {
        SpawnAnnouncement announcement = ev.Wave.ComposeAnnouncement(ev.SpawnCount, ev.UnitName, ev.Players);

        if (!announcement.IsEmpty)
            CassieExtensions.CassieTranslated(announcement.CassieText, announcement.SubtitleText, announcement.Bell);

        if (ev.Wave.Theme is { Length: > 0 } theme)
            PlayTheme(theme);
    }
}
