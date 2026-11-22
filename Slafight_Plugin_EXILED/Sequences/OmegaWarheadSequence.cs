using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.Sequences;

public class OmegaWarheadSequence : SequenceBase
{
    public override string Name => "OMEGA WARHEAD";
    public override string Description => "全施設を爆破するぜYEAH";
    public override byte Priority { get; set; } = 255;
    protected override bool MeetsTriggerConditions()
    {
        if (Warhead.IsInProgress) return false;
        return true;
    }
    
    public SpeakerApi.Playback Playback { get; private set; }

    protected override void OnTrigger(SequenceRun scope)
    {
        
        base.OnTrigger(scope);
    }
}