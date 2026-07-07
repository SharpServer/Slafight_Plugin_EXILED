using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.CustomMaps.Core.Interactions;

public class LeverHandler : IBootstrapHandler, IDisposable
{
    private static LeverHandler _instance;

    private readonly EventSubscriptionScope _subscriptions = new();
    private readonly Dictionary<string, Action<InteractableLeverToggledEventArgs>> _tagHandlers = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public static LeverHandler Instance => _instance;

    public static void Register()
    {
        Unregister();
        _instance = new LeverHandler();
    }

    public static void Unregister()
    {
        _instance?.Dispose();
        _instance = null;
    }

    private LeverHandler()
    {
        RegisterTagHandlers();
        _subscriptions.Add(
            () => InteractableLever.Toggled += OnLeverToggled,
            () => InteractableLever.Toggled -= OnLeverToggled);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _subscriptions.Dispose();
        _tagHandlers.Clear();
        GC.SuppressFinalize(this);
    }

    private void RegisterTagHandlers()
    {
        RegisterTagHandler("EZ_RemoteDoorControl", ev =>
        {
            if (ev.TurnedOn) return;
            ev.Lever.CanInteract = false;
            foreach (var player in Player.List.Where(p => p is not null && p.IsVanillaOrCustom(RoleTypeId.Scp079, CRoleTypeId.Scp079)).ToList())
            {
                if (player?.Role is Scp079Role role)
                {
                    player.ShowHint("<color=red>Remote Door ControlがONにされた！</color>\n※20秒後に復帰します", 20f);
                    role.LoseSignal(20f);
                }
            }

            Timing.CallDelayed(20f, () =>
            {
                if (ev.Lever is null) return;
                ev.Lever.CanInteract = true;
                ev.Lever.IsOn = true;
                if (ev.SourceEventArgs is null) return;
                SpeakerApi.Play("LeverFlip.ogg", ev.Lever.GetSoundId(ev.SourceEventArgs), ev.Lever.Position, true, isSpatial: false, maxDistance: 10f, minDistance: 0.1f);
            });
        });
    }

    private void RegisterTagHandler(string tag, Action<InteractableLeverToggledEventArgs> handler)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag cannot be empty.", nameof(tag));

        _tagHandlers[tag.Trim()] = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    private void OnLeverToggled(object? sender, InteractableLeverToggledEventArgs ev)
    {
        string tag = ev.Tag?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(tag))
            return;

        if (!_tagHandlers.TryGetValue(tag, out var handler))
            return;

        try
        {
            handler(ev);
        }
        catch (Exception e)
        {
            Log.Error($"[LeverHandler] Handler failed for tag '{tag}': {e}");
        }
    }
}
