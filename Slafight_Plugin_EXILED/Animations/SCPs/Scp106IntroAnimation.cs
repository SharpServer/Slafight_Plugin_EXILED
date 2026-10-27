using Exiled.API.Features;
using PlayerRoles;
using PlayerRoles.Filmmaker;
using Slafight_Plugin_EXILED.Animations.Bases;
using Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.Animations.SCPs;

/// <summary>
/// 腐食の光が脈打つ床から SCP-106 がせり上がり、見上げるカメラの周りで振り向く登場演出です。
/// </summary>
public sealed class Scp106IntroAnimation : ScpIntroAnimation
{
    private const string Emerge = "emerge";
    private const string Sink = "sink";
    private const float EmergeSeconds = 3.6f;
    private const float Depth = 2.4f;

    private static readonly Color Corrosion = new(0.35f, 0.5f, 0.2f);
    private static readonly Color Murk = new(0.6f, 0.7f, 0.5f);

    private Light glow;
    private Light key;

    protected override RoleTypeId Role => RoleTypeId.Scp106;

    protected override float FocusHeight => 0.6f;

    protected override void BuildCamera(FilmmakerAnimationBuilder builder)
    {
        float radius = Fit(3.2f);
        float sweepEnd = Mathf.Max(-60f, OpenArc(radius + 0.6f, 200f).From);

        // 真上から床を見下ろし、せり上がりに合わせて降りながら引く。
        builder
            .Key(0f, Eye(0f, 2.6f, Fit(2.2f)), Local(0f, -0.9f, 0f))
            .Cue(0.3f, Emerge)
            .MoveTo(3.9f, Eye(0.8f, 0.45f, radius), Focus, FilmmakerBlendPreset.Smooth)
            .Wait(0.4f)
            .Path(3.6f, t => Around(Mathf.Lerp(15f, sweepEnd, Mathf.SmoothStep(0f, 1f, t / 3.6f)), radius, 0.45f), _ => Focus)
            .Cue(Sink)
            .Wait(1.6f);
    }

    protected override void OnStaging(FilmmakerCutscene cutscene)
    {
        glow = cutscene.SpawnLight(Local(0f, -0.7f, 0.4f), Corrosion, RimLight, 6f, LightShadows.None);
        key = cutscene.SpawnLight(Local(0f, 2.4f, 1f), Murk, 0f, 10f);

        float phase = 0f;
        cutscene.Every(0.05f, () =>
        {
            phase += 0.05f;
            glow.Intensity = RimLight * (0.7f + 0.5f * Mathf.Sin(phase * 4f));
        });
    }

    protected override void OnActorReady(FilmmakerCutscene cutscene, Npc npc)
    {
        npc.Position = Stage + Vector3.down * Depth;
    }

    protected internal override void OnCue(FilmmakerCutsceneCueEventArgs ev)
    {
        FilmmakerCutscene cutscene = ev.Cutscene;

        switch (ev.Cue)
        {
            case Emerge:
                MoveActor(cutscene, Stage, EmergeSeconds);
                FollowCamera(cutscene);
                FadeLight(cutscene, key, KeyLight * 0.7f, 2.5f);
                break;

            case Sink:
                // 本体のストーク (床に沈む) を使い、使えなければ自前で沈める。
                if (!Act(cutscene, "Scp106StalkAbility/Run->Click"))
                    MoveActor(cutscene, Stage + Vector3.down * Depth, 1.4f);

                FadeLight(cutscene, key, 0f, 1.2f);
                break;
        }
    }
}
