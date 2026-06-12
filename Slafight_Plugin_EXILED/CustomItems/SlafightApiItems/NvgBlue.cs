using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class NvgBlue : CItemNvg
{
    public override string DisplayName => "ナイトビジョンゴーグル - 青";
    public override string Description =>
        "遠くや暗い場所まで見えるようになる暗視ゴーグル。電池を消費するが、周りの情報が分かる。";

    protected override string UniqueKey => "NvgBlue";

    /// <summary>NvgBlue は SCP-1344 視覚効果 (青視野) を併用するため打ち消さない。</summary>
    protected override bool Remove1344Effect => false;

    protected override NvgProfile NvgProfile => new()
    {
        DrainPerSecond = 3f,
        LightColor     = Color.blue,
        LightRange     = 180f,
        LightIntensity = 10000f,
        WornBlindnessIntensity = 15,
        UseBlackout    = true,
    };

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.blue;
}
