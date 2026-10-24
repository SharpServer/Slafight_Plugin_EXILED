#nullable enable
using Slafight_Plugin_EXILED.API.Core.Features;

namespace Slafight_Plugin_EXILED.API.Core.Structs;

/// <summary>
/// 陣営・ロール・ユーザー・プレイヤーに適用する定型文通信の上書きです。
/// null の項目は下位の設定（バニラ陣営 → バニラロール → CustomTeam → CustomRole → ユーザー → プレイヤー）を引き継ぎます。
/// </summary>
public readonly struct CommunicationPolicy
{
    public CommunicationPolicy(
        bool? isAvailable = null,
        string? prefix = null,
        string? proximityPrefix = null,
        string? radioPrefix = null,
        string? proximityLabel = null,
        string? radioLabel = null,
        float? proximityRange = null,
        bool? isRadioAvailable = null,
        CommunicationMenuMode? menuMode = null,
        CommunicationMenu? menu = null)
    {
        IsAvailable = isAvailable;
        Prefix = prefix;
        ProximityPrefix = proximityPrefix;
        RadioPrefix = radioPrefix;
        ProximityLabel = proximityLabel;
        RadioLabel = radioLabel;
        ProximityRange = proximityRange;
        IsRadioAvailable = isRadioAvailable;
        MenuMode = menuMode;
        Menu = menu;
    }

    /// <summary>通信機能を利用できるか。null なら下位設定を引き継ぎます。</summary>
    public bool? IsAvailable { get; }

    /// <summary>
    /// 発言者名の前へ付ける陣営表記。null なら引き継ぎ、空文字なら非表示にします。
    /// </summary>
    public string? Prefix { get; }

    /// <summary>近接で届いた行だけに使う陣営表記。null なら <see cref="Prefix"/> を使います。</summary>
    public string? ProximityPrefix { get; }

    /// <summary>Radioで届いた行だけに使う陣営表記。null なら <see cref="Prefix"/> を使います。</summary>
    public string? RadioPrefix { get; }

    /// <summary>近接経路の先頭に出す表示。null なら引き継ぎます。</summary>
    public string? ProximityLabel { get; }

    /// <summary>Radio経路の先頭に出す表示。null なら引き継ぎます。</summary>
    public string? RadioLabel { get; }

    /// <summary>近接通信の距離。null なら引き継ぎます。</summary>
    public float? ProximityRange { get; }

    /// <summary>使用可能なRadioを所持しているとき無線経路も使うか。null なら引き継ぎます。</summary>
    public bool? IsRadioAvailable { get; }

    /// <summary>定型文の選択UI。null なら引き継ぎます。</summary>
    public CommunicationMenuMode? MenuMode { get; }

    /// <summary>カテゴリと定型文のツリー。null なら下位設定、全層でnullなら共通メニューを使います。</summary>
    public CommunicationMenu? Menu { get; }

    /// <summary>可否を引き継ぎます。prefix を指定すれば表記だけ上書きします。</summary>
    public static CommunicationPolicy Inherit(
        string? prefix = null,
        string? proximityPrefix = null,
        string? radioPrefix = null,
        string? proximityLabel = null,
        string? radioLabel = null,
        float? proximityRange = null,
        bool? isRadioAvailable = null,
        CommunicationMenuMode? menuMode = null,
        CommunicationMenu? menu = null)
        => new(
            null,
            prefix,
            proximityPrefix,
            radioPrefix,
            proximityLabel,
            radioLabel,
            proximityRange,
            isRadioAvailable,
            menuMode,
            menu);

    /// <summary>この層で通信を許可します。</summary>
    public static CommunicationPolicy Enabled(
        string? prefix = null,
        string? proximityPrefix = null,
        string? radioPrefix = null,
        string? proximityLabel = null,
        string? radioLabel = null,
        float? proximityRange = null,
        bool? isRadioAvailable = null,
        CommunicationMenuMode? menuMode = null,
        CommunicationMenu? menu = null)
        => new(
            true,
            prefix,
            proximityPrefix,
            radioPrefix,
            proximityLabel,
            radioLabel,
            proximityRange,
            isRadioAvailable,
            menuMode,
            menu);

    /// <summary>この層で通信を禁止します。</summary>
    public static CommunicationPolicy Disabled(string? prefix = null) => new(false, prefix);
}

/// <summary>プレイヤーについて全上書きを解決した、実際に使用する通信設定です。</summary>
public readonly struct ResolvedCommunicationPolicy
{
    public ResolvedCommunicationPolicy(
        bool isAvailable,
        string? prefix,
        string? proximityPrefix = null,
        string? radioPrefix = null,
        string? proximityLabel = "近接",
        string? radioLabel = "通信",
        float proximityRange = 8f,
        bool isRadioAvailable = true,
        CommunicationMenuMode menuMode = CommunicationMenuMode.Scp330,
        CommunicationMenu? menu = null)
    {
        IsAvailable = isAvailable;
        Prefix = prefix ?? string.Empty;
        ProximityPrefix = proximityPrefix ?? Prefix;
        RadioPrefix = radioPrefix ?? Prefix;
        ProximityLabel = proximityLabel ?? "近接";
        RadioLabel = radioLabel ?? "通信";
        ProximityRange = proximityRange;
        IsRadioAvailable = isRadioAvailable;
        MenuMode = menuMode;
        Menu = menu;
    }

    public bool IsAvailable { get; }

    public string Prefix { get; }

    public string ProximityPrefix { get; }

    public string RadioPrefix { get; }

    public string ProximityLabel { get; }

    public string RadioLabel { get; }

    public float ProximityRange { get; }

    public bool IsRadioAvailable { get; }

    public CommunicationMenuMode MenuMode { get; }

    public CommunicationMenu? Menu { get; }

    internal ResolvedCommunicationPolicy Apply(CommunicationPolicy policy)
        => new(
            policy.IsAvailable ?? IsAvailable,
            policy.Prefix ?? Prefix,
            policy.ProximityPrefix ?? policy.Prefix ?? ProximityPrefix,
            policy.RadioPrefix ?? policy.Prefix ?? RadioPrefix,
            policy.ProximityLabel ?? ProximityLabel,
            policy.RadioLabel ?? RadioLabel,
            policy.ProximityRange ?? ProximityRange,
            policy.IsRadioAvailable ?? IsRadioAvailable,
            policy.MenuMode ?? MenuMode,
            policy.Menu ?? Menu);
}

/// <summary>定型文を選ぶために使うクライアントUIです。</summary>
public enum CommunicationMenuMode
{
    /// <summary>SCP-330の6分割メニューを一時的に使用します。</summary>
    Scp330,

    /// <summary>画面下部の文字メニューをServer Specific Settingsキーで操作します。</summary>
    Text,
}
