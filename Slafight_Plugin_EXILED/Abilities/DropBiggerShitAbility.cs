using System;
using System.Collections.Generic;
using Exiled.API.Features;
using Exiled.API.Features.Hazards;
using MEC;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.Abilities;

public class DropBiggerShitAbility : AbilityBase
{
    // AbilityBase の抽象プロパティを実装
    protected override float DefaultCooldown => 120f;
    protected override int DefaultMaxUses => -1;

    // 完全デフォルト
    public DropBiggerShitAbility(Player owner)
        : base(owner) { }

    // コマンドなどから上書きしたいとき用
    public DropBiggerShitAbility(Player owner, float cooldownSeconds)
        : base(owner, cooldownSeconds) { }

    public DropBiggerShitAbility(Player owner, float cooldownSeconds, int maxUses)
        : base(owner, cooldownSeconds, maxUses) { }

    protected override void ExecuteAbility(Player player)
    {
        try
        {
            Timing.RunCoroutine(Coroutine(player));
        }
        catch (Exception ex)
        {
            Log.Error($"Bigger Shit失敗: {ex.Message}");
        }
    }

    private static IEnumerator<float> Coroutine(Player? player)
    {
        for (var i = 0; i < 8; i++)
        {
            if (player is null || Round.IsLobby || !player.IsAlive) yield break;
            TantrumHazard.PlaceTantrum(player.Position);
            yield return Timing.WaitForSeconds(0.55f);
        }
    }
}