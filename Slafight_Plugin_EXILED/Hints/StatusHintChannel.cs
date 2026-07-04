#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using HintServiceMeow.Core.Enum;
using PlayerRoles;

namespace Slafight_Plugin_EXILED.Hints;

public sealed class StatusHintLayout
{
    public HintAlignment Alignment { get; set; } = HintAlignment.Right;
    public HintSyncSpeed SyncSpeed { get; set; } = HintSyncSpeed.Fastest;
    public bool ResolutionBasedAlign { get; set; } = true;
    public float XCoordinate { get; set; } = 0f;
    public float YCoordinate { get; set; } = 150f;
    public int FontSize { get; set; } = 24;

    public bool OffsetNonScp079 { get; set; } = true;
    public float NonScp079XOffset { get; set; } = 370f;
    public Func<Player, float>? XCoordinateResolver { get; set; }

    public float ResolveX(Player player)
    {
        if (XCoordinateResolver != null)
            return XCoordinateResolver(player);

        var x = XCoordinate;
        if (OffsetNonScp079 && player.Role.Type is not RoleTypeId.Scp079)
            x += NonScp079XOffset;

        return x;
    }
}

public sealed class StatusHintBuildContext
{
    public StatusHintBuildContext(
        StatusHintChannel channel,
        Player viewer,
        IReadOnlyList<Player> members,
        IReadOnlyList<Player> allPlayers)
    {
        Channel = channel;
        Viewer = viewer;
        Members = members;
        AllPlayers = allPlayers;
    }

    public StatusHintChannel Channel { get; }
    public Player Viewer { get; }
    public IReadOnlyList<Player> Members { get; }
    public IReadOnlyList<Player> AllPlayers { get; }
}

public sealed class StatusHintLineContext
{
    public StatusHintLineContext(StatusHintBuildContext group, Player subject)
    {
        Group = group;
        Subject = subject;
    }

    public StatusHintBuildContext Group { get; }
    public StatusHintChannel Channel => Group.Channel;
    public Player Viewer => Group.Viewer;
    public Player Subject { get; }
}

public sealed class StatusHintChannel
{
    public StatusHintChannel(string id, Func<Player, bool> includesMember)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Status hint channel id cannot be empty.", nameof(id));

        Id = id;
        IncludesMember = includesMember ?? throw new ArgumentNullException(nameof(includesMember));
        CanReceive = includesMember;
    }

    public string Id { get; }
    public string Title { get; set; } = string.Empty;
    public string Color { get; set; } = "white";
    public int Priority { get; set; } = 100;
    public int MaxVisibleMembers { get; set; } = 0;
    public bool IncludeNpcMembers { get; set; } = true;
    public bool ShowHeader { get; set; } = false;
    public bool ShowDistance { get; set; } = true;
    public bool IncludeGeneratorStatus { get; set; } = false;
    public bool HideWhenNoVisibleMembers { get; set; } = true;

    public StatusHintLayout Layout { get; set; } = new();

    public Func<Player, bool> IncludesMember { get; set; }
    public Func<Player, bool> CanReceive { get; set; }
    public Func<Player, Player, bool> CanViewerSeeMember { get; set; } = (_, _) => true;
    public Func<IEnumerable<Player>, IEnumerable<Player>> SortMembers { get; set; } =
        players => players.OrderBy(player => player.Id);

    public Func<StatusHintBuildContext, string>? HeaderBuilder { get; set; }
    public Func<StatusHintLineContext, string>? LineBuilder { get; set; }
    public Func<StatusHintBuildContext, string>? FooterBuilder { get; set; }
    public Func<StatusHintLineContext, string>? SubjectNameBuilder { get; set; }
    public Func<StatusHintLineContext, string>? SubjectColorBuilder { get; set; }
}
