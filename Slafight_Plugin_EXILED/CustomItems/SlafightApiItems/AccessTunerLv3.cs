using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems.IntermediateBases;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class AccessTunerLv3 : AccessTunerBase
{
    public override string DisplayName => "Access Tuner Level-3";
    public override string Description => "施設のメンテナンスに使用される整備用の診断装置。\n" +
                                          "Lv3のデータセルによって高い権限の扉やほぼ全ての施設に対して\n" +
                                          "ハッキングできる。";
    protected override string UniqueKey => "AccessTunerLv3";
    public override AccessTunerLevel AccessLevel => AccessTunerLevel.LevelThree;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.red;
}