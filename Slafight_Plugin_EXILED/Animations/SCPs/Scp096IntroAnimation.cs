using Exiled.API.Features;
using PlayerRoles;
using PlayerRoles.Filmmaker;
using Slafight_Plugin_EXILED.Animations.Bases;
using Slafight_Plugin_EXILED.API.Features.FilmmakerAnimations;
using UnityEngine;
using Light = Exiled.API.Features.Toys.Light;

namespace Slafight_Plugin_EXILED.Animations.SCPs;

/// <summary>
/// 背を向けた SCP-096 へ低くゆっくり寄ると、振り向いた瞬間に部屋が赤く染まり、こちらへ詰め寄ってくる登場演出です。
/// </summary>
public sealed class Scp096IntroAnimation : ScpIntroAnimation
{
    private const string Reveal = "reveal";

    private static readonly Color Pale = new(0.85f, 0.85f, 0.8f);

    private Light key;
    private Light rim;

    protected override RoleTypeId Role => RoleTypeId.Scp096;

    protected override float FocusHeight => 1f;

    protected override float DesiredSpace => 7f;

    protected override void BuildCamera(FilmmakerAnimationBuilder builder)
    {
        // 096 の部屋は狭いので、距離は部屋の奥行きに対する比率で決める。
        builder
            .Key(0f, Eye(0f, 0.25f, Span(1f)), Focus)
            .MoveTo(5.8f, Eye(0f, 0.3f, Span(0.45f)), Focus)
            .Cue(Reveal)
            .Shake(1.2f, 0.45f, 24f)
            .MoveTo(0.45f, Eye(0f, 0.35f, Span(0.85f)), Focus, FilmmakerBlendPreset.Smooth)
            .Wait(0.7f);
    }

    protected override void OnStaging(FilmmakerCutscene cutscene)
    {
        key = cutscene.SpawnLight(Local(0f, 2.5f, 0.6f), Pale, KeyLight * 0.5f, 8f);
        rim = cutscene.SpawnLight(Local(0f, 1.5f, -1.4f), Pale, RimLight * 0.4f, 6f, LightShadows.None);
    }

    protected override void OnActorReady(FilmmakerCutscene cutscene, Npc npc)
    {
        // うずくまって壁を向いている。壁が近ければ本体の「泣き」の姿勢にもなる。
        npc.Rotation = Facing * Quaternion.Euler(0f, 180f, 0f);
        Act(cutscene, "Scp096TryNotToCryAbility/Zoom->Hold");
    }

    protected internal override void OnCue(FilmmakerCutsceneCueEventArgs ev)
    {
        if (ev.Cue != Reveal)
            return;

        FilmmakerCutscene cutscene = ev.Cutscene;

        Act(cutscene, "Scp096TryNotToCryAbility/Zoom->Release");
        if (Actor is { } npc)
            npc.Rotation = Facing;

        key.Color = Color.red;
        key.Intensity = FlashLight;
        rim.Color = Color.red;
        rim.Intensity = RimLight;
        FadeLight(cutscene, key, KeyLight, 1f);

        cutscene.SetRoomColor(StageRoom, Color.red);
        cutscene.SetRoomLights(StageRoom, true);

        MoveActor(cutscene, Local(0f, 0f, Span(0.3f)), 0.5f);
    }
}
