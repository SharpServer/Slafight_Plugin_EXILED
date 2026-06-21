using System;
using Exiled.API.Features;
using Slafight_Plugin_EXILED.API.Features;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.CustomMaps.Core.DoorAccess;

public sealed class SpecialDoorAccessRule
{
    private const string ItemOnlyHintMessage = "<size=24>専用のアクセス用アイテムが必要そうだ・・・</size>";
    private const string CodeOnlyHintMessage = "<size=24>コードが正しくないようだ・・・</size>";
    private const string ItemAndCodeHintMessage = "<size=24>専用のアクセス用アイテム及びコードが揃っていないようだ・・・</size>";
    private const string NoRequirementHintMessage = "<size=24>しかし、何も反応しなかった。</size>";

    private string? _hintMessage;

    public Type? RequiredCItemType { get; set; }

    public string? RequiredCode { get; set; }

    public string HintMessage
    {
        get => _hintMessage ?? GetDefaultHintMessage();
        set => _hintMessage = value;
    }

    public bool CanOpen(Player player)
    {
        if (RequiredCItemType == null && RequiredCode == null)
            return false;

        bool hasItem = CheckItem(player);
        bool hasCode = CheckCode(player);

        if (RequiredCItemType != null && RequiredCode == null)
            return hasItem;

        if (RequiredCItemType == null && RequiredCode != null)
            return hasCode;

        return hasItem || hasCode;
    }

    private bool CheckItem(Player player)
    {
        if (RequiredCItemType == null)
            return false;

        foreach (var item in CItem.GetAllInstances())
        {
            if (item.GetType() == RequiredCItemType && item.HasIn(player))
                return true;
        }

        return false;
    }

    private bool CheckCode(Player player)
    {
        return RequiredCode != null &&
               RPNameSetter.TryGetPasscode(player, out var code) &&
               code == RequiredCode;
    }

    private string GetDefaultHintMessage()
    {
        if (RequiredCItemType != null && RequiredCode == null)
            return ItemOnlyHintMessage;

        if (RequiredCItemType == null && RequiredCode != null)
            return CodeOnlyHintMessage;

        if (RequiredCItemType != null && RequiredCode != null)
            return ItemAndCodeHintMessage;

        return NoRequirementHintMessage;
    }
}
