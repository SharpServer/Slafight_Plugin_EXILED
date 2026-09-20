#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// Option の 1 パート定義（型 + 名前 + 制約）。
/// <see cref="Int"/> / <see cref="Float"/> / <see cref="Bool"/> / <see cref="String"/> /
/// <see cref="Enum{TEnum}"/> / <see cref="Of{T}"/> のファクトリで生成する。
/// </summary>
public sealed class OptionPart
{
    private readonly Func<object?, bool>? _validator;

    private OptionPart(string name, Type valueType, object? defaultValue, Func<object?, bool>? validator, string? constraint)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("OptionPart name must not be empty.", nameof(name));

        Name = name.Trim();
        ValueType = valueType;
        DefaultValue = defaultValue;
        _validator = validator;
        ConstraintDescription = constraint;
    }

    public string Name { get; }

    public Type ValueType { get; }

    public object? DefaultValue { get; }

    /// <summary>制約の説明（例: "0~3"）。エラーメッセージと usage 表示に使う。</summary>
    public string? ConstraintDescription { get; }

    /// <summary>整数パート。min/max で受け付ける範囲を制限できる。</summary>
    public static OptionPart Int(string name, int defaultValue = 0, int? min = null, int? max = null)
        => new(
            name,
            typeof(int),
            defaultValue,
            value => value is int i && (min == null || i >= min) && (max == null || i <= max),
            DescribeRange(min, max));

    /// <summary>小数パート。min/max で受け付ける範囲を制限できる。</summary>
    public static OptionPart Float(string name, float defaultValue = 0f, float? min = null, float? max = null)
        => new(
            name,
            typeof(float),
            defaultValue,
            value => value is float f && (min == null || f >= min) && (max == null || f <= max),
            DescribeRange(min, max));

    /// <summary>真偽値パート。</summary>
    public static OptionPart Bool(string name, bool defaultValue = false)
        => new(name, typeof(bool), defaultValue, null, null);

    /// <summary>
    /// 文字列パート。<paramref name="allowedValues"/> を渡すとその値のみ受け付ける（大文字小文字無視）。
    /// カンマを含む文字列は最後のパートでのみ安全に扱える。
    /// </summary>
    public static OptionPart String(string name, string defaultValue = "", params string[] allowedValues)
        => new(
            name,
            typeof(string),
            defaultValue,
            allowedValues.Length == 0
                ? null
                : value => value is string s && allowedValues.Any(a => string.Equals(a, s, StringComparison.OrdinalIgnoreCase)),
            allowedValues.Length == 0 ? null : string.Join("|", allowedValues));

    /// <summary>列挙型パート。名前（大文字小文字無視）と数値の両方を受け付ける。</summary>
    public static OptionPart Enum<TEnum>(string name, TEnum defaultValue = default)
        where TEnum : struct, Enum
        => new(name, typeof(TEnum), defaultValue, null, string.Join("|", System.Enum.GetNames(typeof(TEnum))));

    /// <summary>
    /// 任意型パート。ObjectPrefab の Option シリアライザで変換できる型
    /// （enum / IFormattable / TypeConverter 対応型など）を使える。
    /// Vector 系はカンマを含むため複数パートの Option には向かない。
    /// </summary>
    public static OptionPart Of<T>(string name, T defaultValue, Func<T, bool>? validator = null, string? constraintDescription = null)
        => new(
            name,
            typeof(T),
            defaultValue,
            validator == null ? null : value => value is T typed && validator(typed),
            constraintDescription);

    internal bool TryParse(string raw, out object? value, out string error)
    {
        error = string.Empty;
        if (!ObjectPrefab.TryDeserializeOptionValue(raw, ValueType, out value))
        {
            error = $"'{raw}' is not a valid {TypeDisplayName}";
            return false;
        }

        if (!Validate(value))
        {
            error = $"'{raw}' is out of range for {Name}" +
                    (ConstraintDescription != null ? $" (allowed: {ConstraintDescription})" : string.Empty);
            return false;
        }

        return true;
    }

    internal bool Validate(object? value) => _validator == null || _validator(value);

    internal string Serialize(object? value)
        => ObjectPrefab.TrySerializeOptionValue(value, ValueType, out string serialized)
            ? serialized
            : value?.ToString() ?? string.Empty;

    internal string Describe()
        => ConstraintDescription != null
            ? $"{Name}:{TypeDisplayName}({ConstraintDescription})"
            : $"{Name}:{TypeDisplayName}";

    private string TypeDisplayName => ValueType.IsEnum ? ValueType.Name : ValueType.Name.ToLowerInvariant();

    private static string? DescribeRange<T>(T? min, T? max) where T : struct, IFormattable
    {
        if (min == null && max == null)
            return null;

        string minText = min?.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
        string maxText = max?.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
        return $"{minText}~{maxText}";
    }
}

/// <summary>
/// ObjectPrefab の宣言的オプション。public な Option プロパティ/フィールドとして宣言すると
/// メンバー名がオプションキーとして自動収集され、`.sl objprefab modify option` やマーカーの
/// Options から設定できる。値は "v1,v2,..." のカンマ区切りで、空パートは現在値を維持する。
/// <code>
/// public Option Inputs { get; } = new(
///     OptionPart.Int("Mode", 0, min: 0, max: 3),
///     OptionPart.Bool("Blinking"),
///     OptionPart.Bool("Loop"));
/// // 設定例: "2,true,false" / "2"（先頭のみ更新）/ ",true"（2番目のみ更新）
/// // 参照例: Inputs.Get&lt;int&gt;("Mode")
/// </code>
/// </summary>
public sealed class Option
{
    private readonly OptionPart[] _parts;
    private readonly object?[] _values;

    public Option(params OptionPart[] parts)
    {
        if (parts == null || parts.Length == 0)
            throw new ArgumentException("Option requires at least one OptionPart.", nameof(parts));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (OptionPart part in parts)
        {
            if (!seen.Add(part.Name))
                throw new ArgumentException($"Duplicate OptionPart name '{part.Name}'.", nameof(parts));
        }

        _parts = parts;
        _values = parts.Select(part => part.DefaultValue).ToArray();
    }

    public IReadOnlyList<OptionPart> Parts => _parts;

    /// <summary>値が変更されたときに発火する（TryApply / Set 経由）。</summary>
    public event Action? Changed;

    public T Get<T>(int index)
    {
        if (index < 0 || index >= _values.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        return _values[index] is T typed ? typed : default!;
    }

    public T Get<T>(string name) => Get<T>(IndexOf(name));

    public void Set<T>(int index, T value)
    {
        if (index < 0 || index >= _values.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        OptionPart part = _parts[index];
        if (!part.Validate(value))
        {
            throw new ArgumentException(
                $"Value '{value}' violates the constraint of {part.Describe()}.", nameof(value));
        }

        if (Equals(_values[index], value))
            return;

        _values[index] = value;
        Changed?.Invoke();
    }

    public void Set<T>(string name, T value) => Set(IndexOf(name), value);

    /// <summary>
    /// カンマ区切り文字列を適用する。空パートは現在値を維持、末尾パート省略も可。
    /// どれか 1 つでも解析/制約違反があれば何も適用せず false を返す。
    /// </summary>
    public bool TryApply(string raw, out string error)
    {
        error = string.Empty;
        raw ??= string.Empty;

        // 最後のパートは残りのカンマごと受け取る（文字列パート向け）
        string[] pieces = raw.Split([','], _parts.Length);
        if (pieces.Length > _parts.Length)
        {
            error = $"Too many values ({pieces.Length}). Expected: {Describe()}";
            return false;
        }

        var staged = new List<(int Index, object? Value)>();
        for (int i = 0; i < pieces.Length; i++)
        {
            string piece = pieces[i].Trim();
            if (piece.Length == 0)
                continue;

            if (!_parts[i].TryParse(piece, out object? value, out error))
            {
                error += $". Expected: {Describe()}";
                return false;
            }

            staged.Add((i, value));
        }

        bool changed = false;
        foreach ((int index, object? value) in staged)
        {
            if (Equals(_values[index], value))
                continue;

            _values[index] = value;
            changed = true;
        }

        if (changed)
            Changed?.Invoke();

        return true;
    }

    /// <summary>現在値を "v1,v2,..." に直列化する。</summary>
    public string Serialize()
        => string.Join(",", _parts.Select((part, i) => part.Serialize(_values[i])));

    /// <summary>usage 表示用のパート一覧（例: "Mode:int(0~3),Blinking:bool"）。</summary>
    public string Describe()
        => string.Join(",", _parts.Select(part => part.Describe()));

    private int IndexOf(string name)
    {
        for (int i = 0; i < _parts.Length; i++)
        {
            if (string.Equals(_parts[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        throw new KeyNotFoundException($"Option has no part named '{name}'. Parts: {Describe()}");
    }
}
