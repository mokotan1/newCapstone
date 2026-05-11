using System.Collections.Generic;
using Fungus;

public static class ProgressSnapshotCollector
{
    private static readonly string[] BooleanKeys =
    {
        FungusVariableKeys.ElectricOn,
        FungusVariableKeys.UsedStudyKey,
        FungusVariableKeys.UsedMaidKey,
        FungusVariableKeys.UsedBedKey,
        FungusVariableKeys.UsedWifeKey,
        FungusVariableKeys.UsedTutorKey,
        FungusVariableKeys.UsedChildKey,
        FungusVariableKeys.WindowClicked,
        FungusVariableKeys.IsClicked,
    };

    private static readonly string[] StringKeys =
    {
        FungusVariableKeys.InventoryItemIds,
    };

    public static void Populate(CheckpointSaveData data)
    {
        if (data == null)
            return;

        CaptureInventory(data);
        CaptureFungusVariables(data);
    }

    private static void CaptureInventory(CheckpointSaveData data)
    {
        var inventory = InventoryManager.Instance;
        if (inventory == null)
        {
            data.itemIds = new int[0];
            return;
        }

        var ids = new List<int>();
        foreach (Item item in inventory.Items)
        {
            if (item != null)
                ids.Add(item.itemId);
        }

        data.itemIds = ids.ToArray();
    }

    private static void CaptureFungusVariables(CheckpointSaveData data)
    {
        Flowchart fc = FlowchartLocator.Find();
        if (fc == null)
            return;

        var bools = new List<BoolCheckpointEntry>();
        for (int i = 0; i < BooleanKeys.Length; i++)
        {
            string key = BooleanKeys[i];
            bools.Add(new BoolCheckpointEntry(key, fc.GetBooleanVariable(key)));
        }

        var strings = new List<StringCheckpointEntry>();
        for (int i = 0; i < StringKeys.Length; i++)
        {
            string key = StringKeys[i];
            strings.Add(new StringCheckpointEntry(key, fc.GetStringVariable(key)));
        }

        data.fungusBooleans = bools.ToArray();
        data.fungusStrings = strings.ToArray();
    }
}
