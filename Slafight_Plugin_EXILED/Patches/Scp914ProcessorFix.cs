using System;
using Exiled.API.Features;
using InventorySystem;
using InventorySystem.Items;
using Scp914.Processors;
using Server = Exiled.Events.Handlers.Server;

namespace Slafight_Plugin_EXILED.Patches;

/// <summary>
/// Ensures every item prefab can enter SCP-914 event pipeline.
/// The base game skips items without an <see cref="Scp914ItemProcessor"/>,
/// so missing processors are replaced with a pass-through processor.
/// </summary>
public static class Scp914ProcessorFix
{
    private static bool _isRegistered;

    public static void Register()
    {
        if (_isRegistered)
            return;

        Server.WaitingForPlayers += AddMissingProcessors;
        _isRegistered = true;

        // Also supports plugin reloads during an active round.
        AddMissingProcessors();
    }

    public static void Unregister()
    {
        if (!_isRegistered)
            return;

        Server.WaitingForPlayers -= AddMissingProcessors;
        _isRegistered = false;
    }

    private static void AddMissingProcessors()
    {
        int addedCount = 0;
        foreach (var entry in InventoryItemLoader.AvailableItems)
        {
            ItemType itemType = entry.Key;
            ItemBase item = entry.Value;

            if (item is null || item.TryGetComponent<Scp914ItemProcessor>(out _))
                continue;

            try
            {
                AddPassthroughProcessor(item, itemType);
                addedCount++;
                Log.Debug($"[Scp914ProcessorFix] Added a pass-through processor for {itemType}.");
            }
            catch (Exception exception)
            {
                Log.Warn($"[Scp914ProcessorFix] Failed to add a processor for {itemType}: {exception}");
            }
        }

        if (addedCount > 0)
            Log.Info($"[Scp914ProcessorFix] Added {addedCount} missing SCP-914 processors.");
    }

    private static void AddPassthroughProcessor(ItemBase item, ItemType itemType)
    {
        ItemType[] outputs = [itemType];
        StandardItemProcessor processor = item.gameObject.AddComponent<StandardItemProcessor>();

        processor._roughOutputs = outputs;
        processor._coarseOutputs = outputs;
        processor._oneToOneOutputs = outputs;
        processor._fineOutputs = outputs;
        processor._veryFineOutputs = outputs;
        processor._fireUpgradeTrigger = false;
    }
}
