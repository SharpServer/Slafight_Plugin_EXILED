using System.Collections.Generic;
using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
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
        return MapFlags.GetSeason() is SeasonTypeId.FifthFestival;
    }

    // ===== ショートカット =====
    // ===== 実行本体 =====
    protected override void OnExecute(int eventPid)
    {
        Timing.KillCoroutines(_handle);
        _handle = Timing.RunCoroutine(Coroutine());
        SetupBomb();
        RoleAssign();
    }

    private static void SetupBomb()
    {
        new AntiMemeBomb(){Position = StaticUtils.GetWorldFromRoomLocal(RoomType.LczClassDSpawn, new Vector3(-25.32238f, 0f, 0f), Vector3.zero).worldPosition}.Create();
    }

    private static void RoleAssign()
    {
        Timing.CallDelayed(1.5f, () =>
        {
            var candidates = Player.List.Where(p => p is not null).Shuffle().ToList();

            var marion = candidates[0];
            var scp3125 = candidates[1];
            var ara = candidates[2];

            marion.SetRole(CRoleTypeId.MarionWheeler);
            scp3125.SetRole(CRoleTypeId.Scp3125);
            ara.SetRole(CRoleTypeId.AraOrun);

            foreach (var p in candidates.Skip(3))
            {
                p.SetRole(CRoleTypeId.FifthistMarionette, RoleSpawnFlags.AssignInventory);
                Timing.CallDelayed(1.05f, () => p.Position = Room.Get(RoomType.HczIncineratorWayside).Doors.First().Position + Vector3.up * 1.15f);
            }
        });
    }

    private IEnumerator<float> Coroutine()
    {
        yield return Timing.WaitForSeconds(5f);
        while (true)
        {
            if (CancelIfOutdated() || Player.List.Where(p => p.GetCustomRole() is CRoleTypeId.Scp3125).ToList().Count <= 0) yield break;
            Player.List.Where(p => p?.Role.Type is RoleTypeId.Spectator && p.GetCustomRole() is CRoleTypeId.None).ToList().ForEach(p =>
            {
                p.SetRole(CRoleTypeId.FifthistMarionette);
            });
            yield return Timing.WaitForSeconds(20f);
        }
    }
}