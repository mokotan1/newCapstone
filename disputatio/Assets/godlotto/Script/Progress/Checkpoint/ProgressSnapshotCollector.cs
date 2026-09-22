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
    };

    private static readonly string[] IntegerKeys =
    {
        FungusVariableKeys.CorrectAnswerCount,
        ItemAcquisitionTracker.FungusVariableKey,
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

    public static bool TryRefreshRuntimeSnapshot(CheckpointSaveData data)
    {
        if (data == null)
            return false;

        bool capturedAny = false;
        capturedAny |= CaptureInventory(data, preserveExistingWhenUnavailable: true);
        capturedAny |= CaptureFungusVariables(data, preserveExistingWhenUnavailable: true);
        return capturedAny;
    }

    private static bool CaptureInventory(CheckpointSaveData data, bool preserveExistingWhenUnavailable = false)
    {
        var inventory = InventoryManager.Instance;
        if (inventory == null)
        {
            if (!preserveExistingWhenUnavailable)
                data.itemIds = new int[0];
            return false;
        }

        var ids = new List<int>();
        foreach (Item item in inventory.Items)
        {
            if (item != null)
                ids.Add(item.itemId);
        }

        data.itemIds = ids.ToArray();
        return true;
    }

    private static bool CaptureFungusVariables(CheckpointSaveData data, bool preserveExistingWhenUnavailable = false)
    {
        Flowchart fc = FlowchartLocator.Find();
        if (fc == null)
            return false;

        var bools = new List<BoolCheckpointEntry>();
        var capturedBoolKeys = new HashSet<string>();
        for (int i = 0; i < BooleanKeys.Length; i++)
        {
            string key = BooleanKeys[i];
            if (ShouldCaptureFungusKey(key) && capturedBoolKeys.Add(key))
                bools.Add(new BoolCheckpointEntry(key, fc.GetBooleanVariable(key)));
        }

        var ints = new List<IntCheckpointEntry>();
        var capturedIntKeys = new HashSet<string>();
        for (int i = 0; i < IntegerKeys.Length; i++)
        {
            string key = IntegerKeys[i];
            if (ShouldCaptureFungusKey(key) && capturedIntKeys.Add(key))
                ints.Add(new IntCheckpointEntry(key, fc.GetIntegerVariable(key)));
        }

        var strings = new List<StringCheckpointEntry>();
        var capturedStringKeys = new HashSet<string>();
        for (int i = 0; i < StringKeys.Length; i++)
        {
            string key = StringKeys[i];
            if (ShouldCaptureFungusKey(key) && capturedStringKeys.Add(key))
                strings.Add(new StringCheckpointEntry(key, fc.GetStringVariable(key)));
        }

        foreach (Variable variable in fc.Variables)
        {
            if (variable == null || !ShouldCaptureFungusKey(variable.Key))
                continue;

            if (variable is BooleanVariable booleanVariable && capturedBoolKeys.Add(variable.Key))
                bools.Add(new BoolCheckpointEntry(variable.Key, booleanVariable.Value));
            else if (variable is IntegerVariable integerVariable && capturedIntKeys.Add(variable.Key))
                ints.Add(new IntCheckpointEntry(variable.Key, integerVariable.Value));
            else if (variable is StringVariable stringVariable && capturedStringKeys.Add(variable.Key))
                strings.Add(new StringCheckpointEntry(variable.Key, stringVariable.Value));
        }

        data.fungusBooleans = bools.ToArray();
        data.fungusIntegers = ints.ToArray();
        data.fungusStrings = strings.ToArray();
        return true;
    }

    private static bool ShouldCaptureFungusKey(string key)
    {
        return ProgressSnapshotPolicy.ShouldCapturePlayerPrefsKey(key);
    }
}
