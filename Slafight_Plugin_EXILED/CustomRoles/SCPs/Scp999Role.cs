using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp999Role : CRole
{
    protected override string RoleName { get; set; } = "SCP-999";
    protected override string Description { get; set; } = "<size=24><color=#FF1493>SCP-999</color>\n全員とたわむれましょう！\n※勝敗には影響しません。可愛いペット的にふるまって\n攻撃してきた奴らに痛い一撃を喰らわせてやりましょう。";
    protected override float DescriptionDuration { get; set; } = 10f;
    protected override bool DescriptionShowRoleName { get; set; } = false;
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp999;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp999";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp173;
    protected override IReadOnlyList<CRoleEffect> SpawnEffects =>
    [
        new(EffectType.Fade, 255)
    ];

    protected override IReadOnlyList<SpecificFlagType> SpawnSpecificFlags =>
    [
        SpecificFlagType.GunsDisabled
    ];

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Player.SpawningRagdoll += CancelRagdoll;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Player.SpawningRagdoll -= CancelRagdoll;
        base.UnregisterEvents();
    }

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        int playerId = player.Id;

        Timing.CallDelayed(RoleSpawnTimings.AfterRoleSet, () =>
        {
            var current = Player.Get(playerId);
            if (!Check(current) || !IsSafeRolePlayer(current))
                return;

            var position = current.Position;
            current.Role.Set(RoleTypeId.Tutorial, RoleSpawnFlags.AssignInventory);
            TrySetPlayerPosition(current, position, "Scp999Role role swap");
            AssignIdentity(current);
            ApplySpawnSpecificFlags(current);
            FinalizeSpawnState(current);

            Timing.CallDelayed(RoleSpawnTimings.NextFrame, () =>
            {
                var next = Player.Get(playerId);
                if (!Check(next) || !IsSafeRolePlayer(next))
                    return;

                TrySetPlayerPosition(next, position, "Scp999Role next-frame restore");
                ApplySpawnSpecificFlags(next);
                next.SetCustomInfo("SCP-999");
            });
        });
    }

    private void FinalizeSpawnState(Player player)
    {
        player.MaxHealth = 999;
        player.Health = player.MaxHealth;
        player.ClearInventory();

        player.SetCustomInfo("SCP-999");
        LabApiHandler.Schem999(LabApi.Features.Wrappers.Player.Get(player.ReferenceHub));
    }
    
    private void CancelRagdoll(SpawningRagdollEventArgs ev)
    {
        if (Check(ev.Player))
            ev.IsAllowed = false;
    }
    
    protected override void OnRoleDying(DyingEventArgs ev)
    {
        CassieHelper.AnnounceTermination(ev, "SCP 9 9 9", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }
}
