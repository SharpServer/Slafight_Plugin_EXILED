using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Item;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.Handlers;
using MEC;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;
using Player = Exiled.API.Features.Player;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class BattleAxe : CItem
{
    public override string DisplayName => "バトルアックス";
    public override string Description => "W.I.P";
    protected override string UniqueKey => "BattleAxe";
    protected override ItemType BaseItem => ItemType.Jailbird;
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.red;
    private static readonly HashSet<int> CooldownPlayerIds = [];

    protected override void OnWaitingForPlayers()
    {
        CooldownPlayerIds.Clear();
        base.OnWaitingForPlayers();
    }

    public override void RegisterEvents()
    {
        Item.Swinging += OnSwinging;
        base.RegisterEvents();
    }

    public override void UnregisterEvents()
    {
        Item.Swinging -= OnSwinging;
        base.UnregisterEvents();
    }

    private void OnSwinging(SwingingEventArgs ev)
    {
        if (!Check(ev.Item)) return;
        if (ev.Player is null) return;

        if (!CooldownPlayerIds.Add(ev.Player.Id))
        {
            ev.IsAllowed = false;
            return;
        }
        Timing.RunCoroutine(CooldownCoroutine(ev.Player.Id, 5f));
    }

    protected override void OnHurtingOthers(HurtingEventArgs ev)
    {
        ev.Amount = 200f;
        base.OnHurtingOthers(ev);
    }

    private IEnumerator<float> CooldownCoroutine(int playerId, float cooldown)
    {
        float elapsed = 0f;
        while (true)
        {
            if (elapsed >= cooldown)
            {
                CooldownPlayerIds.Remove(playerId);
                yield break;
            }

            var player = Player.Get(playerId);
            if (Round.IsLobby || player is null || player.ReferenceHub is null || !player.HasCItem<BattleAxe>())
            {
                CooldownPlayerIds.Remove(playerId);
                yield break;
            }

            elapsed++;
            yield return Timing.WaitForSeconds(1f);
        }
    }
}
