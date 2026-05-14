namespace Slafight_Plugin_EXILED.API.Features;

public class AbilityLoadout
{
    public const int MaxSlots = 3;

    // スロット → AbilityBase のインスタンス
    public AbilityBase[] Slots { get; } = new AbilityBase[MaxSlots];

    public int ActiveIndex { get; set; } = 0;

    public AbilityBase ActiveAbility => Slots[ActiveIndex];

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

    public void CycleNext()
    {
        if (Slots.Length <= 1) return;

        for (int i = 1; i <= MaxSlots; i++)
        {
            int idx = (ActiveIndex + i) % MaxSlots;
            if (Slots[idx] != null)
            {
                ActiveIndex = idx;
                return;
            }
        }
    }
}
