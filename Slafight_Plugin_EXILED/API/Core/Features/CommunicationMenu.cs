#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Slafight_Plugin_EXILED.CustomItems.Accesibilities.InventoryChat;

namespace Slafight_Plugin_EXILED.API.Core.Features;

/// <summary>
/// 定型文通信のカテゴリ・内容をまとめた変更不可のツリーです。
/// SCP-330と文字メニューの両方で使えます。各階層は最大6件、空のルートはメニュー非表示です。
/// </summary>
public sealed class CommunicationMenu
{
    public const int MaxOptions = 6;

    public CommunicationMenu(IEnumerable<CannedChatNode> options)
    {
        CannedChatNode[] snapshot = options?.ToArray() ?? throw new ArgumentNullException(nameof(options));
        ValidateOptions(snapshot, "root");
        Options = Array.AsReadOnly(snapshot);
    }

    public IReadOnlyList<CannedChatNode> Options { get; }

    private static void ValidateOptions(IReadOnlyList<CannedChatNode> options, string path)
    {
        if (options.Count > MaxOptions)
            throw new ArgumentException($"{path} has {options.Count} options; SCP-330 supports at most {MaxOptions}.");

        foreach (CannedChatNode node in options)
        {
            if (node is null)
                throw new ArgumentException($"{path} contains a null option.");

            if (string.IsNullOrWhiteSpace(node.Label))
                throw new ArgumentException($"{path} contains an option without a label.");

            if (node.Children.Count > 0 && (node.Message is not null || node.Action is not null))
                throw new ArgumentException($"{path}/{node.Label} cannot be both a category and an action.");

            if (node.Children.Count == 0 && string.IsNullOrWhiteSpace(node.Message) && node.Action is null)
                throw new ArgumentException($"{path}/{node.Label} has no children or action.");

            ValidateOptions(node.Children, $"{path}/{node.Label}");
        }
    }

}
