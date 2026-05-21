using System;
using System.Collections.Generic;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp096;
using JetBrains.Annotations;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using EventHandler = Slafight_Plugin_EXILED.MainHandlers.EventHandler;

namespace Slafight_Plugin_EXILED.CustomRoles.SCPs;

public class Scp096Anger : CRole  // 属性なしで自動登録
{
    protected override string RoleName { get; set; } = "SCP-096";
    protected override string Description { get; set; } =
        "<size=24><color=red>SCP-096: Anger</color>\nSCP-096の怒りと悲しみが再び不安定化し、本来の力が戻ってきた！\n<color=red>自分を見てきた相手を地の底まで追いかけろ！！！</color>";

    protected override float DescriptionDuration { get; set; } = 8.5f;
    protected override bool DescriptionShowRoleName { get; set; } = false;
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.Scp096Anger;
    protected override CTeam Team { get; set; } = CTeam.SCPs;
    protected override string UniqueRoleKey { get; set; } = "Scp096_Anger";

    private static readonly Dictionary<Player, Vector3> ShyGuyPositions = new();
    
    private Action<string, string, Vector3, bool, Transform, bool, float, float> CreateAndPlayAudio =>
        EventHandler.CreateAndPlayAudio;
    
    public override void RegisterEvents()
    {
        Log.Info("[Scp096Anger] RegisterEvents");  // 確認用
        Exiled.Events.Handlers.Scp096.Enraging += OnEnraging;
        Exiled.Events.Handlers.Scp096.AddingTarget += OnTargetAdded;
        Exiled.Events.Handlers.Scp096.CalmingDown += OnCalming;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Log.Info("[Scp096Anger] UnregisterEvents");
        Exiled.Events.Handlers.Scp096.Enraging -= OnEnraging;
        Exiled.Events.Handlers.Scp096.AddingTarget -= OnTargetAdded;
        Exiled.Events.Handlers.Scp096.CalmingDown -= OnCalming;
        base.UnregisterEvents();
    }
    
    protected override void OnRoleDying(DyingEventArgs ev)
    {
        if (ev.Player != null)
        {
            ShyGuyPositions.Remove(ev.Player);
            InTryNotToCryAnim.Remove(ev.Player);  // ★安全クリーンアップ
        }
        CassieHelper.AnnounceTermination(ev, "SCP 0 9 6", $"<color={Team.GetTeamColor()}>{RoleName}</color>", true);
        base.OnRoleDying(ev);
    }

    private void StartAnger(Player player)
    {
        foreach (Door door in Door.List)
        {
            if (door.Type == DoorType.HeavyContainmentDoor && door.Room.Type == RoomType.Hcz096)
            {
                door.Lock(DoorLockType.AdminCommand);
            }
        }
            
        Vector3 shyguyPosition = ShyGuyPositions[player];
        Vector3 spawnPoint = new Vector3(shyguyPosition.x + 1f, shyguyPosition.y + 0f, shyguyPosition.z);
        Npc term_npc = Npc.Spawn("SCP-096 Chamber Facility Guard", RoleTypeId.FacilityGuard, false, position: spawnPoint);
        term_npc.Transform.localEulerAngles = new Vector3(0, -90, 0);
    }

    private void OnEnraging(EnragingEventArgs ev)
    {
        if (ev.Player?.GetCustomRole() != CRoleTypeId.Scp096Anger) return;
        bool isInAnim = InTryNotToCryAnim.TryGetValue(ev.Player, out bool animValue) ? animValue : false;
        if (isInAnim)
        {
            ev.IsAllowed = false;
            return;
        }
        foreach (var npc in Npc.List)
        {
            if (npc.CustomName == "SCP-096 Chamber Facility Guard")
            {
                npc.Destroy();
            }
        }
    }

    public override void SpawnRole(Player? player, RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        InTryNotToCryAnim[player!] = false;  // ★セット
        
        player!.Role.Set(RoleTypeId.Scp096);
        player.UniqueRole = UniqueRoleKey;
        player.SetCustomInfo("SCP-096: ANGER");
        player.MaxArtificialHealth = 1000;
        player.MaxHealth = 8000;
        player.Health = 8000;
        player.MaxHumeShield = 150f;
        player.HumeShieldRegenerationMultiplier = 0.35f;
        ChangeSpeedState(player, false);
        
        player.Transform.eulerAngles = new Vector3(0, -90, 0);
        ShyGuyPositions[player] = player.Position;
        
        Log.Debug("Scp096: Anger was Spawned!");
        Timing.CallDelayed(0.1f, () => StartAnger(player));
    }

    private Dictionary<Player, bool> InTryNotToCryAnim = [];  // そのまま
    
    private void OnTargetAdded(AddingTargetEventArgs ev)
    {
        if (!Check(ev.Player)) return;
        
        // ★TryGetValueで完全安全（例外ゼロ）
        var isInAnim = InTryNotToCryAnim.TryGetValue(ev.Player, out bool animValue) ? animValue : false;
        if (ev.Scp096.RageManager.IsEnraged || isInAnim) return;
        
        Log.Debug("Scp096Anger: TargetAdded Triggered");
        InTryNotToCryAnim[ev.Player] = true;
        ev.Player.EnableEffect(EffectType.Slowness, 95);
        ev.Player.EnableEffect(EffectType.DamageReduction, 90);
        CreateAndPlayAudio("096Angered.ogg", "Scp096", ev.Player.Position, true, ev.Player.Transform, false, 80f, 0f);
        Timing.CallDelayed(35f, () =>
        {
            if (!Check(ev.Player)) return;
            InTryNotToCryAnim.Remove(ev.Player);  // 安全削除
            ChangeSpeedState(ev.Player, true);
            ev.Player.DisableEffect(EffectType.DamageReduction);
            ev.Scp096.Enrage(999f);
        });
    }

    private void OnCalming(CalmingDownEventArgs ev)
    {
        if (!Check(ev.Player)) return;
        if (ev.Scp096.Targets.Count != 0)
        {
            ev.IsAllowed = false;
            ev.ShouldClearEnragedTimeLeft = true;
            return;
        }
        ChangeSpeedState(ev.Player, false);
    }
    
    protected override void OnRoleHurtingOthers(HurtingEventArgs ev)
    {
        if (ev.Attacker != null)
        {
            ev.Amount = 999999;
            ev.Attacker.ArtificialHealth += 25;
            Timing.CallDelayed(1f, () =>
            {
                if (!Check(ev.Attacker)) return;
                if (ev.Attacker.Role is not Scp096Role scp096Role) return;
                if (scp096Role.Targets.Count == 0)
                    scp096Role.Calm();
            });
        }
    }

    private void ChangeSpeedState([CanBeNull] Player player, bool isFast)
    {
        if (!Check(player)) return;
        if (isFast)
        {
            player!.EnableEffect(EffectType.MovementBoost, 50);
            player!.DisableEffect(EffectType.Slowness);
            player!.EnableEffect(EffectType.Invigorated, 20);
        }
        else
        {
            player!.EnableEffect(EffectType.Slowness, 40);
            player!.DisableEffect(EffectType.MovementBoost);
            player!.DisableEffect(EffectType.Invigorated);
        }
    }
}
