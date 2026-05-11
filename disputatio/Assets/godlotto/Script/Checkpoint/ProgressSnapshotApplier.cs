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
                if (!string.IsNullOrEmpty(entry.key))
                    fc.SetBooleanVariable(entry.key, entry.value);
            }
        }

        if (data.fungusStrings != null)
        {
            for (int i = 0; i < data.fungusStrings.Length; i++)
            {
                var entry = data.fungusStrings[i];
                if (!string.IsNullOrEmpty(entry.key))
                    fc.SetStringVariable(entry.key, entry.value ?? string.Empty);
            }
        }
    }

    private static void ApplyInventory(CheckpointSaveData data)
    {
        InventoryManager inventory = InventoryManager.Instance;
        if (inventory != null)
            inventory.RestoreItemsByIds(data.itemIds);
    }
}
