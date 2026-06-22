using System.Linq;
using Exiled.API.Enums;
using Exiled.API.Features;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps;
using Slafight_Plugin_EXILED.Extensions;
using Slafight_Plugin_EXILED.ProximityChat;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

public class AllowEscapeAbility : AbilityBase
{
    // AbilityBase の抽象プロパティを実装
    protected override float DefaultCooldown => 999f;
    protected override int DefaultMaxUses => 1;

    // 完全デフォルト
    public AllowEscapeAbility(Player owner)
        : base(owner) { }

    // コマンドなどから上書きしたいとき用
    public AllowEscapeAbility(Player owner, float cooldownSeconds)
        : base(owner, cooldownSeconds) { }

    public AllowEscapeAbility(Player owner, float cooldownSeconds, int maxUses)
        : base(owner, cooldownSeconds, maxUses) { }

    protected override bool CanActivate(Player player, out string failureReason)
    {
        if (!base.CanActivate(player, out failureReason))
            return false;

        if (!PDEx.PDExPlayers.Any(target =>
                target?.ReferenceHub != null && target.IsConnected && target.IsAlive))
        {
            failureReason = "ポケットディメンション内に救出対象がいません。";
            return false;
        }

        return true;
    }

    protected override void ExecuteAbility(Player player)
    {
        foreach (var escapePlayer in PDEx.PDExPlayers.ToList())
        {
            if (escapePlayer == null) continue;
            escapePlayer.Position = Room.Random().WorldPosition(new Vector3(0f,0.25f,0f));
            escapePlayer.DisableEffect(EffectType.Slowness);
            escapePlayer.DisableEffect(EffectType.PocketCorroding);
            escapePlayer.EnableEffect(EffectType.Traumatized);
            PDEx.PDExPlayers.Remove(escapePlayer);
        }

        foreach (var kings in Player.List)
        {
            if (kings == null) continue;
            if (kings.GetCustomRole() == CRoleTypeId.Scp106 || (kings.GetCustomRole() == CRoleTypeId.None && kings.Role.Type == RoleTypeId.Scp106))
            {
                kings.RemoveAbility<AllowEscapeAbility>();
                Handler.SetProximityChatForced(kings, false);
                kings.Position = Room.Get(RoomType.Hcz106).WorldPosition(new Vector3(0f,0.25f,0f));
            }
        }
    }
}
