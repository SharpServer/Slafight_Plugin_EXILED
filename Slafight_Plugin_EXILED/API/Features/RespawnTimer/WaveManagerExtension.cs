using System.Linq;
using Respawning;
using Respawning.Waves;
using Respawning.Waves.Generic;

namespace Slafight_Plugin_EXILED.API.Features.RespawnTimer;

public static class WaveManagerExtension
{
    public static TimeBasedWave? GetTimeBasedWave(this SpawnableWaveBase wave)
    {
        return wave as TimeBasedWave;
    }

    public static ILimitedWave? GetLimitedWave(this SpawnableWaveBase wave)
    {
        return wave.GetTimeBasedWave() as ILimitedWave;
    }

    public static int? GetRespawnTokens(this SpawnableWaveBase wave)
    {
        return wave.GetLimitedWave()?.RespawnTokens;
    }

    public static bool? IsWavePaused(this SpawnableWaveBase wave)
    {
        return wave.GetTimeBasedWave()?.Timer.IsPaused;
    }

    public static bool? IsWaveReady(this SpawnableWaveBase wave)
    {
        return wave.GetTimeBasedWave()?.Timer.IsReadyToSpawn;
    }

    public static float? GetWaveTime(this SpawnableWaveBase wave)
    {
        return wave.GetTimeBasedWave()?.Timer.TimeLeft;
    }

    public static float GetWaveTime(this TimeBasedWave wave)
    {
        return wave.Timer.TimeLeft;
    }

    public static bool IsWavePaused(this TimeBasedWave wave)
    {
        return wave.Timer.IsPaused;
    }

    public static SpawnableWaveBase? GetNextSpawnWave()
    {
        SpawnableWaveBase? nextWave = null;
        float minTime = float.MaxValue;

        foreach (var wave in WaveManager.Waves)
        {
            var timeBasedWave = wave.GetTimeBasedWave();
            if (timeBasedWave?.Timer.IsPaused != false)
                continue;

            var timeLeft = timeBasedWave.Timer.TimeLeft;
            if (timeLeft < minTime)
            {
                minTime = timeLeft;
                nextWave = wave;
            }
        }

        return nextWave;
    }

    public static SpawnableWaveBase? GetWaveWithShortestTime()
    {
        return WaveManager.Waves
            .Where(w => w.GetWaveTime().HasValue)
            .OrderBy(w => w.GetWaveTime()!.Value)
            .FirstOrDefault();
    }

    public static float? GetRespawnTime(this SpawnableWaveBase wave)
    {
        return wave?.GetWaveTime();
    }

    public static bool AreAllWavesPaused()
    {
        return WaveManager.Waves.All(wave =>
            wave.IsWavePaused() ?? true);
    }

    public static void LogWaveStatus()
    {
        foreach (var wave in WaveManager.Waves)
        {
            var timeLeft = wave.GetWaveTime();
            var isPaused = wave.IsWavePaused();
            var tokens = wave.GetRespawnTokens();
            var isReady = wave.IsWaveReady();
        }
    }
}
