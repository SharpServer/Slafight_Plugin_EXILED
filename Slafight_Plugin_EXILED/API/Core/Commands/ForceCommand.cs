using System;
using System.Linq;
using System.Text;
using Slafight_Plugin_EXILED.API.Core.Features;
using Slafight_Plugin_EXILED.ForceSystem;

namespace Slafight_Plugin_EXILED.API.Core.Commands;

/// <summary>
/// いま存在する隊と、その隊員の階級・貢献度を表示します。
/// </summary>
public sealed class ForceCommand : CommandBase
{
    public override Type Parent => typeof(RootCommand);

    public override string Command => "force";

    public override string Usage => "force [隊名]";

    public override string Description => "部隊システムの状態を確認します。";

    protected override bool OnExecute(out string response)
    {
        if (ForceRegistry.All.Count == 0)
        {
            response = "いま存在する隊はありません。";

            return true;
        }

        if (TryGetArgument(0, out string name))
            return Detail(name, out response);

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"隊: {ForceRegistry.All.Count}");

        foreach (ForceBase force in ForceRegistry.All.OrderByDescending(force => force.IsMainForce))
        {
            builder.AppendLine(
                $"  [{force.Faction}] {force.KindName} {force.Name} " +
                $"({force.AliveCount}名 / 貢献 {force.TotalContribution}" +
                $"{(force.UnitId is { } id ? $" / UnitId {id}" : string.Empty)}" +
                $"{(force.Parent is { } parent ? $" / 親 {parent.Name}" : string.Empty)})");
        }

        response = builder.ToString().TrimEnd();

        return true;
    }

    private static bool Detail(string name, out string response)
    {
        ForceBase force = ForceRegistry.All
            .FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));

        if (force is null)
        {
            response = $"'{name}' という隊はありません。";

            return false;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"{force.KindName} {force.Name} ({force.Faction})");
        builder.AppendLine($"  合計貢献: {force.TotalContribution}");
        builder.AppendLine($"  移動速度: {force.MovementBoost()} / 攻撃力: {force.DamageBoost()}");

        foreach (ForceMember member in force.Members.OrderByDescending(member => member.Contribution))
        {
            builder.AppendLine(
                $"  - {member.Player.Nickname}: {force.RankNameOf(member.Level)} " +
                $"/ 貢献 {member.Contribution} ({ForceContribution.ShareOf(member):P0}) " +
                $"/ 所属 {member.MembershipSeconds:F0}秒");
        }

        response = builder.ToString().TrimEnd();

        return true;
    }
}
