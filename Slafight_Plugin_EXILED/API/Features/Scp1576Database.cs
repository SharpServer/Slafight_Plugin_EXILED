using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Features.Items;
using Exiled.Events.EventArgs.Player;
using MEC;
using Respawning;
using Respawning.Waves;
using Slafight_Plugin_EXILED.API.Interface;

namespace Slafight_Plugin_EXILED.API.Features;

public static class Scp1576Database
{
    public static List<Scp1576Instance> Instances = [];
}

public class Scp1576Instance
{
    public Player? Player { get; init; }
    public Scp1576? Scp1576 { get; init; }
}
public class Scp1576DatabaseHandler : IBootstrapHandler
{
    public static void Register()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
        Exiled.Events.Handlers.Player.Scp1576TransmissionEnded += OnTransmissionEnded;
        Exiled.Events.Handlers.Player.Left += OnLeft;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
        Exiled.Events.Handlers.Player.Scp1576TransmissionEnded -= OnTransmissionEnded;
        Exiled.Events.Handlers.Player.Left -= OnLeft;

        Timing.KillCoroutines(_coroutineHandle);
        ClearInstances();
    }

    private static CoroutineHandle _coroutineHandle;

    private static void OnWaitingForPlayers()
    {
        Timing.KillCoroutines(_coroutineHandle);
        ClearInstances();
        _coroutineHandle = Timing.RunCoroutine(UpdateCoroutine());
    }

    private static void OnTransmissionEnded(Scp1576TransmissionEndedEventArgs ev)
    {
        RemovePlayer(ev.Player);
        UpdateWavePauseState();
    }

    private static void OnLeft(LeftEventArgs ev)
    {
        RemovePlayer(ev.Player);
        UpdateWavePauseState();
    }

    private static IEnumerator<float> UpdateCoroutine()
    {
        while (true)
        {
            if (Round.IsEnded)
            {
                ClearInstances();
                yield break;
            }

            PruneInvalidInstances();
            
            foreach (var player in Player.List)
            {
                if (player is null || player.ReferenceHub == null || !player.IsAlive) continue;
                if (Scp1576Database.Instances.Find(x => x.Player?.Id == player.Id) != null) continue;
                if (player.IsEffectActive<CustomPlayerEffects.Scp1576>())
                {
                    Scp1576 scp1576 = null;
                    if (player.CurrentItem is Scp1576 item)
                        scp1576 = item;
                    Scp1576Database.Instances.Add(new Scp1576Instance { Player = player, Scp1576 = scp1576 });
                }
            }

            UpdateWavePauseState();

            yield return Timing.WaitForSeconds(1f);
        }
    }

    private static void RemovePlayer(Player player)
    {
        if (player is null)
            return;

        Scp1576Database.Instances.RemoveAll(x => x.Player?.Id == player.Id);
    }

    private static void PruneInvalidInstances()
    {
        Scp1576Database.Instances.RemoveAll(x =>
            x.Player is null ||
            x.Player.ReferenceHub is null ||
            !x.Player.IsAlive ||
            !x.Player.IsEffectActive<CustomPlayerEffects.Scp1576>());
    }

    private static void ClearInstances()
    {
        Scp1576Database.Instances.Clear();
        SetWavesPaused(false);
    }

    private static void UpdateWavePauseState()
    {
        SetWavesPaused(Scp1576Database.Instances.Count >= 1);
    }

    private static void SetWavesPaused(bool isPaused)
    {
        WaveManager.Waves.ForEach(x =>
        {
            if (x is TimeBasedWave wave)
                wave.Timer.IsForcefullyPaused = isPaused;
        });
    }
}
