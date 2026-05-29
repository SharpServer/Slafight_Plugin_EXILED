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
        RegisterCommand(new RestartCommand());
        RegisterCommand(new QueueCommand());
        RegisterCommand(new PlayerCommand());
        RegisterCommand(new DebugStart());
        RegisterCommand(new SpawnDebugToolRole());
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
        RegisterCommand(new ProximityChatCommand());
        RegisterCommand(new VoiceRecordingCommand());
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
        sb.AppendLine(CommandTools.BuildRichHeader("Slafight Commands", "sl <command> [args]"));
        sb.AppendLine("<size=18><color=#9fb0c3>Quick: <color=#7bdcff>sl list roles scp</color> / <color=#7bdcff>sl player info @me</color> / <color=#7bdcff>sl queue list</color> / <color=#7bdcff>sl hitbox look on</color></color></size>");
        sb.AppendLine("<size=18><line-height=92%>");
        sb.AppendLine(CommandTools.BuildCommandCatalog(AllCommands, sender));
        sb.AppendLine("</line-height></size>");

        response = sb.ToString().TrimEnd();
        return false;
    }
}
