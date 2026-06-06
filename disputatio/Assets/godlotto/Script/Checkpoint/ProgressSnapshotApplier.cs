using Fungus;

public static class ProgressSnapshotApplier
{
    public static void Apply(CheckpointSaveData data)
    {
        if (data == null)
            return;

        ApplyFungusVariables(data);
        ApplyInventory(data);
    }

    private static void ApplyFungusVariables(CheckpointSaveData data)
    {
        Flowchart fc = FlowchartLocator.Find();
        if (fc == null)
            return;

        if (data.fungusBooleans != null)
        {
            for (int i = 0; i < data.fungusBooleans.Length; i++)
            {
                var entry = data.fungusBooleans[i];
                if (ShouldApplyFungusKey(entry.key))
                    fc.SetBooleanVariable(entry.key, entry.value);
            }
        }

        if (data.fungusIntegers != null)
        {
            for (int i = 0; i < data.fungusIntegers.Length; i++)
            {
                var entry = data.fungusIntegers[i];
                if (ShouldApplyFungusKey(entry.key))
                    fc.SetIntegerVariable(entry.key, entry.value);
            }
        }

        if (data.fungusStrings != null)
        {
            for (int i = 0; i < data.fungusStrings.Length; i++)
            {
                var entry = data.fungusStrings[i];
                if (ShouldApplyFungusKey(entry.key))
                    fc.SetStringVariable(entry.key, entry.value ?? string.Empty);
            }
        }
    }

    private static bool ShouldApplyFungusKey(string key)
    {
        return ProgressSnapshotPolicy.ShouldCapturePlayerPrefsKey(key);
    }

    private static void ApplyInventory(CheckpointSaveData data)
    {
        if (HasProgressInventorySnapshot(data))
            InventoryAccessState.Unlock();

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null)
            inventory.RestoreItemsByIds(data.itemIds);
    }

    private static bool HasProgressInventorySnapshot(CheckpointSaveData data)
    {
        return data != null
               && data.itemIds != null
               && data.itemIds.Length > 0;
    }
}
