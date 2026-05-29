using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp966Role : CRole
{
    protected override string RoleName { get; set; } = "SCP-966";

    protected override string Description { get; set; } = "通常不可視の体を持つ恐怖の存在。\n" +
                                                          "眠りを貪り食い、財団に大打撃を与えよ！\n" +
                                                          "死体をむさぼることで足を速くできるぞ！";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp966;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp966";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp3114;
    protected override float? SpawnMaxHealth => 1500f;
    protected override bool SpawnClearsInventory => true;
    protected override string SpawnCustomInfo => "SCP-966";

    private static readonly Dictionary<Player, int> SpeedLevels = new();
    
    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Scp3114.Disguising += ExtendTime;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Scp3114.Disguising -= ExtendTime;
        base.UnregisterEvents();
    }
    
    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.MaxHumeShield = 100;
        SpeedLevels[player] = 0;

        var spawnRoom = Room.Get(RoomType.LczGlassBox);
        Log.Debug(spawnRoom.Position);
        var offset = new Vector3(0f, 1.5f, 0f);
        player.Position = spawnRoom.Position + spawnRoom.Rotation * offset;
        player.Rotation = spawnRoom.Rotation;
        Timing.RunCoroutine(Coroutine(player));
    }

    private static IEnumerator<float> Coroutine(Player player)
    {
        for (;;)
        {
            var speedLevel = SpeedLevels[player];
            if (player.GetCustomRole() != CRoleTypeId.Scp966)
            {
                // ★ 修正: RoleSpecificTextProvider を使用
                RoleSpecificTextProvider.Clear(player);
                player.DisableEffect(EffectType.Invisible);
                player.DisableEffect(EffectType.NightVision);
                player.DisableEffect(EffectType.Slowness);
                player.DisableEffect(EffectType.MovementBoost);
                yield break;
            }

            if (UnityEngine.Random.Range(0, 3) == 0)
            {
                player.DisableEffect(EffectType.Invisible);
                if (player.CurrentRoom?.RoomLightController != null)
                    player.CurrentRoom.RoomLightController.ServerFlickerLights(0.5f);
            }
            else
            {
                player.EnableEffect(EffectType.Invisible);
            }
            
            player.EnableEffect(EffectType.NightVision, 255);
            switch (speedLevel)
            {
                case <= 0:
                    player.EnableEffect(EffectType.Slowness, 30);
                    break;
                case 1:
                    player.EnableEffect(EffectType.Slowness, 20);
                    break;
                case 2:
                    player.EnableEffect(EffectType.Slowness, 10);
                    break;
                case 3:
                    player.EnableEffect(EffectType.MovementBoost, 0);
                    break;
                default:
                    player.EnableEffect(EffectType.MovementBoost, 10);
                    break;
            }

            // ★ 修正: RoleSpecificTextProvider を使用
            RoleSpecificTextProvider.Set(player, 
                "Speed Level: " + (Math.Abs(speedLevel + 1)) + "/5");
            
            yield return Timing.WaitForSeconds(UnityEngine.Random.Range(1.5f, 3f));
        }
    }

    private void ExtendTime(Exiled.Events.EventArgs.Scp3114.DisguisingEventArgs ev)
    {
        if (ev.Player?.GetCustomRole() != CRoleTypeId.Scp966) return;
    
        SpeedLevels[ev.Player] = SpeedLevels.GetValueOrDefault(ev.Player) + 1;
        if (SpeedLevels[ev.Player] > 4)
        {
            ev.Player.Heal(35f);
            SpeedLevels[ev.Player] = 4;
        }
        ev.IsAllowed = false;
        ev.Ragdoll.Destroy();
    }

    protected override void OnRoleHurtingOthers(HurtingEventArgs ev)
    {
        ev.Amount = 10f;
    }

    protected override void OnRoleHurting(HurtingEventArgs ev)
    {
        if (!ev.Player?.GetEffect(EffectType.Invisible)) return;
        ev.Attacker?.ShowHitMarker();
    }

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        SpeedLevels.Remove(ev.Player);
        CassieHelper.AnnounceTermination(ev, "SCP 9 6 6", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }
}
