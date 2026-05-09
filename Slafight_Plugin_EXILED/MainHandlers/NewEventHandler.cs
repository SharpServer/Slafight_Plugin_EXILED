using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp079;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.SpecialEvents;

namespace Slafight_Plugin_EXILED.MainHandlers;

public class NewEventHandler : IBootstrapHandler
{
    public static void Register()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers += OnWaitingForPlayers;
        Exiled.Events.Handlers.Scp079.Recontaining += OnOvercharged;
        Exiled.Events.Handlers.Player.TriggeringTesla += OnTesla;
        Exiled.Events.Handlers.Scp079.InteractingTesla += OnScp079InteractingTesla;
    }

    public static void Unregister()
    {
        Exiled.Events.Handlers.Server.WaitingForPlayers -= OnWaitingForPlayers;
        Exiled.Events.Handlers.Scp079.Recontaining -= OnOvercharged;
        Exiled.Events.Handlers.Player.TriggeringTesla -= OnTesla;
        Exiled.Events.Handlers.Scp079.InteractingTesla -= OnScp079InteractingTesla;
    }

    public static bool AlreadyRecovered { get; private set; } = false;
    public static bool IsTeslaIdled = false;

    public static void RecoverControl(FacilityControlRecoverType type)
    {
        if (AlreadyRecovered) return;
        AlreadyRecovered = true;
        switch (type)
        {
            case FacilityControlRecoverType.DisableTesla:
                IsTeslaIdled = true;
                Exiled.API.Features.Cassie.MessageTranslated(
                    "Attention, All personnel. All Facility Control System is now full operative. Disabled Tesla Gates for MtfUnit Operation.",
                    "全職員に通達。全ての施設制御システムの制御を取り戻しました。<split>全テスラゲートを機動部隊の作戦の為に無効化しました。");
                break;
            case FacilityControlRecoverType.EnableTesla:
                IsTeslaIdled = false;
                Exiled.API.Features.Cassie.MessageTranslated(
                    "Attention, All personnel. All Facility Control System is now full operative. Disabled Tesla Gates for Terminate Unknown Forces",
                    "全職員に通達。全ての施設制御システムの制御を取り戻しました。<split>全テスラゲートを不明な部隊の終了の為に有効化しました。");
                break;
        }
    }

    private static void OnWaitingForPlayers()
    {
        AlreadyRecovered = false;
        IsTeslaIdled = false;
    }

    private static void OnOvercharged(RecontainingEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        Timing.CallDelayed(25f, () =>
        {
            switch (SpecialEventsHandler.Instance.NowEvent)
            {
                case SpecialEventType.NuclearAttack:
                    RecoverControl(FacilityControlRecoverType.EnableTesla);
                    break;
                default:
                    RecoverControl(FacilityControlRecoverType.DisableTesla);
                    break;
            }
        });
    }

    private static void OnTesla(TriggeringTeslaEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        if (IsTeslaIdled)
        {
            ev.IsTriggerable = false;
        }
    }

    private static void OnScp079InteractingTesla(InteractingTeslaEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        if (IsTeslaIdled)
        {
            ev.IsAllowed = false;
        }
    }
}

public enum FacilityControlRecoverType
{
    DisableTesla,
    EnableTesla,
}