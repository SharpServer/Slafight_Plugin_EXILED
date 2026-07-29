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

    /// <summary>
    /// <see cref="YCoordinate"/> がテキストブロックのどこを指すか。
    /// 既定の <see cref="HintVerticalAlign.Top"/> ではブロックが下方向にのみ伸びるため、
    /// 行数が増えても上方向へ食い込まない。
    /// </summary>
    public HintVerticalAlign VerticalAlign { get; set; } = HintVerticalAlign.Top;

    public HintSyncSpeed SyncSpeed { get; set; } = HintSyncSpeed.Fastest;
    public bool ResolutionBasedAlign { get; set; } = true;
    public float XCoordinate { get; set; } = 0f;

    /// <summary>
    /// 値が大きいほど画面下方向。<see cref="VerticalAlign"/> が Top のときはブロック上端の位置。
    /// </summary>
    public float YCoordinate { get; set; } = 100f;
    public int FontSize { get; set; } = 24;

    /// <summary>0 は HintServiceMeow の既定行送りを使う。</summary>
    public float LineHeight { get; set; } = 0f;

    /// <summary>
    /// 同一レイアウトの他チャンネルと 1 つの Hint に結合してよいか。
    /// false にすると単独の Hint として描画されるため、他チャンネルと重なる可能性がある。
    /// </summary>
    public bool AllowMerge { get; set; } = true;

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

    /// <summary>
    /// 同じ Hint に他チャンネルのブロックも並ぶ場合は true。
    /// ヘッダー生成時にしか確定しないため、テキスト構築中に参照してはならない。
    /// </summary>
    public bool IsSharedHint { get; internal set; }
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

    /// <summary>
    /// 他チャンネルと 1 つの Hint を共有するときだけタイトル行を出すか。
    /// 単独表示ではヘッダー無し、共有時のみ区切りとしてタイトルを出したい場合に使う。
    /// </summary>
    public bool ShowHeaderWhenShared { get; set; } = true;

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
