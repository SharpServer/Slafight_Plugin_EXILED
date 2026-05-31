using System.Collections.Generic;
using System.Text;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using HintServiceMeow.Core.Enum;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;

public class ScpStatusHints : IBootstrapHandler
{
    public static void Register()
    {
        
    }

    public static void Unregister()
    {
        
    }
    
    public static List<Hint> Hints = [];
    
    private static List<Hint> GetStatusHints(Player targetPlayer)
    {
        List<Hint> result = [];
        var sb = new StringBuilder();
        foreach (var player in Player.List)
        {
            sb.Clear();
            if (player?.GetTeam() is not CTeam.SCPs) continue;
            CRole.TryGet(player.GetCustomRole(), out var cRole);
            var isScp079 = player.IsVanillaOrCustom(RoleTypeId.Scp079, CRoleTypeId.Scp079);
            var scp079Role = player.Role as Scp079Role;
            if (player.GetCustomRole() is not CRoleTypeId.None)
            {
                sb.Append($"<color={CTeam.SCPs.GetTeamColor()}>")
                    .Append(cRole.RoleDisplayName.RemoveUnityRichTextTag())
                    .Append("</color> ");
            }
            else
            {
                sb.Append($"<color={CTeam.SCPs.GetTeamColor()}>")
                    .Append(player.Role.Name.RemoveUnityRichTextTag())
                    .Append("</color> ");
            }

            if (isScp079 && scp079Role != null)
            {
                var playerEnergyPercentage = scp079Role.Energy / scp079Role.MaxEnergy;
                var energyColor = StaticUtils.ToGradientColor(playerEnergyPercentage);
                sb.Append($"[ENERGY: <color={energyColor}>{scp079Role.Energy}</color>/{scp079Role.MaxEnergy}] (LEVEL: {scp079Role.Level})");
            }
            else
            {
                sb.Append($"[");
                var playerHealthPercentage = player.Health / player.MaxHealth;
                var healthColor = StaticUtils.ToGradientColor(playerHealthPercentage);
                sb.Append($"<color={healthColor}>").Append(player.Health).Append("</color>/").Append(player.MaxHealth).Append(" HP")
                    .Append("] ");
            
                sb.Append($"(");
                var playerHsPercentage = player.HumeShield / player.MaxHumeShield;
                var hsColor = StaticUtils.ToGradientColor(playerHsPercentage);
                sb.Append($"<color={hsColor}>").Append(player.HumeShield).Append("</color>/").Append(player.MaxHumeShield).Append(" HS")
                    .Append(") ");
            }
            if (player != targetPlayer)
            {
                sb.Append("距離: ");
                int Distance = 0;
                if (isScp079 && scp079Role != null)
                {
                    Distance = (int)Vector3.Distance(targetPlayer.Position, scp079Role.CameraPosition);
                }
                else
                {
                    Distance = (int)Vector3.Distance(targetPlayer.Position, player.Position);
                }

                sb.Append($"{Distance}m");
            }
            
            result.Add(new Hint()
            {
                Text = sb.ToString(),
                FontSize = 18,
                Alignment = HintAlignment.Right,
                ResolutionBasedAlign = true
            });
        }

        result.Add(new Hint()
        {
            Text = "発電機の状態：",
            FontSize = 18,
            Alignment = HintAlignment.Right,
            ResolutionBasedAlign = true
        });
        foreach (var generator in Generator.List)
        {
            
        }
        return result;
    }
}