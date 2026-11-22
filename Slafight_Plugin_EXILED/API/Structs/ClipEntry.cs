using Slafight_Plugin_EXILED.API.Enums;

namespace Slafight_Plugin_EXILED.API.Structs;

/// <summary>この銃に登録されている全クリップの (AudioIndex, クリップ名, 判定できた種類)。</summary>
public readonly struct ClipEntry(int index, string name, GunSoundKind? kind)
{
    public int Index { get; } = index;
    public string Name { get; } = name;
    public GunSoundKind? Kind { get; } = kind;

    public override string ToString()
        => $"[{Index}] {Name}" + (Kind.HasValue ? $" ({Kind})" : string.Empty);
}
