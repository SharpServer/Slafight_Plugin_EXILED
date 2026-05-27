using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Warhead;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Changes;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

public class AlphaWarheadOverride : AbilityBase
{
    // AbilityBase の抽象プロパティを実装
    protected override float DefaultCooldown => 999f;
    protected override int DefaultMaxUses => 1;

    // 完全デフォルト
    public AlphaWarheadOverride(Player owner)
        : base(owner) { }

    // コマンドなどから上書きしたいとき用
    public AlphaWarheadOverride(Player owner, float cooldownSeconds)
        : base(owner, cooldownSeconds) { }

    public AlphaWarheadOverride(Player owner, float cooldownSeconds, int maxUses)
        : base(owner, cooldownSeconds, maxUses) { }
    protected override void ExecuteAbility(Player player)
    {
        if (!OmegaWarhead.CanBeStart() || Warhead.IsInProgress || MapFlags.IsOverrideActivated)
        {
            player.RemoveAbility<AlphaWarheadOverride>();
            player.AddAbility<AlphaWarheadOverride>();
            player.ShowHint("※実行に失敗しました", 3f);
            return;
        }
        FacilityLightHandler.OnWarhead(new StartingEventArgs(null, false, true));
        Exiled.API.Features.Cassie.MessageTranslated(
            "$PITCH_0.2 .g4 .g4 BY $PITCH_0.8 BY ORDER OF FACILITY SYSTEM CONTROL, ALPHA WARHEAD FORCE OPERATION ACTIVATED. DETONATE IN T MINUS 90 SECONDS.",
            "<color=red><b>BY ORDER OF FACILITY SYSTEM CONTROL, ALPHA WARHEAD FORCE OPERATION ACTIVATED. DETONATE IN T-90 SECONDS. </b></color>");
        Timing.CallDelayed(5f, () =>
        {
            SpeakerApi.Play("warhead079.ogg", "AlphaWarhead", Vector3.zero, true, null, false, 999999999, 0);
            Timing.RunCoroutine(Coroutine());
            player.RemoveAbility<AlphaWarheadOverride>();
        });
    }

    private static IEnumerator<float> Coroutine()
    {
        var elapsed = 0f;
        while (elapsed < 90f)
        {
            if (Round.IsLobby) yield break;
            elapsed++;
            yield return Timing.WaitForSeconds(1f);
        }
        Warhead.Detonate();
    }
}