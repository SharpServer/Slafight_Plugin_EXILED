using System;
using Exiled.API.Features;
using Exiled.API.Features.Hazards;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.Extensions;
using UnityEngine;

namespace Slafight_Plugin_EXILED.Abilities;

public class PlaceTantrumAbility : AbilityBase
{
    // AbilityBase の抽象プロパティを実装
    protected override float DefaultCooldown => 80f;
    protected override int DefaultMaxUses => -1;

    protected override void ExecuteAbility(Player player)
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                TantrumHazard.PlaceTantrum(player.GetRandomSquarePosition(5f));
            }
            catch (Exception e)
            {
                Log.Error(e);
                return;
            }
        }
    }
    
    // APIs
    public static void ExecuteByApi(Vector3 pos)
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                TantrumHazard.PlaceTantrum(pos.GetRandomSquarePosition(5f));
            }
            catch (Exception e)
            {
                Log.Error(e);
                return;
            }
        }
    }
}
