using Exiled.API.Features;
using PlayerRoles;
using PlayerRoles.Filmmaker;
using Slafight_Plugin_EXILED.Animations.Bases;
using Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.Animations.SCPs;

/// <summary>
/// 左右で交互に回る赤色灯の中を低く回り込むと、SCP-939 が飛びかかってくる登場演出です。
/// </summary>
public sealed class Scp939IntroAnimation : ScpIntroAnimation
{
    private const string Lunge = "lunge";

    private Light left;
    private Light right;

    protected override RoleTypeId Role => RoleTypeId.Scp939;

    protected override float FocusHeight => 0.1f;

    private float endDegrees;

    protected override void BuildCamera(FilmmakerAnimationBuilder builder)
    {
        const float sweepSeconds = 4.6f;
        const float radius = 4.2f;

        // 壁に当たらない範囲で、片側の端から正面近くまで低く回り込む。
        (float from, float to) = OpenArc(2.5f, 200f);
        float start = Mathf.Abs(from) >= Mathf.Abs(to) ? from : to;
        endDegrees = Mathf.Sign(start) * Mathf.Min(10f, Mathf.Abs(start));
        Vector3 lungeTarget = Focus + Facing * (Quaternion.Euler(0f, endDegrees, 0f) * Vector3.forward) * 1.5f;

        builder
            .Key(0f, Around(start, radius, 0.1f), Focus)
            .Path(
                sweepSeconds,
                t => Around(Mathf.Lerp(start, endDegrees, Mathf.SmoothStep(0f, 1f, t / sweepSeconds)), radius, 0.1f),
                _ => Focus)
            .Cue(Lunge)
            .MoveTo(0.35f, Around(endDegrees, radius + 1.6f, 0.25f), lungeTarget, FilmmakerBlendPreset.Smooth)
            .Shake(0.6f, 0.4f, 26f)
            .Wait(0.7f);
    }

    protected override void OnStaging(FilmmakerCutscene cutscene)
    {
        left = cutscene.SpawnLight(Local(-1.6f, 2f, 0.5f), Color.red, 0f, 9f, LightShadows.None);
        right = cutscene.SpawnLight(Local(1.6f, 2f, 0.5f), Color.red, 0f, 9f, LightShadows.None);

        bool flip = false;
        cutscene.Every(0.45f, () =>
        {
            flip = !flip;
            left.Intensity = flip ? KeyLight * 0.8f : RimLight * 0.1f;
            right.Intensity = flip ? RimLight * 0.1f : KeyLight * 0.8f;
        });
    }

    protected override void OnActorReady(FilmmakerCutscene cutscene, Npc npc)
    {
        FollowCamera(cutscene);

        // 本体の Focus で低く構えさせる。
        Act(cutscene, "Scp939FocusKeySync/Sneak->Hold");
    }

    protected internal override void OnCue(FilmmakerCutsceneCueEventArgs ev)
    {
        if (ev.Cue != Lunge)
            return;

        FilmmakerCutscene cutscene = ev.Cutscene;
        Act(cutscene, "Scp939LungeAbility/Shoot->Click");
        Act(cutscene, "Scp939FocusKeySync/Sneak->Release");

        Vector3 direction = Facing * (Quaternion.Euler(0f, endDegrees, 0f) * Vector3.forward);
        MoveActor(cutscene, Stage + direction * (FitAt(endDegrees, 4.2f) * 0.5f), 0.3f);

        left.Intensity = FlashLight;
        right.Intensity = FlashLight;
    }
}
