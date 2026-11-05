using System;
using System.Text.RegularExpressions;
using CustomPlayerEffects;
using Exiled.API.Enums;
using Exiled.API.Features;
using MEC;
using PlayerRoles;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 完全に隠した装着者の代わりに名札を見せる Internal NPC です。透明で当たり判定が無く、本人からは見えません。
/// </summary>
internal sealed class WearNameplate
{
    private const byte FullFade = byte.MaxValue;
    private const float FollowInterval = 0.05f;
    private const float FollowThreshold = 0.01f;
    private const int MaxInfoLength = 400;

    /// <summary>
    /// CustomInfo の検証を通らない文字です。1 文字でも残ると名札全体が拒否されます。
    /// </summary>
    private static readonly Regex UnsupportedBadgeText = new Regex(@"[^\p{L}\p{P}\p{Sc}\p{N} ^=+|~`]", RegexOptions.Compiled);

    private readonly Player owner;
    private readonly Npc npc;

    private CoroutineHandle followLoop;
    private string lastInfo;
    private bool ready;
    private bool disposed;

    internal WearNameplate(Player owner)
    {
        this.owner = owner;

        npc = Npc.Spawn(owner.Nickname, RoleTypeId.None, true);
        InternalNpcRegistry.Register(npc, InternalNpcCategory.Generic);

        // 役職は透明化と同じ瞬間に与える。Spawn に渡すと素の体が一瞬見える。
        RoundScope.Current.Delay(Npc.SpawnSetRoleDelay + 0.1f, Setup);
    }

    /// <summary>
    /// NPC がまだ使えるかどうか。
    /// </summary>
    internal bool IsAlive => !disposed && npc.IsConnected;

    /// <summary>
    /// 名札の NPC です。
    /// </summary>
    internal Npc Npc => npc;

    /// <summary>
    /// 役職・透明化・当たり判定・名札の内容を装着者に揃えます。
    /// </summary>
    internal void Sync()
    {
        if (!ready || !IsAlive) return;

        if (npc.Role.Type != owner.Role.Type)
            npc.Role.Set(owner.Role.Type, SpawnReason.ForceClass, RoleSpawnFlags.None);

        if (npc.GetEffect<Fade>().Intensity != FullFade)
            npc.EnableEffect<Fade>(FullFade);

        if (!npc.IsGodModeEnabled) npc.IsGodModeEnabled = true;
        if (!npc.IsNoclipEnabled) npc.IsNoclipEnabled = true;
        if (npc.IsSpectatable) npc.IsSpectatable = false;

        PlayerVisibilitySyncProvider.TrySetHiddenFor(npc, owner, true);
        WearManager.SetBodyHitboxes(npc, false);

        Follow();
        CopyInfo();
    }

    internal void Dispose()
    {
        if (disposed) return;

        disposed = true;
        Timing.KillCoroutines(followLoop);
        InternalNpcRegistry.Unregister(npc.Id);

        if (npc.IsConnected)
            npc.Destroy();
    }

    private static string BuildBadgeLine(Player player)
    {
        if (player.BadgeHidden || string.IsNullOrWhiteSpace(player.RankName)) return string.Empty;

        string text = UnsupportedBadgeText.Replace(player.RankName.Replace('[', '|').Replace(']', '|'), string.Empty).Trim();

        return text.Length == 0 ? string.Empty : $"<color={BadgeColor(player.RankColor)}>{text}</color>\n";
    }

    /// <summary>
    /// CustomInfo の色は決められた 16 進値しか通らないので、バッジの色名から引き直します。
    /// </summary>
    private static string BadgeColor(string colorName) =>
        Misc.AllowedColors[
            Enum.TryParse(colorName?.Replace("_", string.Empty), true, out Misc.PlayerInfoColorTypes color)
                ? color
                : Misc.PlayerInfoColorTypes.White];

    private void Setup()
    {
        if (!IsAlive) return;

        ready = true;
        Sync();
        followLoop = RoundScope.Current.RunLoop(FollowInterval, Follow);
    }

    private void Follow()
    {
        if (!IsAlive || owner.ReferenceHub == null) return;

        Vector3 position = owner.Position;

        if ((npc.Position - position).sqrMagnitude > FollowThreshold * FollowThreshold)
            npc.Position = position;
    }

    private void CopyInfo()
    {
        npc.CustomName = owner.CustomName;

        string badge = BuildBadgeLine(owner);
        string info = badge + owner.CustomInfo;

        // 長すぎるならバッジより本来の名札を優先する。
        if (info.Length > MaxInfoLength)
            info = owner.CustomInfo;

        if (info != lastInfo)
        {
            lastInfo = info;
            npc.CustomInfo = info;
        }

        npc.InfoArea = badge.Length > 0 ? owner.InfoArea | PlayerInfoArea.CustomInfo : owner.InfoArea;
    }
}
