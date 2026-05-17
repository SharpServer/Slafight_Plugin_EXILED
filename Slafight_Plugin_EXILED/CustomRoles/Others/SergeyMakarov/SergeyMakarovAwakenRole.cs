using System;
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
using EventHandler = Slafight_Plugin_EXILED.MainHandlers.EventHandler;

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

    private readonly Action<string, string, Vector3, bool, Transform, bool, float, float> CreateAndPlayAudio
        = EventHandler.CreateAndPlayAudio;

    public override void SpawnRole(Player? player,RoleSpawnFlags roleSpawnFlags = RoleSpawnFlags.All)
    {
        base.SpawnRole(player, roleSpawnFlags);
        player!.Role.Set(RoleTypeId.Scp0492);
        player.UniqueRole = UniqueRoleKey;
        player.MaxHealth = 5000;
        player.Health = player.MaxHealth;
        player.ClearInventory();
        player.DisableAllEffects();
        var pos = Door.Get(DoorType.Scp106Primary).Position + Vector3.up * 0.15f;
        player.Position = pos;
            
        player.SetCustomInfo("SPIRIT OF CURSEMASTER");
        player.AddAbility(new CreateSinkholeAbility(player));
        player.AddAbility(new MagicMissileAbility(player));
        player.AddAbility(new SoundOfFifthAbility(player));
        Timing.CallDelayed(0.05f, () =>
        {
            RPNameSetter.SetForcedCustomName(player, $"セルゲイ・マカロフ ({player.Nickname})");
        });
    }

    protected override void OnDying(DyingEventArgs ev)
    {
        CreateAndPlayAudio("FemurBreaker.ogg", "SergeyVoice", Vector3.zero, true, null, false, 999999999, 0);
        Timing.CallDelayed(3,
            () => Exiled.API.Features.Cassie.MessageTranslated("Anomaly It is successfully terminated.",
                "「霊的実体「セルゲイ・マカロフ」は終了されました」", true));
        base.OnDying(ev);
    }
}
