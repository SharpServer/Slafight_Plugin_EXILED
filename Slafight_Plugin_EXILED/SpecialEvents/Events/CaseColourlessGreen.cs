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
        return MapFlags.GetSeason() is SeasonTypeId.FifthFestival or SeasonTypeId.Summer;
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

            var scp3125 = candidates[0];
            var ara = candidates[1];

            scp3125.SetRole(CRoleTypeId.Scp3125);
            ara.SetRole(CRoleTypeId.AraOrun);

            // 残りの候補から Marionette と AntiMemeDivisionScientist を 4:6 の割合で割り当て
            var remaining = candidates.Skip(2).ToList();
            var scientistRole = CRoleTypeId.AntiMemeDivisionScientist;
            var marionetteRole = CRoleTypeId.FifthistMarionette;
            var marionetteChance = 0.4f; // 40% で Marionette、60% で Scientist

            for (int i = 0; i < remaining.Count; i++)
            {
                var p = remaining[i];
    
                // ランダムで 40% の場合 Marionette、60% の場合 Scientist
                var roleToAssign = UnityEngine.Random.value < marionetteChance ? marionetteRole : scientistRole;
    
                p.SetRole(roleToAssign);
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