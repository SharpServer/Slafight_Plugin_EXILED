using System.Collections.Generic;
using System.Text;
using CustomPlayerEffects;
using Exiled.API.Extensions;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using HintServiceMeow.Core.Enum;
using HintServiceMeow.Core.Extension;
using MEC;
using PlayerStatsSystem;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomMaps.Entities;
using Slafight_Plugin_EXILED.CustomMaps.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Hint = HintServiceMeow.Core.Models.Hints.Hint;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class LsdPill : CItemUsable
{
    private const float Duration = 60f;
    private const float RefreshInterval = 0.5f;
    private const int TextCountPerRefresh = 10;
    private const int LineLength = 50;

    private static readonly Dictionary<int, CoroutineHandle> TextCoroutines = new();
    private static readonly Dictionary<int, int> Sessions = new();

    public override string DisplayName => "L-SD2剤";

    public override string Description =>
        "「これはなあに？」";

    protected override string UniqueKey => "LsdPill";
    protected override ItemType BaseItem => ItemType.Adrenaline;
    protected override bool PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.gray;

    protected override void OnUsedEffect(UsingItemCompletedEventArgs ev)
    {
        Player player = ev.Player;
        if (player is null)
        {
            base.OnUsedEffect(ev);
            return;
        }

        int session = CreateNewSession(player);

        StopTextCoroutine(player, removeHint: true);

        ApplyEffects(player);
        Scp513.AddTarget(player);

        TextCoroutines[player.Id] = Timing.RunCoroutine(TextCoroutine(player, session));

        Timing.CallDelayed(Duration, () =>
        {
            EndSession(player, session);
        });

        base.OnUsedEffect(ev);
    }

    private static void ApplyEffects(Player player)
    {
        player.EnableEffect<Invigorated>(255, Duration);
        player.EnableEffect<Concussed>(255, Duration);
        player.EnableEffect<Blurred>(255, Duration);
        player.EnableEffect<AmnesiaVision>(255, Duration);
        player.EnableEffect<Asphyxiated>(5, Duration);
    }

    private static void RemoveEffects(Player player)
    {
        if (!IsValid(player))
            return;

        player.DisableEffect<Invigorated>();
        player.DisableEffect<Concussed>();
        player.DisableEffect<Blurred>();
        player.DisableEffect<AmnesiaVision>();
        player.DisableEffect<Asphyxiated>();
    }

    private static IEnumerator<float> TextCoroutine(Player player, int session)
    {
        if (!IsValid(player))
            yield break;

        var display = player.GetPlayerDisplay();
        if (display is null)
            yield break;

        string hintId = GetHintId(player);

        RemoveHint(player);

        Hint hint = new()
        {
            Id = hintId,
            Alignment = HintAlignment.Center,
            XCoordinate = 0,
            YCoordinate = 0,
            FontSize = 50,
            Text = "",
            SyncSpeed = HintSyncSpeed.Fastest
        };

        display.AddHint(hint);

        float elapsedTime = 0f;
        int tick = 0;
        StringBuilder sb = new();

        while (elapsedTime < Duration)
        {
            if (!IsValid(player))
                break;

            if (!IsCurrentSession(player, session))
                break;

            sb.Clear();

            for (int i = 0; i < TextCountPerRefresh; i++)
            {
                sb.Append(DocumentDictionary.Get(EnumUtils.GetRandom<DocumentType>()));
            }

            string text = StringUtils.InsertLineBreaks(sb.ToString(), LineLength);

            int colorSeed = unchecked((int)(Timing.LocalTime * 1000f) + tick * 31);
            hint.Text = StringUtils.ToRandomRichTextColors(text, colorSeed);

            tick++;
            elapsedTime += RefreshInterval;

            yield return Timing.WaitForSeconds(RefreshInterval);
        }

        if (IsCurrentSession(player, session))
            RemoveHint(player);

        if (TextCoroutines.ContainsKey(player.Id))
            TextCoroutines.Remove(player.Id);
    }

    private static void EndSession(Player player, int session)
    {
        if (!IsCurrentSession(player, session))
            return;

        StopTextCoroutine(player, removeHint: true);

        Scp513.RemoveTarget(player);

        RemoveEffects(player);

        Sessions.Remove(player.Id);
    }

    private static int CreateNewSession(Player player)
    {
        if (!Sessions.TryGetValue(player.Id, out int session))
            session = 0;

        session++;
        Sessions[player.Id] = session;

        return session;
    }

    private static bool IsCurrentSession(Player player, int session)
    {
        return player is not null
               && Sessions.TryGetValue(player.Id, out int currentSession)
               && currentSession == session;
    }

    private static void StopTextCoroutine(Player player, bool removeHint)
    {
        if (player is null)
            return;

        if (TextCoroutines.TryGetValue(player.Id, out CoroutineHandle handle))
        {
            Timing.KillCoroutines(handle);
            TextCoroutines.Remove(player.Id);
        }

        if (removeHint)
            RemoveHint(player);
    }

    private static void RemoveHint(Player player)
    {
        if (player is null)
            return;

        var display = player.GetPlayerDisplay();
        if (display is null)
            return;

        var existing = display.GetHint(GetHintId(player));
        if (existing is Hint hint)
            display.RemoveHint(hint);
    }

    private static string GetHintId(Player player)
    {
        return $"{player.NetId}_LsdPill";
    }

    private static bool IsValid(Player player)
    {
        return player is not null
               && !player.IsDead
               && !Round.IsLobby
               && !Round.IsEnded;
    }
}