using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 波が出たときのアナウンスを流します。
/// </summary>
/// <remarks>
/// <b>ここには波ごとの分岐がありません。</b>
/// 何と言うか・何を流すかは波自身が
/// <see cref="SpawnSet.Announcement"/> と <see cref="SpawnSet.Theme"/> で名乗ります。
/// 旧実装は同じ処理を 24 分岐の switch 2 つで書いていました。
/// 波を足すときにこのファイルを触る必要はありません。
///
/// このクラスはどこからも登録されていません。<see cref="EventHandlerBase"/> を
/// 継承しているだけで <see cref="EventHandlerRegistry"/> が購読させます。
/// </remarks>
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
    /// <remarks>
    /// 位置を持たない全体再生です。引数は旧実装の呼び出しに合わせてあります
    /// (最後まで鳴らして片付ける・非空間・実質無限の到達距離)。
    /// </remarks>
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
        (string cassie, string subtitle) = ev.Wave.Announcement(ev.SpawnCount, ev.UnitName);

        if (cassie is { Length: > 0 } && ev.Wave.AnnouncesRemainingScps)
        {
            (string scpCassie, string scpSubtitle) = CustomSeason.Current.RemainingScps(CustomSeason.CountScps());
            cassie = $"{cassie} {scpCassie}";
            subtitle = $"{subtitle}<split>{scpSubtitle}";
        }

        if (cassie is { Length: > 0 })
            CassieExtensions.CassieTranslated(cassie, subtitle ?? string.Empty, false);

        if (ev.Wave.Theme is { Length: > 0 } theme)
            PlayTheme(theme);
    }
}
