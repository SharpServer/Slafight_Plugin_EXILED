using System;

namespace Slafight_Plugin_EXILED.API.Core.Features.Attributes;

/// <summary>
/// この型が昔名乗っていた名前です。<see cref="TypeParser"/> がこの綴りでも解決します。
/// </summary>
/// <remarks>過去の綴り専用です。新しい別名には使わないでください。</remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class LegacyNameAttribute : Attribute
{
    public LegacyNameAttribute(string name) => Name = name ?? string.Empty;

    /// <summary>昔の名前です。</summary>
    public string Name { get; }
}
