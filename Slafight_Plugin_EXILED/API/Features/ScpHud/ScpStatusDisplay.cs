using System;
using System.Linq;
using System.Text;
using Exiled.API.Enums;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp079;
using MEC;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp079.Pinging;
using RueI.API;
using RueI.API.Elements;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Interface;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using PlayerHandlers = Exiled.Events.Handlers.Player;
using Scp079Handlers = Exiled.Events.Handlers.Scp079;
using ServerHandlers = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.API.Features.ScpHud;

public sealed class ScpStatusDisplay : IBootstrapHandler, IDisposable
{
    private static readonly Tag ScpHudTag = new("SCPHUD");

    public static DynamicElement ScpElement { get; } = new(900, GetScpContent)
    {
        UpdateInterval = TimeSpan.FromTicks(500)
    };

    public static ScpStatusDisplay? Instance { get; private set; }

    private bool _disposed;

    public static void Register()
    {
        Unregister();
        Instance = new ScpStatusDisplay();
    }

    public static void Unregister()
    {
        Instance?.Dispose();
        Instance = null;
    }

    private ScpStatusDisplay()
    {
        PlayerHandlers.Verified += OnVerified;
        PlayerHandlers.ChangingRole += OnChangingRole;
        PlayerHandlers.Spawned += OnSpawned;
        PlayerHandlers.Died += OnDied;
        ServerHandlers.RoundStarted += OnRoundStarted;
        ServerHandlers.RestartingRound += OnRestartingRound;
        Scp079Handlers.Pinging += OnPinging;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        PlayerHandlers.Verified -= OnVerified;
        PlayerHandlers.ChangingRole -= OnChangingRole;
        PlayerHandlers.Spawned -= OnSpawned;
        PlayerHandlers.Died -= OnDied;
        ServerHandlers.RoundStarted -= OnRoundStarted;
        ServerHandlers.RestartingRound -= OnRestartingRound;
        Scp079Handlers.Pinging -= OnPinging;

        foreach (var player in Player.List)
            RemoveElement(player);

        GC.SuppressFinalize(this);
    }

    private static void OnVerified(VerifiedEventArgs ev)
    {
        if (ev?.Player == null)
            return;

        ApplyForPlayer(ev.Player);
    }

    private static void OnChangingRole(ChangingRoleEventArgs ev)
    {
        if (ev?.Player == null)
            return;

        Timing.CallDelayed(0.1f, () => ApplyForPlayer(ev.Player));
    }

    private static void OnSpawned(SpawnedEventArgs ev)
    {
        if (ev?.Player == null)
            return;

        Timing.CallDelayed(0.1f, () => ApplyForPlayer(ev.Player));
    }

    private static void OnDied(DiedEventArgs ev)
    {
        if (ev?.Player == null)
            return;

        Timing.CallDelayed(0.2f, () => ApplyForPlayer(ev.Player));
    }

    private static void OnRoundStarted()
    {
        Timing.CallDelayed(0.5f, () =>
        {
            foreach (var player in Player.List)
                ApplyForPlayer(player);
        });
    }

    private static void OnRestartingRound()
    {
        foreach (var player in Player.List)
            RemoveElement(player);
    }

    private static void ApplyForPlayer(Player player)
    {
        if (!IsValid(player))
            return;

        RemoveElement(player);

        if (!ShouldSeeScpHud(player))
            return;

        try
        {
            RueDisplay.Get(player.ReferenceHub).Show(ScpHudTag, ScpElement);
        }
        catch (Exception ex)
        {
            Log.Debug($"[ScpStatusDisplay] show failed for {player.Nickname}: {ex.Message}");
        }
    }

    private static void RemoveElement(Player? player)
    {
        if (!IsValid(player))
            return;

        try
        {
            RueDisplay.Get(player!.ReferenceHub).Remove(ScpHudTag);
        }
        catch
        {
            // Display may not exist yet, or the tag may already be removed.
        }
    }

    private static void OnPinging(PingingEventArgs ev)
    {
        if (ev?.Room == null)
            return;

        var targets = Player.List.Where(player => player.Role.Side is Side.Scp);
        string room = PingTranslate.TranslateRoomName(ev.Room.Type);
        string zone = PingTranslate.TranslateZoneName(ev.Room.Zone);
        string message = ev.Type switch
        {
            PingType.Generator => $"<color=red><size=80%>SCP079が発電機にピンを差した。場所：{zone}の{room}</size></color>",
            PingType.Projectile => $"<color=red><size=80%>SCP079が爆発物にピンを差した。場所：{zone}の{room}</size></color>",
            PingType.MicroHid => $"<color=red><size=80%>SCP079がマイクロHIDを持った人間にピンを差した。場所：{zone}の{room}</size></color>",
            PingType.Human => $"<color=yellow><size=80%>SCP079が人間にピンを差した。場所：{zone}の{room}</size></color>",
            PingType.Elevator => $"<size=80%>SCP079がエレベーターにピンを差した。場所：{zone}の{room}</size>",
            PingType.Door => $"<size=80%>SCP079がドアにピンを差した。場所：{zone}の{room}</size>",
            _ => $"<size=80%>SCP079がピンを差した。場所：{zone}の{room}</size>"
        };

        foreach (var scp in targets)
            scp.ShowRueiPlus(message);
    }

    private static string GetScpContent(ReferenceHub core)
    {
        var viewer = Player.Get(core);
        if (!IsValid(viewer) || !ShouldSeeScpHud(viewer))
            return string.Empty;

        var builder = new StringBuilder()
            .Append("<align=right><size=30>");

        var scps = Player.List
            .Where(player => player != null && player.IsAlive && IsScpTeam(player))
            .ToList();

        foreach (var scp in scps.Where(player => player.Role.Type != RoleTypeId.Scp0492))
        {
            if (scp.Role.Type == RoleTypeId.Scp079)
                AppendScp079(builder, viewer, scp);
            else
                AppendScpHealth(builder, viewer, scp);
        }

        int zombies = scps.Count(player => player.Role.Type == RoleTypeId.Scp0492);
        if (zombies > 0)
        {
            builder.Append("<b><color=#ff3232>SCP049-2</color></b>")
                .Append("の数: ")
                .Append(zombies)
                .AppendLine();
        }

        AppendGeneratorStatus(builder);

        builder.Append("</size></align>");
        return builder.ToString();
    }

    private static void AppendScp079(StringBuilder builder, Player viewer, Player scp)
    {
        if (scp.Role is not Scp079Role scp079Role)
            return;

        builder.Append("<b><color=#ff3232>SCP-079 : </color></b>")
            .Append("レベル: ")
            .Append(scp079Role.Level)
            .Append("(電力: ")
            .Append((int)scp079Role.Energy)
            .Append('/')
            .Append((int)scp079Role.MaxEnergy)
            .Append(')');

        if (viewer.Role.Type != RoleTypeId.Scp079 && scp079Role.Camera != null)
            builder.Append("[距離: ")
                .Append((int)Vector3.Distance(viewer.Position, scp079Role.Camera.Position))
                .Append("m]");

        builder.AppendLine();
    }

    private static void AppendScpHealth(StringBuilder builder, Player viewer, Player scp)
    {
        string scpName = GetScpName(scp);
        int health = Mathf.CeilToInt(scp.Health);
        int maxHealth = Math.Max(1, Mathf.CeilToInt(scp.MaxHealth));
        int humeShield = Mathf.CeilToInt(scp.HumeShield);
        int maxHumeShield = Math.Max(0, Mathf.CeilToInt(scp.MaxHumeShield));
        float healthPercentage = Mathf.Clamp01((float)health / maxHealth);
        float humeShieldPercentage = maxHumeShield > 0 ? Mathf.Clamp01((float)humeShield / maxHumeShield) : 0f;

        string healthColor = Rgb(255 * (1f - healthPercentage), 255 * healthPercentage, 0);
        string humeShieldColor = Rgb(0, 255 * humeShieldPercentage, 255 * (1f - humeShieldPercentage));

        builder.Append("<b><color=#ff3232>")
            .Append(scpName)
            .Append(" : </color></b><color=")
            .Append(healthColor)
            .Append('>')
            .Append(health)
            .Append('/')
            .Append(maxHealth)
            .Append(" HP</color>");

        if (maxHumeShield > 0)
        {
            builder.Append(" (<color=")
                .Append(humeShieldColor)
                .Append('>')
                .Append(humeShield)
                .Append('/')
                .Append(maxHumeShield)
                .Append(" HS</color>)");
        }

        AppendDistance(builder, viewer, scp);
        builder.AppendLine();
    }

    private static void AppendDistance(StringBuilder builder, Player viewer, Player target)
    {
        if (viewer == target)
            return;

        Vector3 from = viewer.Position;
        if (viewer.Role is Scp079Role scp079Role && scp079Role.Camera != null)
            from = scp079Role.Camera.Position;

        builder.Append("[距離:")
            .Append((int)Vector3.Distance(target.Position, from))
            .Append("m]");
    }

    private static void AppendGeneratorStatus(StringBuilder builder)
    {
        builder.AppendLine("発電機の状態");

        foreach (var generator in Generator.List)
        {
            float activationTime = Math.Max(1f, generator.ActivationTime);
            float progress = Mathf.Clamp01(1f - generator.CurrentTime / activationTime);
            string statusColor;
            string statusText;

            if (generator.State == GeneratorState.Engaged || progress >= 1f)
            {
                statusColor = "#ff0000";
                statusText = "起動済み";
            }
            else if (progress <= 0f)
            {
                statusColor = "#ffffff";
                statusText = "未起動";
            }
            else if (progress < 0.5f)
            {
                statusColor = "#ffff00";
                statusText = $"進行度: {progress:P0} (起動まで{generator.CurrentTime:0}秒)";
            }
            else if (progress < 0.8f)
            {
                statusColor = "#ffa500";
                statusText = $"進行度: {progress:P0} (起動まで{generator.CurrentTime:0}秒)";
            }
            else
            {
                statusColor = "#ff0000";
                statusText = $"進行度: {progress:P0} (起動まで{generator.CurrentTime:0}秒)";
            }

            builder.Append("<color=")
                .Append(statusColor)
                .Append("><b>")
                .Append(PingTranslate.TranslateRoomName(generator.Room.Type))
                .Append(": </b>")
                .Append(statusText)
                .AppendLine("</color>");
        }
    }

    private static string GetScpName(Player player)
    {
        if (CRole.TryGetByUniqueRole(player.UniqueRole, out var customRole))
            return customRole.RoleDisplayName;

        return player.Role.Type switch
        {
            RoleTypeId.Scp173 => "SCP-173",
            RoleTypeId.Scp106 => "SCP-106",
            RoleTypeId.Scp096 => "SCP-096",
            RoleTypeId.Scp049 => "SCP-049",
            RoleTypeId.Scp939 => "SCP-939",
            RoleTypeId.Scp3114 => "SCP-3114",
            RoleTypeId.Scp079 => "SCP-079",
            _ => player.Role.Name ?? "???"
        };
    }

    private static bool ShouldSeeScpHud(Player? player)
        => IsValid(player) && player!.IsAlive && IsScpTeam(player);

    private static bool IsScpTeam(Player player)
        => player.GetTeam() == CTeam.SCPs || player.Role.Team == Team.SCPs;

    private static bool IsValid(Player? player)
    {
        try
        {
            return player != null &&
                   player.IsConnected &&
                   !player.IsHost &&
                   !player.IsNPC &&
                   player.ReferenceHub != null &&
                   player.ReferenceHub.connectionToClient != null;
        }
        catch
        {
            return false;
        }
    }

    private static string Rgb(float red, float green, float blue)
        => $"#{Mathf.Clamp(Mathf.RoundToInt(red), 0, 255):X2}{Mathf.Clamp(Mathf.RoundToInt(green), 0, 255):X2}{Mathf.Clamp(Mathf.RoundToInt(blue), 0, 255):X2}";
}
