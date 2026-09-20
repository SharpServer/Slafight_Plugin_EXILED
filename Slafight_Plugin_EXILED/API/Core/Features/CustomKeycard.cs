using Exiled.API.Enums;
using Exiled.API.Features.Items;

using ExiledItem = Exiled.API.Features.Items.Item;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// <see cref="CustomItem"/> のキーカード向け基底です。
/// 権限は付与時と再拾得時に常に復元されます。
/// </summary>
public abstract class CustomKeycard : CustomItem
{
    /// <summary>このカードが持つアクセス権限。</summary>
    protected abstract KeycardPermissions Permissions { get; }

    protected override void Customize(LabApi.Features.Wrappers.Item item)
    {
        if (ExiledItem.Get(item.Base) is Keycard keycard)
            keycard.Permissions = Permissions;

        base.Customize(item);
    }
}
