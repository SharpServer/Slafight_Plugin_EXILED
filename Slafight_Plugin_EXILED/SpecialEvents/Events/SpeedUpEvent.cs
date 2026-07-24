using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;

// SpecialEvent 基底クラス

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

public class SpeedUpEvent : SpecialEvent
{
    // ===== メタ情報 =====
    public override SpecialEventType EventType => SpecialEventType.SpeedUpEvent;
    public override int MinPlayersRequired => 0; // 誰もいなくても可なら 0
    public override string LocalizedName => "- ULTIMATE SPEED EX -";
    public override string TriggerRequirement => "Only available in April fools.";

    // ===== ショートカット =====
    // ===== 実行本体 =====
    public override bool IsReadyToExecute()
    {
        return MapFlags.GetSeason() == SeasonTypeId.April;
    }

    protected override void OnExecute(int eventPid)
    {
        foreach (var player in Player.List)
        {
            if (player == null) continue;
            player.EnableEffect(EffectType.MovementBoost, 255);
            player.EnableEffect(EffectType.Scp207, 255);
            player.TryAddFlag(SpecificFlagType.Scp207Resistance);
            player.IsUsingStamina = false;
        }
        
        Exiled.API.Features.Cassie.MessageTranslated("$pitch_.2 .g4 .g4 $pitch_1 Attention All Personnel . By Division . Extremely Advanced Adrenaline Injection Detected . Please Stay Safe And Continue Work .",
            "お菓子の戦士達の妨害工作により、全職員に超絶加速アドレナリンが注射されていることが確認されました。安全に気を付けて業務を続けてください。");

        Timing.RunCoroutine(SpeedUpCoroutine());
    }

    private IEnumerator<float> SpeedUpCoroutine()
    {
        while (true)
        {
            if (CancelIfOutdated()) yield break;
            foreach (var player in Player.List)
            {
                if (player == null) continue;
                player.EnableEffect(EffectType.MovementBoost, 255);
                player.EnableEffect(EffectType.Scp207, 255);
                player.TryAddFlag(SpecificFlagType.Scp207Resistance);
                player.IsUsingStamina = false;
            }
            yield return Timing.WaitForSeconds(5f);
        }
    }

    public override void RegisterEvents() { }
    public override void UnregisterEvents() { }
}