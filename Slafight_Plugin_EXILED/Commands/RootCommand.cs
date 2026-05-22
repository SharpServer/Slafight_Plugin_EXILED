using System;
using System.Linq;
using System.Text;
using CommandSystem;
using Exiled.Permissions.Extensions;
using Slafight_Plugin_EXILED.Commands.DevTools;

namespace Slafight_Plugin_EXILED.Commands;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public class RootCommand : ParentCommand
{
    public RootCommand() => LoadGeneratedCommands();
    public override string Command => "slafight";
    public override string[] Aliases { get; } = ["sl"];
    public override string Description => "Slafight plugin administration root command.";
    public override void LoadGeneratedCommands()
    {
        RegisterCommand(new HelpCommand(() => AllCommands.ToArray()));
        RegisterCommand(new ListCommand());
        RegisterCommand(new StatusCommand());
        RegisterCommand(new QueueCommand());
        RegisterCommand(new PlayerCommand());
        RegisterCommand(new DebugStart());
        RegisterCommand(new SpawnDebugToolRole());
        RegisterCommand(new SpawnMapEditRole());
        RegisterCommand(new ReRollSpecial());
        RegisterCommand(new ReRollSetQueue());
        RegisterCommand(new GetQueue());
        RegisterCommand(new AddQueue());
        RegisterCommand(new SetQueue());
        RegisterCommand(new RunEvent());
        RegisterCommand(new SpawnUniversal());
        RegisterCommand(new AbilityUniversal());
        RegisterCommand(new GiveItem());
        RegisterCommand(new SpawnBuiltInPrefab());
        RegisterCommand(new SpawnObjectPrefab());
        RegisterCommand(new HitboxCommand());
        RegisterCommand(new SpawnWave());
        RegisterCommand(new PlaySurfaceAttack());
        RegisterCommand(new PlayInstantSurfaceBombing());
        RegisterCommand(new PlayAudioHere());
        RegisterCommand(new PlayOmegaWarhead());
        RegisterCommand(new ActivateGenerator());
    }

    protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Slafight Commands");
        sb.AppendLine("Usage: sl <command> [args]");
        sb.AppendLine();
        sb.AppendLine("Main:");
        sb.AppendLine("  help/list/status/player/queue");
        sb.AppendLine("  Examples:");
        sb.AppendLine("    sl list roles scp");
        sb.AppendLine("    sl player info @me");
        sb.AppendLine("    sl player role Scp173 5");
        sb.AppendLine("    sl player item KeycardSiteDirector @me");
        sb.AppendLine("    sl queue list");
        sb.AppendLine("    sl hitbox look on");
        sb.AppendLine("    sl hitbox clear");
        sb.AppendLine();
        sb.AppendLine("Available subcommands:");

        foreach (ICommand command in AllCommands.OrderBy(c => c.Command))
        {
            if (sender.CheckPermission($"slperm.{command.Command}"))
            {
                sb.AppendLine($"  {CommandTools.FormatCommand(command)}");
            }
        }

        response = sb.ToString().TrimEnd();
        return false;
    }
}
