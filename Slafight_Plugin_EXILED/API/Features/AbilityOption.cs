#nullable enable
using System;
using System.Collections.Generic;
using Exiled.API.Features;

namespace Slafight_Plugin_EXILED.API.Features;

public enum AbilityOptionDirection
{
    Previous = -1,
    Next = 1,
}

public sealed class AbilityOption
{
    public AbilityOption(string displayName)
        : this(displayName, displayName)
    {
    }

    private AbilityOption(string id, string displayName, string description = "")
    {
        Id = string.IsNullOrWhiteSpace(id)
            ? throw new ArgumentException("Option id cannot be empty.", nameof(id))
            : id;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? throw new ArgumentException("Option display name cannot be empty.", nameof(displayName))
            : displayName;
        Description = description ?? string.Empty;
    }

    public static AbilityOption Create(string id, string displayName, string description = "")
        => new(id, displayName, description);

    public string Id { get; }
    public string DisplayName { get; }
    public string Name => DisplayName;
    public string Description { get; }

    public bool Is(string id)
        => string.Equals(Id, id, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => DisplayName;
}

public abstract class OptionAbilityBase : AbilityBase
{
    private int _selectedOptionIndex;

    public IReadOnlyList<AbilityOption> AvailableOptions => field ??= NormalizeOptions(DefineOptions());

    public IReadOnlyList<AbilityOption> Options => AvailableOptions;

    public int SelectedOptionIndex
    {
        get
        {
            if (_selectedOptionIndex < 0 || _selectedOptionIndex >= AvailableOptions.Count)
                _selectedOptionIndex = 0;

            return _selectedOptionIndex;
        }
        protected set => _selectedOptionIndex = WrapIndex(value, AvailableOptions.Count);
    }

    public AbilityOption SelectedOption => AvailableOptions[SelectedOptionIndex];

    public override bool HasSelectableOptions => AvailableOptions.Count > 1;

    public override string GetSelectedOptionName(Player player)
        => SelectedOption.DisplayName;

    public override string GetSelectedOptionDescription(Player player)
        => SelectedOption.Description;

    public override bool TrySwitchOptionFromInput(Player player, AbilityOptionDirection direction)
    {
        if (!CanSwitchOption(player))
            return false;

        var options = AvailableOptions;
        if (options.Count <= 1)
            return false;

        var previousIndex = SelectedOptionIndex;
        var nextIndex = WrapIndex(previousIndex + (int)direction, options.Count);
        if (nextIndex == previousIndex)
            return false;

        _selectedOptionIndex = nextIndex;
        OnOptionChanged(player, options[previousIndex], options[nextIndex]);
        AbilityManager.UpdateAbilityHint(player, AbilityManager.TryGetLoadout(player, out var loadout) ? loadout : null);

        if (ShowOptionSwitchHint)
            ShowOptionHint(player, options[nextIndex]);

        return true;
    }

    protected static AbilityOption Option(string id, string displayName, string description = "")
        => AbilityOption.Create(id, displayName, description);

    protected static AbilityOption Option(string displayName)
        => new(displayName);

    protected abstract IReadOnlyList<AbilityOption> DefineOptions();

    protected virtual bool CanUseOption(Player player, AbilityOption option, out string failureReason)
    {
        failureReason = string.Empty;
        return true;
    }

    protected abstract void UseOption(Player player, AbilityOption option);

    protected sealed override bool CanActivate(Player player, out string failureReason)
    {
        if (!base.CanActivate(player, out failureReason))
            return false;

        return CanUseOption(player, SelectedOption, out failureReason);
    }

    protected sealed override void ExecuteAbility(Player player)
        => UseOption(player, SelectedOption);

    protected virtual bool ShowOptionSwitchHint => true;

    protected virtual bool CanSwitchOption(Player player)
    {
        if (player?.ReferenceHub == null || !player.IsAlive)
            return false;

        return AbilityManager.TryGetLoadout(player, out var loadout) &&
               loadout.ActiveAbility == this;
    }

    protected virtual void OnOptionChanged(Player player, AbilityOption previousOption, AbilityOption nextOption)
    {
    }

    protected virtual void ShowOptionHint(Player player, AbilityOption option)
    {
        var description = string.IsNullOrWhiteSpace(option.Description)
            ? string.Empty
            : $"\n<size=20>{option.Description}</size>";
        player.ShowHint($"<size=24>オプション: {option.DisplayName}</size>{description}", 2f);
    }

    private static IReadOnlyList<AbilityOption> NormalizeOptions(IReadOnlyList<AbilityOption>? options)
    {
        if (options == null || options.Count == 0)
            return [new AbilityOption("Default")];

        return options;
    }

    private static int WrapIndex(int index, int count)
    {
        if (count <= 0)
            return 0;

        var wrapped = index % count;
        return wrapped < 0 ? wrapped + count : wrapped;
    }
}
