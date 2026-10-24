using System.Collections.Generic;
using Exiled.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// 脱出できる場所です。バニラの <c>Escape.EscapeZones</c> へ触るのはここだけにしてください。
/// </summary>
/// <remarks>
/// <para>
/// <b>バニラの既定ゾーン (地上ゲート付近の一辺 25 m の立方体) は使いません。</b>
/// 脱出できる場所はマップが決めます。ProjectMER のトリガーポイントに
/// <see cref="MapTag"/> のタグを付ければ、そこが脱出点になります。
/// </para>
/// <para>
/// 旧実装は脱出点を自前の座標リストで持ち、0.5 秒ごとに
/// 全プレイヤー × 全脱出点を総当たりしていました。バニラの脱出ゾーンに積んでしまえば
/// 内外判定は本体が毎フレームやるので、巡回コルーチンは要りません。
/// </para>
/// <para>
/// 読み直し (<see cref="Rebuild"/>) はマップやスキマティックが変わるたびに走ります。
/// イベントや役職が足した脱出点はマップ由来と別に覚えてあるので、読み直しで消えません。
/// </para>
/// </remarks>
public static class EscapeZones
{
    /// <summary>
    /// 脱出点として読むトリガーポイントのタグです。
    /// </summary>
    public const string MapTag = "EscapePoint";

    /// <summary>
    /// 点で足したときの判定半径 (m) です。旧実装の判定距離と同じ値にしてあります。
    /// </summary>
    public const float DefaultRadius = 1.75f;

    /// <summary>
    /// マップ由来ではない、手で足したぶんです。読み直しのたびに積み直します。
    /// </summary>
    private static readonly List<Bounds> Added = [];

    /// <summary>
    /// いま脱出できる場所です。
    /// </summary>
    public static IReadOnlyList<Bounds> All => Escape.EscapeZones;

    /// <summary>
    /// 脱出点を 1 つ足します。ラウンド再開で消えます。
    /// </summary>
    public static void Add(Vector3 center, float radius = DefaultRadius) => Add(Box(center, radius));

    /// <inheritdoc cref="Add(Vector3, float)"/>
    public static void Add(Bounds zone)
    {
        Added.Add(zone);
        Escape.EscapeZones.Add(zone);
    }

    /// <summary>
    /// 手で足した脱出点をすべて外します。マップ由来のぶんは残ります。
    /// </summary>
    public static void ClearAdded()
    {
        if (Added.Count == 0) return;

        Added.Clear();
        Rebuild();
    }

    /// <summary>
    /// マップの <see cref="MapTag"/> を読み直して積み直します。
    /// </summary>
    public static void Rebuild()
    {
        Escape.EscapeZones.Clear();

        int fromMap = 0;

        foreach (CustomTriggerPoint point in CustomTriggerPoint.GetAll())
        {
            if (point.Tag != MapTag) continue;

            Escape.EscapeZones.Add(Box(point.Position, DefaultRadius));
            fromMap++;
        }

        foreach (Bounds zone in Added)
        {
            Escape.EscapeZones.Add(zone);
        }

        if (Escape.EscapeZones.Count == 0)
        {
            // バニラの既定ゾーンを外している以上、0 個は「誰も脱出できない」と同義。
            Log.Warn($"[Slafight] 脱出点が 1 つもありません。マップに \"{MapTag}\" のトリガーポイントを置いてください。");

            return;
        }

        Log.Debug($"[Slafight] 脱出点を {Escape.EscapeZones.Count} 個登録しました (マップ {fromMap} / 追加 {Added.Count})。");
    }

    /// <summary>
    /// バニラの既定ゾーンだけの状態に戻します。
    /// </summary>
    /// <remarks>
    /// プラグインを外したときに、脱出できない地図を残さないための後始末です。
    /// </remarks>
    public static void Reset()
    {
        Added.Clear();
        Escape.EscapeZones.Clear();
        Escape.EscapeZones.Add(Escape.DefaultEscapeZone);
    }

    /// <summary>
    /// 中心と半径から、バニラが判定に使う直方体を作ります。
    /// </summary>
    private static Bounds Box(Vector3 center, float radius) => new Bounds(center, Vector3.one * (radius * 2f));
}
