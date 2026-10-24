using PlayerRoles;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.CustomRoles.Specials;

public class DAISUKE : CustomRole
{
    public override string Name => "DAISUKE";
    public override RoleTypeId BaseRole => RoleTypeId.Tutorial;
    private SpeakerApi.Playback _playback;

    protected override void OnSpawned()
    {
        _playback = MediaProcessingApi.PlayAudioFromUrl("https://www.youtube.com/watch?v=T9rMDOkPiRY", $"DAISUKE_{Player.NetId}",
            Player.Position, Player.Transform, isSpatial: true, loop: true);
        var light = Light.Create(Player.Position);
        light.Transform.SetParent(Player.Transform);
        light.Color = Color.blue;
        light.Intensity = 999;
        light.Range = 55;
        base.OnSpawned();
    }

    protected override void OnRemoved()
    {
        _playback.Stop();
        base.OnRemoved();
    }
}