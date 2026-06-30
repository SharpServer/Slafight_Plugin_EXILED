using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using MEC;
using Slafight_Plugin_EXILED.API.Enums;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.CustomRoles.SCPs;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomItems.SlafightApiItems;

public class PandraBreaker : CItem
{
    public override string DisplayName => "Pandra Breaker - アベル爆破スイッチ";
    public override string Description => "SCP-076を爆破する。\n投げて使用可能";
    protected override string UniqueKey => "PandraBreaker";
    protected override ItemType BaseItem => ItemType.Radio;
    protected override bool  PickupLightEnabled => true;
    protected override Color PickupLightColor => Color.red;
    private static readonly HashSet<ushort> CooldownItemSerials = [];

    protected override void OnWaitingForPlayers()
    {
        CooldownItemSerials.Clear();
        base.OnWaitingForPlayers();
    }

    protected override void OnDropping(DroppingItemEventArgs ev)
    {
        if (!ev.IsThrown) return;

        ev.IsAllowed = false;
        var serial = ev.Item.Serial;
        if (CooldownItemSerials.Contains(serial))
        {
            ev.Player?.ShowHint("<size=23>現在クールダウン中です！使用してから一分後に再利用できるようになります。</size>");
            return;
        }

        var activated = false;
        foreach (var player in Player.List)
        {
            if (player?.GetCustomRole() is not CRoleTypeId.Scp076) continue;
            if (Scp076Role.IsResistanceState(player))
            {
                activated = true;
                player.Explode();
                if (player.IsAlive)
                {
                    player.Kill("抑制装置により爆発された");
                }
            }
        }

        if (!activated)
        {
            ev.Player?.ShowHint("<size=23>反逆状態のSCP-076が存在しません。</size>", 5f);
            return;
        }

        Timing.RunCoroutine(CooldownCoroutine(serial));
        base.OnDropping(ev);
    }

    private IEnumerator<float> CooldownCoroutine(ushort serial)
    {
        CooldownItemSerials.Add(serial);
        float elapsedTime = 0;
        while (elapsedTime < 60f)
        {
            if (Round.IsLobby)
            {
                CooldownItemSerials.Remove(serial);
                yield break;
            }

            elapsedTime++;
            yield return Timing.WaitForSeconds(1f);
        }

        CooldownItemSerials.Remove(serial);
    }
}
