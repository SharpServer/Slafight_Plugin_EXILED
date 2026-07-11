using System.Linq;
using LabApi.Events.Arguments.PlayerEvents;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class Vent : ObjectPrefab
{
    protected override string SchematicName => "Vent";
    public string ExitPointTag { get; set; }

    protected override void OnSetup()
    {
        AddInteractable(duration: 1.5f);
    }
    protected override void OnToySearchedNearby(PlayerSearchedToyEventArgs ev)
    {
        var player = Player.Get(ev.Player);
        if (Schematic == null)
            return;
        
        if (string.IsNullOrEmpty(ExitPointTag)) return;
        CustomTriggerPoint marker = null;
        marker = CustomTriggerPoint.GetAll().FirstOrDefault(x => x.Tag == ExitPointTag);
        if (marker is null) return;
        
        SpeakerApi.Play("ventsound.ogg", "Vent", Schematic.Position, true, null, false, 10f, 0f);

        player.Position = marker.Position;
        player.Rotation = marker.Rotation;

        Timing.CallDelayed(0.1f, () =>
        {
            if (marker is null) return;
            SpeakerApi.Play("ventsound.ogg", "Vent", marker.Position, true, null, false, 10f, 0f);
        });
    }
}
