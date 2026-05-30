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
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
        Exiled.Events.Handlers.Player.Scp1576TransmissionEnded -= OnTransmissionEnded;
    }

    private static CoroutineHandle _coroutineHandle;

    private static void OnWaitingForPlayers()
    {
        Timing.KillCoroutines(_coroutineHandle);
        _coroutineHandle = Timing.RunCoroutine(UpdateCoroutine());
    }

    private static void OnTransmissionEnded(Scp1576TransmissionEndedEventArgs ev)
    {
        if (ev.Player is null) return;
        var instance = Scp1576Database.Instances.Find(x => x.Player == ev.Player);
        if (instance is not null)
        {
            Scp1576Database.Instances.Remove(instance);
        }

        if (Scp1576Database.Instances.Count <= 0)
        {
            WaveManager.Waves.ForEach(x =>
            {
                if (x is TimeBasedWave wave)
                {
                    wave.Timer.IsForcefullyPaused = false;
                }
            });
        }
    }

    private static IEnumerator<float> UpdateCoroutine()
    {
        while (true)
        {
            if (Round.IsEnded)
            {
                Scp1576Database.Instances.Clear();
                yield break;
            }
            
            foreach (var player in Player.List)
            {
                if (player is null || !player.IsAlive) continue;
                if (Scp1576Database.Instances.Find(x => x.Player == player) != null) continue;
                if (player.IsEffectActive<CustomPlayerEffects.Scp1576>())
                {
                    Scp1576 scp1576 = null;
                    if (player.CurrentItem is Scp1576 item)
                        scp1576 = item;
                    Scp1576Database.Instances.Add(new Scp1576Instance { Player = player, Scp1576 = scp1576 });
                }
            }

            if (Scp1576Database.Instances.Count >= 1)
            {
                WaveManager.Waves.ForEach(x =>
                {
                    if (x is TimeBasedWave wave)
                    {
                        wave.Timer.IsForcefullyPaused = true;
                    }
                });
            }

            yield return Timing.WaitForSeconds(1f);
        }
    }
}