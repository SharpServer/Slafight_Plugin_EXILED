using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.CustomRoles.Others;

public class SnowWarrier : CRole
{
    protected override string RoleName { get; set; } = "<color=white>SNOW WARRIER</color>";
    protected override string Description { get; set; } = "<size=24>非常に<color=#ffffff>雪玉的</color>である。そうは思わんかね？";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SnowWarrier;
    protected override CTeam Team { get; set; } = CTeam.Others;
    protected override string UniqueRoleKey { get; set; } = "SnowWarrier";

    public override void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);

        player!.Role.Set(RoleTypeId.ChaosRifleman, RoleSpawnFlags.All);
        player.Role.Set(RoleTypeId.Tutorial, RoleSpawnFlags.AssignInventory);
        player.UniqueRole = UniqueRoleKey;
        LabApiHandler.SchemSnowWarrier(LabApi.Features.Wrappers.Player.Get(player.ReferenceHub));

        const int maxHealth = 1000;

        Timing.CallDelayed(0.05f, () =>
        {
            player.TryAddFlag(SpecificFlagType.SpecialWeaponsDisabled);
            CustomInfoDisplay.Apply(player, "<color=#FFFFFF>SNOW WARRIER</color>");
            player.MaxHealth = maxHealth;
            player.Health = maxHealth;
            player.EnableEffect(EffectType.Slowness, 10);

            player.AddItem(ItemType.SCP1509);
            player.AddItem(ItemType.GunCOM18);
            player.AddItem(ItemType.ArmorHeavy);
            player.AddItem(ItemType.SCP500);
            player.AddItem(ItemType.SCP500);
            player.AddItem(ItemType.KeycardO5);

            player.SetAmmo(AmmoType.Nato9, 50);
        });
    }
}
