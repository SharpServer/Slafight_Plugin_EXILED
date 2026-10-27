using Exiled.API.Features;
using PlayerRoles;
using PlayerRoles.Filmmaker;
using Slafight_Plugin_EXILED.Animations.Bases;
using Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.Animations.SCPs;

/// <summary>
/// 暗闇で照明が瞬くたびに SCP-173 が距離を詰め、最後は目の前に現れる登場演出です。
/// </summary>
public sealed class Scp173IntroAnimation : ScpIntroAnimation
{
    private const string LightsOn = "lights-on";
    private const string Blink = "blink";
    private const string Snap = "snap";
    private const float SnapTime = 5.4f;
    private const float BlinkDark = 0.22f;
    private const float FinalGap = 1.1f;

    private static readonly float[] BlinkTimes = [2.2f, 3.4f, 4.4f];

    private static readonly Color Warm = new(1f, 0.85f, 0.65f);
    private static readonly Color Cold = new(0.55f, 0.65f, 1f);

    private Light key;
    private Light rim;
    private float cameraEnd;
    private int blinks;

    protected override RoleTypeId Role => RoleTypeId.Scp173;

    protected override float FocusHeight => 0.9f;

    protected override void BuildCamera(FilmmakerAnimationBuilder builder)
    {
        float start = Fit(6.5f);
        cameraEnd = start - 0.6f;
        Vector3 face = Local(0f, FocusHeight, cameraEnd - FinalGap);

        // じりじり寄る間に瞬きで 173 だけが近づき、最後に跳ね退く。
        builder
            .Key(0f, Eye(0f, 0.75f, start), Focus)
            .Cue(0.4f, LightsOn)
            .MoveTo(SnapTime, Eye(0f, 0.7f, cameraEnd), Focus);

        foreach (float time in BlinkTimes)
            builder.Cue(time, Blink);

        builder
            .Cue(SnapTime, Snap)
            .MoveTo(0.12f, Eye(0f, 0.65f, cameraEnd + 0.2f), face, FilmmakerBlendPreset.Smooth)
            .Shake(0.7f, 0.35f, 22f)
            .Wait(0.9f);
    }

    protected override void OnStaging(FilmmakerCutscene cutscene)
    {
        key = cutscene.SpawnLight(Local(0f, 2.4f, 1.2f), Warm, 0f, 12f);
        rim = cutscene.SpawnLight(Local(0f, 1.8f, -1.2f), Cold, 0f, 8f, LightShadows.None);
    }

    protected internal override void OnCue(FilmmakerCutsceneCueEventArgs ev)
    {
        FilmmakerCutscene cutscene = ev.Cutscene;

        switch (ev.Cue)
        {
            case LightsOn:
                // 古い蛍光灯のように二度瞬いてから点く。
                cutscene.Animate(0.7f, t => key.Intensity = t is < 0.15f or (> 0.3f and < 0.4f) ? KeyLight * 0.15f : KeyLight);
                rim.Intensity = RimLight * 0.6f;
                break;

            case Blink:
                blinks++;
                Darken();
                if (Actor is { } npc)
                {
                    float step = (cameraEnd - FinalGap) / (BlinkTimes.Length + 1);
                    npc.Position = Local(0f, 0f, step * blinks);
                }

                cutscene.Delay(BlinkDark, Lighten);
                break;

            case Snap:
                Darken();
                if (Actor is { } closest)
                    closest.Position = Local(0f, 0f, cameraEnd - FinalGap);

                cutscene.Delay(0.18f, () =>
                {
                    key.Color = Color.red;
                    key.Intensity = FlashLight;
                    rim.Intensity = RimLight;
                    FadeLight(cutscene, key, KeyLight, 0.8f);
                });
                break;
        }
    }

    private void Darken()
    {
        key.Intensity = 0f;
        rim.Intensity = 0f;
    }

    private void Lighten()
    {
        key.Intensity = KeyLight;
        rim.Intensity = RimLight * 0.6f;
    }
}
