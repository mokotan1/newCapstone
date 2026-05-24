using System.Reflection;
using NUnit.Framework;

public class ItemPickupLifecycleTests
{
    [Test]
    public void ItemPickup_DoesNotSuppressInAwake_BeforeCheckpointSnapshotCanApply()
    {
        MethodInfo awake = typeof(ItemPickup).GetMethod(
            "Awake",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.IsNull(awake, "ItemPickup must not destroy pickups in Awake because checkpoint restore runs after scene Awake.");
    }

    [Test]
    public void ItemPickup_SuppressesInStart_AfterCheckpointSnapshotCanApply()
    {
        MethodInfo start = typeof(ItemPickup).GetMethod(
            "Start",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.IsNotNull(start, "ItemPickup should defer already-taken suppression until Start.");
    }
}
