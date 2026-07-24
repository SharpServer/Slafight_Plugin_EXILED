using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>
/// 地下鉄演出の列車1台分。<see cref="Features.TerminalTrain"/> がサイクル管理を担い、
/// このクラスは Position/Create/Destroy のライフサイクルを ObjectPrefab に寄せるだけの薄いラッパー。
/// </summary>
public class TerminalTrainCar : ObjectPrefab
{
    protected override string SchematicName => "STrain";
    protected override float SetupDelay => 0f;
}
