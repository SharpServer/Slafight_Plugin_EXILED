using MEC;
using ProjectMER.Events.Arguments;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Features;

using MerSchematic = ProjectMER.Events.Handlers.Schematic;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.Handlers;

/// <summary>
/// マップが置いた脱出点を、読み込みが落ち着いてからバニラの脱出ゾーンへ積み直します。
/// </summary>
public sealed class EscapeZoneHandler : EventHandlerBase
{
    /// <summary>待機開始からマップの読み込みが落ち着くまでの待ち時間 (秒)。</summary>
    private const float MapLoadDelay = 5f;

    /// <summary>スキマティックの増減が落ち着くまでの待ち時間 (秒)。</summary>
    private const float SchematicSettleDelay = 0.1f;

    /// <summary>予約の世代です。古い予約が起きても何もしないようにするための印です。</summary>
    private static int generation;

    private static CoroutineHandle refresh;

    /// <inheritdoc />
    public override void RegisterEvents()
    {
        MerSchematic.SchematicSpawned += OnSchematicSpawned;
        MerSchematic.SchematicDestroyed += OnSchematicDestroyed;
        ServerHandlers.RestartingRound += OnRestartingRound;
    }

    /// <inheritdoc />
    public override void UnregisterEvents()
    {
        MerSchematic.SchematicSpawned -= OnSchematicSpawned;
        MerSchematic.SchematicDestroyed -= OnSchematicDestroyed;
        ServerHandlers.RestartingRound -= OnRestartingRound;
    }

    /// <summary>
    /// マップが読み込まれる時機です。LabApi 側の <c>OnXxx</c> は override するだけで購読されます。
    /// </summary>
    public override void OnServerWaitingForPlayers() => Schedule(MapLoadDelay);

    /// <inheritdoc />
    protected override void OnDisposed()
    {
        generation++;
        Timing.KillCoroutines(refresh);

        // 脱出できない地図を残さない。
        EscapeZones.Reset();
    }

    private static void OnSchematicSpawned(SchematicSpawnedEventArgs ev) => Schedule(SchematicSettleDelay);

    private static void OnSchematicDestroyed(SchematicDestroyedEventArgs ev) => Schedule(SchematicSettleDelay);

    /// <summary>
    /// ラウンド再開時に脱出ゾーンを既定へ戻します。
    /// </summary>
    private static void OnRestartingRound()
    {
        generation++;
        Timing.KillCoroutines(refresh);
        EscapeZones.Reset();
    }

    /// <summary>
    /// 読み直しを予約します。続けて呼ばれたら最後の 1 回だけが走ります。
    /// </summary>
    private static void Schedule(float delay)
    {
        int scheduled = ++generation;

        Timing.KillCoroutines(refresh);

        refresh = Timing.CallDelayed(delay, () =>
        {
            if (scheduled != generation) return;

            EscapeZones.Rebuild();
        });
    }
}
