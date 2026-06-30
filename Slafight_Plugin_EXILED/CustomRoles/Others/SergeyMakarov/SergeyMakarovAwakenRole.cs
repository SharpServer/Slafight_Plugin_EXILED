using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Features.Doors;
using Exiled.Events.EventArgs.Player;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.Abilities;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.MainHandlers;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomRoles.Others.SergeyMakarov;

public class SergeyMakarovAwakenRole : CRole
{
    protected override string RoleName { get; set; } = "<color=#c50000>呪詛 - セルゲイ・マカロフ</color>";
    protected override string Description { get; set; } = "<size=25>" +
                                                          "怨念に呑まれ、全てを排除せんと暴れ狂う嘗ての管理官。\n" +
                                                          "アビリティ「怨みの沼, 呪詛, 管理官の祟り」が使用可能だ。\n" +
                                                          "<color=red><b>邪魔者を滅ぼし、サイト-02から毒を浄化せよ。</b></color>";
    protected override CRoleTypeId CRoleTypeId { get; set; } = CRoleTypeId.SergeyMakarovAwaken;
    protected override CTeam Team { get; set; } = CTeam.Others;
    protected override string UniqueRoleKey { get; set; } = "TheSergeyHimSelfAwaken";
    protected override RoleTypeId? SpawnBaseRole => RoleTypeId.Scp0492;
    protected override float? SpawnMaxHealth => 5000f;
    protected override bool SpawnClearsInventory => true;
    protected override string SpawnCustomInfo => "SPIRIT OF CURSEMASTER";

    protected override void OnRoleSpawned(Player player, RoleSpawnFlags roleSpawnFlags)
    {
        player.DisableAllEffects();
        player.Scale = Vector3.one;
        var pos = Door.Get(DoorType.Scp106Primary).Position + Vector3.up * 0.25f;
        TrySetPlayerPosition(player, pos, nameof(SergeyMakarovAwakenRole));

        player.AddAbility<CreateSinkholeAbility>();
        player.AddAbility<MagicMissileAbility>();
        player.AddAbility<SoundOfFifthAbility>();

        var playerId = player.Id;
        Timing.CallDelayed(RoleSpawnTimings.AfterRoleSet, () =>
        {
            var current = Player.Get(playerId);
            if (!Check(current) || !IsSafeRolePlayer(current))
                return;

            RPNameSetter.SetForcedCustomName(current, $"セルゲイ・マカロフ ({current.Nickname})");
            var bossbar = new BossBar()
            {
                Title = "呪詛 セルゲイ・マカロフ",
                TitleColor = "#c50000",
                Subtitle = "SITE-02に巣食う怨霊",
                BarColor = Color.red.ToHex(),
                MaxValue = SpawnMaxHealth ?? current.MaxHealth,
            }.TrackPlayer(current, show: true);
        });
    }

    protected override void OnRoleDying(DyingEventArgs ev)
    {
        SpeakerApi.Play("FemurBreaker.ogg", "SergeyVoice", Vector3.zero, true, null, false, 999999999, 0);
        Timing.CallDelayed(3,
            () => Exiled.API.Features.Cassie.MessageTranslated("Anomaly It is successfully terminated.",
                "「霊的実体「セルゲイ・マカロフ」は終了されました」", true));
        base.OnRoleDying(ev);
    }
}
