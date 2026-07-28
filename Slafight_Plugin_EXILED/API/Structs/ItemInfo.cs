using Exiled.API.Features.Items;
using Exiled.API.Features.Pickups;

namespace Slafight_Plugin_EXILED.API.Structs;

public struct ItemInfo(Item? item = null, Pickup? pickup = null)
{
    public Item? Item = item;
    public Pickup? Pickup = pickup;
}