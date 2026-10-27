using Exiled.API.Features;
using PlayerRoles;
using PlayerRoles.Filmmaker;
using Slafight_Plugin_EXILED.Animations.Bases;
using Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.Animations.SCPs;

/// <summary>
/// 背を向けた SCP-049 に寄ると振り返り、カメラを目で追いながら回り込まれ、最後に顔を覗き込む登場演出です。
/// </summary>
public sealed class Scp049IntroAnimation : ScpIntroAnimation
{
    private const string Turn = "turn";
    private const string Close = "close";
    private const float TurnSeconds = 1.3f;
    private const float OrbitEnd = 140f;

    private static readonly Color Clinical = new(0.7f, 0.8f, 1f);
    private static readonly Color Candle = new(1f, 0.75f, 0.45f);

    private Light key;
    private Light rim;
    private float orbitEnd;

    protected override RoleTypeId Role => RoleTypeId.Scp049;

    protected override float FocusHeight => 0.75f;

    protected override void BuildCamera(FilmmakerAnimationBuilder builder)
    {
        float near = Fit(2.6f);
        orbitEnd = Mathf.Min(OrbitEnd, OpenArc(near + 0.6f, 300f).To);

        builder
            .Key(0f, Eye(-0.6f, 0.55f, Fit(5.5f)), Focus)
            .MoveTo(3.6f, Eye(-0.3f, 0.6f, near), Focus, FilmmakerBlendPreset.Smooth)
            .Cue(Turn)
            .Wait(1f)
            .Path(5f, t => Around(Mathf.Lerp(-7f, orbitEnd, Mathf.SmoothStep(0f, 1f, t / 5f)), near, 0.6f), _ => Focus)
            .Cue(Close)
            .Cut(Around(orbitEnd, 1.25f, 0.8f), Focus)
            .Wait(1.4f);
    }

    protected override void OnStaging(FilmmakerCutscene cutscene)
    {
        key = cutscene.SpawnLight(Local(0f, 2.6f, 0.3f), Clinical, KeyLight * 0.8f, 9f);
        rim = cutscene.SpawnLight(Local(0f, 1.2f, -1.5f), Clinical, RimLight * 0.5f, 7f, LightShadows.None);

        // 古い照明のわずかな揺らぎ。
        cutscene.Every(0.12f, () => key.Intensity = KeyLight * Random.Range(0.7f, 0.8f));
    }

    protected override void OnActorReady(FilmmakerCutscene cutscene, Npc npc)
    {
        npc.Rotation = Facing * Quaternion.Euler(0f, 180f, 0f);
    }

    protected internal override void OnCue(FilmmakerCutsceneCueEventArgs ev)
    {
        FilmmakerCutscene cutscene = ev.Cutscene;

        switch (ev.Cue)
        {
            case Turn:
                RotateActor(cutscene, Facing, TurnSeconds);
                cutscene.Delay(TurnSeconds, () => FollowCamera(cutscene));
                key.Color = Candle;
                break;

            case Close:
                FadeLight(cutscene, rim, RimLight * 1.4f, 0.6f);
                Act(cutscene, "Scp049CallAbility/Reload->Click");
                break;
        }
    }
}
