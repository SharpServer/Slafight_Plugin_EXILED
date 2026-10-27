using Exiled.API.Features;
using PlayerRoles;
using PlayerRoles.Filmmaker;
using Slafight_Plugin_EXILED.Animations.Bases;
using Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.Animations.SCPs;

/// <summary>
/// 明滅する照明の下で SCP-3114 の周りを昇りながら一周し、最後に顔へ寄る登場演出です。
/// </summary>
/// <remarks>スポーン地点が取れなければ <see cref="ScpIntroAnimation.FallbackRoom"/> に立たせます。</remarks>
public sealed class Scp3114IntroAnimation : ScpIntroAnimation
{
    private const string Stare = "stare";
    private const float OrbitSeconds = 8f;

    private static readonly Color Cold = new(0.75f, 0.85f, 1f);
    private static readonly Color Blood = new(1f, 0.2f, 0.15f);

    private Light key;
    private float brightness = KeyLight * 0.7f;

    protected override RoleTypeId Role => RoleTypeId.Scp3114;

    protected override float FocusHeight => 0.65f;

    protected override void BuildCamera(FilmmakerAnimationBuilder builder)
    {
        float radius = Fit(3f);

        builder
            .Key(0f, Around(0f, radius, 0.2f), Focus)
            .Path(
                OrbitSeconds,
                t =>
                {
                    float progress = Mathf.SmoothStep(0f, 1f, t / OrbitSeconds);
                    return Around(360f * progress, radius, Mathf.Lerp(0.2f, 0.6f, progress));
                },
                _ => Focus)
            .Cue(Stare)
            .MoveTo(1.3f, Eye(0f, 0.62f, Fit(1.05f)), Focus, FilmmakerBlendPreset.Smooth)
            .Shake(1.2f, 0.06f, 8f);
    }

    protected override void OnStaging(FilmmakerCutscene cutscene)
    {
        key = cutscene.SpawnLight(Local(0f, 2.3f, 0f), Cold, brightness, 9f);

        // 切れかけの蛍光灯。
        cutscene.Every(0.08f, () => key.Intensity = Random.value < 0.2f ? brightness * 0.05f : brightness);
    }

    protected override void OnActorReady(FilmmakerCutscene cutscene, Npc npc)
    {
        FollowCamera(cutscene);
    }

    protected internal override void OnCue(FilmmakerCutsceneCueEventArgs ev)
    {
        if (ev.Cue != Stare)
            return;

        key.Color = Blood;
        brightness = KeyLight;
        ev.Cutscene.Delay(1f, () => Act(ev.Cutscene, "Scp3114Slap/Shoot->Click"));
    }
}
