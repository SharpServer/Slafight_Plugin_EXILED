using System.Collections.Generic;
using System.Linq;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;

namespace Slafight_Plugin_EXILED.Extensions;

public static class PlayerCItemBaseExtensions
{
    public static PlayerCItemBase GetCItemBase(this Player player)
    {
        var @base = new PlayerCItemBase { Player = player };
        return @base;
    }

    public static bool HasCItem<T>(this Player player) where T : CItem => player.GetCItemBase().HasItem<T>();
    public static bool GiveCItem<T>(this Player player) where T : CItem => player.GetCItemBase().TryGiveItem<T>();
}

public class PlayerCItemBase
{
    public Player Player { get; init; } = null!;
    public List<CItem> OwnItemInstances => GetOwnItemInstances();

    // ===== [PUBLIC APIs] ===== //
    public bool HasItem<T>() where T : CItem
    {
        return Player is not null && OwnItemInstances.Any(x => x is T);
    }

    public bool TryGiveItem<T>(bool showMessage = false) where T : CItem
    {
        if (Player is null) return false;
        return CItem.Get<T>()?.Give(Player, showMessage) is not null;
    }
    
    // ===== [PRIVATE] ===== //
    private List<CItem> GetOwnItemInstances()
    {
        var result = new List<CItem>();
        Player.Items.ToList().ForEach(item =>
        {
            CItem.TryGet(item, out var cItem);
            if (cItem != null)
            {
                result.Add(cItem);
            }
        });
        return result;
    }
}
