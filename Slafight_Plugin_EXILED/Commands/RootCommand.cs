using System;
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
    public override string Description => "Slafight Plugin root command.";
    public override void LoadGeneratedCommands()
    {
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
        RegisterCommand(new SpawnWave());
        RegisterCommand(new PlaySurfaceAttack());
        RegisterCommand(new PlayOmegaWarhead());
        RegisterCommand(new ActivateGenerator());
    }

    protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        response = "\nPlease enter a valid subcommand:";

        foreach (ICommand command in AllCommands)
        {
            if (sender.CheckPermission($"slperm.{command.Command}"))
            {
                response += $"\n{command.Command} ({string.Join(", ", command.Aliases)})\n    - {command.Description}";
            }
        }

        return false;
    }
}