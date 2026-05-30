using System.Collections.Generic;
using System.IO;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using HintServiceMeow.Core.Utilities.Image;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;

namespace Slafight_Plugin_EXILED.Hints.SpawnTimer;

public class SpawnTimerHandler : SlafightLabApiHandler, IBootstrapHandler
{
    private static SpawnTimerHandler _instance;
    private static CoroutineHandle handle;

    public static void Register()
    {
        return;
        _instance = LabApiHandlerRegistry.Register(_instance);
    }

    public static void Unregister()
    {
        return;
        LabApiHandlerRegistry.Unregister(ref _instance);
    }

    protected override void RegisterEvents(EventSubscriptionScope subscriptions)
    {
        subscriptions.Add(() => Exiled.Events.Handlers.Player.ChangingRole += OnSpectating, () => Exiled.Events.Handlers.Player.ChangingRole -= OnSpectating);
        subscriptions.Add(() => Exiled.Events.Handlers.Server.RoundStarted += OnRoundStarted, () => Exiled.Events.Handlers.Server.RoundStarted -= OnRoundStarted);
    }

    private void OnRoundStarted()
    {
        Timing.KillCoroutines(handle);
        handle = Timing.RunCoroutine(UpdateCoroutine());
    }

    private void OnSpectating(ChangingRoleEventArgs ev)
    {
        if (!ev.IsAllowed || ev.Player is null) return;
        if (!ev.NewRole.IsAlive())
        {
            SetupHUD(ev.Player);
        }
        else
        {
            var display = ev.Player.GetPlayerDisplay();
            display.RemoveHint(HudConstId.SpawnTimerHUD_FoundationStaff);
            display.RemoveHint(HudConstId.SpawnTimerHUD_FoundationEnemy);
            display.RemoveHint(HudConstId.SpawnTimerHUD_ExpectedSpawn);
        }
    }

    private void SetupHUD(Player player)
    {
        var display = player.GetPlayerDisplay();
        var foundationStaff = new Hint()
        {
            Id = HudConstId.SpawnTimerHUD_FoundationStaff,
            Text = "null",
            Alignment = HintAlignment.Left,
            XCoordinate = 320,
            YCoordinate = 120,
            SyncSpeed = HintSyncSpeed.Fast,
            ResolutionBasedAlign = true
        };
        var foundationEnemy = new Hint()
        {
            Id = HudConstId.SpawnTimerHUD_FoundationEnemy,
            Text = "null",
            Alignment = HintAlignment.Right,
            XCoordinate = -320,
            YCoordinate = 120,
            SyncSpeed = HintSyncSpeed.Fast,
            ResolutionBasedAlign = true
        };
        var expectedSpawn = new Hint()
        {
            Id = HudConstId.SpawnTimerHUD_ExpectedSpawn,
            Text = "null",
            Alignment = HintAlignment.Center,
            XCoordinate = 0,
            YCoordinate = 240,
            SyncSpeed = HintSyncSpeed.Fast,
            ResolutionBasedAlign = true
        };
        display.AddHint(foundationStaff);
        display.AddHint(foundationEnemy);
        display.AddHint(expectedSpawn);
        ImageHintPlayer.PlayFile(display, Path.Combine(Plugin.Singleton.Config.AudioReferences, "test.png"));
    }

    private IEnumerator<float> UpdateCoroutine()
    {
        while (true)
        {
            foreach (var player in Player.List)
            {
                if (player is null || player.IsAlive) continue;
                var display = player.GetPlayerDisplay();
                display.GetHint(HudConstId.SpawnTimerHUD_FoundationStaff)?.Text = "n分n秒";
                display.GetHint(HudConstId.SpawnTimerHUD_FoundationEnemy)?.Text = "n分n秒";
                display.GetHint(HudConstId.SpawnTimerHUD_ExpectedSpawn)?.Text = "Null-0 Null Reference Expectionが到着します...";
            }

            yield return Timing.WaitForSeconds(0.1f);
        }
    }
}
