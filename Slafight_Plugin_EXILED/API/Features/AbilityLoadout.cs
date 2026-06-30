namespace Slafight_Plugin_EXILED.API.Features;

public class AbilityLoadout
{
    public const int MaxSlots = 3;

    // スロット → AbilityBase のインスタンス
    public AbilityBase[] Slots { get; } = new AbilityBase[MaxSlots];

    public int ActiveIndex { get; set; } = 0;

    public AbilityBase ActiveAbility
    {
        get
        {
            EnsureActiveSlot();
            return Slots[ActiveIndex];
        }
    }

    public bool HasFreeSlot()
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            if (Slots[i] == null)
                return true;
        }

        return false;
    }

    public bool AddAbility(AbilityBase ability)
    {
        for (int i = 0; i < MaxSlots; i++)
        {
            if (Slots[i] == null)
            {
                Slots[i] = ability;
                return true;
            }
        }
        return false; // もう入らない
    }

    public bool EnsureActiveSlot()
    {
        if (ActiveIndex is < 0 or >= MaxSlots)
            ActiveIndex = 0;

        if (Slots[ActiveIndex] != null)
            return true;

        for (int i = 0; i < MaxSlots; i++)
        {
            if (Slots[i] != null)
            {
                ActiveIndex = i;
                return true;
            }
        }

        return false;
    }

    public bool CycleNext()
    {
        if (!EnsureActiveSlot())
            return false;

        for (int i = 1; i < MaxSlots; i++)
        {
            int idx = (ActiveIndex + i) % MaxSlots;
            if (Slots[idx] != null)
            {
                ActiveIndex = idx;
                return true;
            }
        }

        return false;
    }
}
