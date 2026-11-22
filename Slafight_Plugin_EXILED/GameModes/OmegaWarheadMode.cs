using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Core.Extensions;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.CustomEffects;
using Slafight_Plugin_EXILED.CustomTeams;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.Sequences;

namespace Slafight_Plugin_EXILED.GameModes;

public class OmegaWarheadMode : GameMode
{
    public override string Name => "OMEGA WARHEAD";
    protected override void OnStarted()
    {
        Exiled.API.Features.Cassie.MessageTranslated("ATTENTION, ALL PERSONNEL. SCP _SUFFIX_PLURAL_REGULAR ANOMALY POWER UP AND BREACH IS CONFIRMED FOR EVERY. PLEASE EVACUATE NOW FROM THIS FACILITY IMMEDIATELY. THINKING APPROACH _SUFFIX_PLURAL_SYLLABIC . . .",
            "全職員に通達。全SCPの異常な能力向上および収容違反が確認されました。<split>直ちに施設からの避難を行ってください。<split>現在対応策を検討中です…",
            true);
        foreach (var player in Player.List.Where(p => p.IsSafePlayer() && p.IsInTeam<ScpTeam>()))
        {
            player.MaxHealth += 2000f;
            player.EnableEffect<NaturalHeal>(duration: 20f,intensity: 100);
            player.EnableEffect<DamageBoost>(duration: 300f, intensity: 10);
        }
        var coroutine = Timing.RunCoroutine(Coroutine());
        Scope.Track(coroutine);
    }

    private IEnumerator<float> Coroutine()
    {
        if (IsCanceled) yield break;
        yield return Timing.WaitForSeconds(80f);
        if (IsCanceled) yield break;
        Exiled.API.Features.Cassie.MessageTranslated("ATTENTION, ALL PERSONNEL. STATUS IS CRITICAL. OVER SEER COMMAND DECIDED OMEGA PROTOCOL. PLEASE EVACUATE FROM THIS FACILITY NOW.",
            "全職員に通達。事態は深刻であることが確認され、監督委員会は<color=blue><b>OMEGAプロトコル</b></color>の実施を決定しました。<split>繰り返しますが職員は直ちにこの施設から避難してください。",
            true);
        yield return Timing.WaitForSeconds(20f);
        if (IsCanceled) yield break;
        if (!SequenceManager.TryStart(new OmegaWarheadSequence(),true))
            Log.Warn("[Slafight] OMEGA WARHEAD sequence did not start.");
    }
}