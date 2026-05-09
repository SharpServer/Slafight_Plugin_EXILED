using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class NvgRed : CItemNvg
{
    public override string DisplayName => "ナイトビジョンゴーグル - 赤";
    public override string Description =>
        "遠くや暗い場所まで見えるようになる暗視ゴーグル。電池を消費しない。";

    protected override string UniqueKey => "NvgRed";

    protected override NvgProfile NvgProfile => new()
    {
        DrainPerSecond = 0f,
        LightColor     = Color.red,
        LightRange     = 30f,
        LightIntensity = 10000f,
        UseBlackout    = false,
    };

    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor   => Color.red;
}
