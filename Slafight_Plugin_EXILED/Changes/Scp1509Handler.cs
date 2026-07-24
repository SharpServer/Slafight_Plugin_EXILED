using System;
using Exiled.Events.EventArgs.Scp1509;
using Exiled.Events.Handlers;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;
using Slafight_Plugin_EXILED.Extensions;

namespace Slafight_Plugin_EXILED.Changes;

public class Scp1509Handler : IBootstrapHandler, IDisposable
{
    public static Scp1509Handler Instance { get; private set; }
    public static void Register()
    {
        Unregister();
        Instance = new();
    }

    public static void Unregister()
    {
        Instance?.Dispose();
        Instance = null;
    }

    private bool _disposed;

    public Scp1509Handler()
    {
        Scp1509.Resurrecting += OnReincarnating;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Scp1509.Resurrecting -= OnReincarnating;
        GC.SuppressFinalize(this);
    }

    private void OnReincarnating(ResurrectingEventArgs ev)
    {
        if (!ev.IsAllowed) return;
        if (ev.Player == null) return;
        if (CItem.Get<Scp148>()?.CheckHeld(ev.Player) == true) return;

        var caster = ev.Player;
        var target = ev.Target;

        Timing.CallDelayed(0.1f, () =>
        {
            if (target == null) return;
            // チーム判定
            switch (caster.GetTeam())
            {
                case CTeam.SCPs:
                    target.SetRole(RoleTypeId.Scp0492, RoleSpawnFlags.None);
                    Timing.CallDelayed(1f, () =>
                    {
                        target.UniqueRole = "Zombified";
                    });
                    break;
                case CTeam.FoundationForces:
                    target.SetRole(RoleTypeId.NtfPrivate, RoleSpawnFlags.None);
                    break;
                case CTeam.Guards:
                    target.SetRole(RoleTypeId.FacilityGuard, RoleSpawnFlags.None);
                    break;
                case CTeam.Scientists:
                    target.SetRole(RoleTypeId.Scientist, RoleSpawnFlags.None);
                    break;
                case CTeam.ClassD:
                    target.SetRole(RoleTypeId.ClassD, RoleSpawnFlags.None);
                    break;
                case CTeam.ChaosInsurgency:
                    target.SetRole(RoleTypeId.ChaosConscript, RoleSpawnFlags.None);
                    break;
                case CTeam.Fifthists:
                    target.SetRole(CRoleTypeId.FifthistConvert, RoleSpawnFlags.None);
                    break;
                case CTeam.GoC:
                    target.SetRole(CRoleTypeId.GoCOperative, RoleSpawnFlags.None);
                    break;
                default:
                    var state = caster.GetRoleInfo();
                    if (state.Custom != CRoleTypeId.None)
                        target.SetRole(state.Custom, RoleSpawnFlags.None);
                    else
                        target.SetRole(state.Vanilla, RoleSpawnFlags.None);
                    break;
            }
        });
    }
}
