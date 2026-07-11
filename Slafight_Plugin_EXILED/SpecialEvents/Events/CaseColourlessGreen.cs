using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.Events.EventArgs.Map;
using MEC;
using Mirror;
using PlayerRoles;
using ProjectMER.Features;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;
using Slafight_Plugin_EXILED.Extensions; // SpecialEvent 基底クラス
using UnityEngine;

namespace Slafight_Plugin_EXILED.SpecialEvents.Events;

public class CaseColourlessGreen : SpecialEvent
{
    // ===== メタ情報 =====
    public override SpecialEventType EventType => SpecialEventType.CaseColourlessGreen;
    public override int MinPlayersRequired => 3;
    public override string LocalizedName => "CASE COLOURLESS GREEN";
    public override string TriggerRequirement => "無し";

    private CoroutineHandle _handle;

    public override bool IsReadyToExecute()
    {
        return MapFlags.GetSeason() is SeasonTypeId.FifthFestival or SeasonTypeId.Summer;
    }

    // ===== ショートカット =====
    // ===== 実行本体 =====
    protected override void OnExecute(int eventPid)
    {
        Timing.KillCoroutines(_handle);
        _handle = Timing.RunCoroutine(Coroutine());
        SetupMaps();
        RoleAssign();
    }

    public override void RegisterEvents()
    {
        Exiled.Events.Handlers.Map.GeneratorActivating += OnGenerating;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Exiled.Events.Handlers.Map.GeneratorActivating -= OnGenerating;
        base.UnregisterEvents();
    }

    private void SetupMaps()
    {
        if (IsCanceled()) return;
        RoundHazardController.DisableLightDecontamination();
        RoundHazardController.SetAlphaWarheadDisarmLocked(true);
        RoundHazardController.SetDeadmanSwitchBlocked(true);
        new AntiMemeBomb {Position = StaticUtils.GetWorldFromRoomLocal(RoomType.LczClassDSpawn, new Vector3(-25.32238f, 0f, 0f), Vector3.zero).worldPosition}.Create();
        foreach (var generator in Generator.List)
        {
            if (generator is null) continue;
            NetworkServer.Destroy(generator.GameObject);
        }
        
        MapUtils.LoadMap("ccg");
        foreach (var generator in Generator.List)
        {
            if (generator is null) continue;
            generator.ActivationTime = 20f;
        }
        SetDoorState(ZoneType.Entrance, true);
        SetDoorState(ZoneType.HeavyContainment, true);
        SetDoorState(ZoneType.LightContainment, true);
    }

    private void SetDoorState(ZoneType zoneType, bool isLock)
    {
        if (IsCanceled()) return;
        switch (zoneType)
        {
            case ZoneType.Entrance:
                SetLockState(Door.Get(DoorType.CheckpointEzHczA), isLock);
                SetLockState(Door.Get(DoorType.CheckpointEzHczB), isLock);
                SetLockState(Door.Get(DoorType.CheckpointGateA), isLock);
                SetLockState(Door.Get(DoorType.CheckpointGateB), isLock);
                break;
            case ZoneType.HeavyContainment:
                SetLockState(Door.Get(DoorType.CheckpointLczA), isLock);
                SetLockState(Door.Get(DoorType.CheckpointLczB), isLock);
                break;
            case ZoneType.LightContainment:
            {
                var list = Door.Get(x => x.Rooms.Any(room => room.Type is RoomType.LczClassDSpawn)).ToList();
                foreach (var door in list)
                {
                    if (door is null) continue;
                    SetLockState(door, isLock);
                }
                break;
            }
        }
    }

    private void SetLockState(Door? door, bool isLock)
    {
        if (IsCanceled()) return;
        if (isLock)
        {
            door?.IsOpen = false;
            door?.Lock(DoorLockType.AdminCommand);
        }
        else
        {
            door?.IsOpen = true;
            door?.Unlock();
        }
    }

    private void OnGenerating(GeneratorActivatingEventArgs ev)
    {
        if (IsCanceled()) return;
        switch (ev.Generator?.Room.Zone)
        {
            case ZoneType.Entrance:
                SetDoorState(ZoneType.Entrance, false);
                Exiled.API.Features.Cassie.MessageTranslated("Opened Heavy Containment Zone Checkpoint. Please go there immediately. . . . .", "重度収容区画とのチェックポイントが解放されました。<split>直ちに向かってください",true);
                break;
            case ZoneType.HeavyContainment:
                SetDoorState(ZoneType.HeavyContainment, false);
                Exiled.API.Features.Cassie.MessageTranslated("Opened Light Containment Zone Checkpoint. Please go there immediately. . . . .", "軽度収容区画とのチェックポイントが解放されました。<split>直ちに向かってください",true);
                break;
            case ZoneType.LightContainment:
                SetDoorState(ZoneType.LightContainment, false);
                Exiled.API.Features.Cassie.MessageTranslated("Opened Anti- Me mu contained Room. Please go there immediately. . . . .", "反ミーム爆弾部屋(Dクラス収容房)が解放されました。<split>直ちに向かい、起爆させてください！！！！！",true);
                break;
        }
    }

    private static void RoleAssign()
    {
        Timing.CallDelayed(1.5f, () =>
        {
            var candidates = Player.List.Where(p => p.IsSafePlayer()).Shuffle().ToList();

            var scp3125 = candidates[0];
            var ara = candidates[1];

            scp3125.SetRole(CRoleTypeId.Scp3125);
            ara.SetRole(CRoleTypeId.AraOrun);

            // 残りの候補から Marionette と AntiMemeDivisionScientist を 4:6 の割合で割り当て
            var remaining = candidates.Skip(2).ToList();
            var scientistRole = CRoleTypeId.AntiMemeDivisionScientist;
            var marionetteRole = CRoleTypeId.FifthistMarionette;
            var marionetteChance = 0.2f; // 40% で Marionette、60% で Scientist

            foreach (var p in remaining)
            {
                // ランダムで 40% の場合 Marionette、60% の場合 Scientist
                var roleToAssign = UnityEngine.Random.value < marionetteChance ? marionetteRole : scientistRole;
    
                p.SetRole(roleToAssign);
            }
            
            Exiled.API.Features.Cassie.MessageTranslated("Attention, All personnel. Were detected 5 5 5 Mega Forces. Please detonate anti- me mu process in light containment zone. . . . . . . .",
                $"全職員に通達。<color={CTeam.Fifthists.GetTeamColor()}>大勢の反ミーム勢力</color>を検知しました。<split>直ちに<color=yellow>各階の発電機を起動</color>して下層に向かい、<color={ServerColors.Green}>反ミーム爆弾</color>を起爆してください。",
                true);
        });
    }

    private IEnumerator<float> Coroutine()
    {
        yield return Timing.WaitForSeconds(5f);
        while (true)
        {
            if (IsCanceled() || Player.List.Where(p => p.GetCustomRole() is CRoleTypeId.Scp3125).ToList().Count <= 0) yield break;
            Player.List.Where(p => p?.Role.Type is RoleTypeId.Spectator && p.GetCustomRole() is CRoleTypeId.None).ToList().ForEach(p =>
            {
                p.SetRole(CRoleTypeId.FifthistMarionette);
            });
            yield return Timing.WaitForSeconds(20f);
        }
    }
}