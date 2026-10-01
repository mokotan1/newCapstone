using Fungus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

[TestFixture]
public sealed class WorldItemDropZoneHostTests
{
    GameObject door;
    Item item;

    [SetUp]
    public void SetUp()
    {
        HallGlobalStateHost.ResetForTests();
        item = ScriptableObject.CreateInstance<Item>();
        item.itemName = "WifeRoomKey";
        door = new GameObject("Wife_Door");
        door.SetActive(false);
    }

    [TearDown]
    public void TearDown()
    {
        HallGlobalStateHost.ResetForTests();
        if (door != null)
            Object.DestroyImmediate(door);
        if (item != null)
            Object.DestroyImmediate(item);
    }

    [Test]
    public void PersistedWifeKey_DisablesDropZoneAndKeepsDoorCollider()
    {
        BoxCollider2D collider = door.AddComponent<BoxCollider2D>();
        Clickable2D clickable = door.AddComponent<Clickable2D>();
        clickable.enabled = false;
        WorldItemDropZone zone = door.AddComponent<WorldItemDropZone>();
        zone.requiredItem = item;
        zone.dialogBoolName = FungusVariableKeys.IsClicked;
        HallGlobalStateHost.EnsureInstance();
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.UsedWifeKey, true);
        door.SetActive(true);
        typeof(WorldItemDropZone).GetMethod("Start", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(zone, null);

        Assert.IsFalse(zone.enabled);
        Assert.IsTrue(collider.enabled);
        Assert.IsNotNull(door.GetComponent<Clickable2D>());
    }

    [Test]
    public void HostIsClicked_RejectsDropWithoutUnlock()
    {
        door.AddComponent<BoxCollider2D>();
        WorldItemDropZone zone = door.AddComponent<WorldItemDropZone>();
        zone.requiredItem = item;
        zone.dialogBoolName = FungusVariableKeys.IsClicked;
        bool unlocked = false;
        zone.onUnlock = new UnityEvent();
        zone.onUnlock.AddListener(() => unlocked = true);
        HallGlobalStateHost.EnsureInstance();
        HallGlobalStateHost.Instance.SetBool(FungusVariableKeys.IsClicked, true);

        door.SetActive(true);
        bool applied = zone.TryApplyDroppedItem(item);

        Assert.IsFalse(applied);
        Assert.IsFalse(unlocked);
        Assert.IsTrue(zone.enabled);
        Assert.AreSame(item, zone.requiredItem);
    }

    [Test]
    public void MissingHost_UsesFlowchartPathAndAllowsDropWhenFlowchartIsAbsent()
    {
        door.AddComponent<BoxCollider2D>();
        Clickable2D clickable = door.AddComponent<Clickable2D>();
        clickable.enabled = false;
        WorldItemDropZone zone = door.AddComponent<WorldItemDropZone>();
        item.itemName = "TestToken";
        zone.requiredItem = item;
        zone.dialogBoolName = FungusVariableKeys.IsClicked;
        zone.flowchart = null;
        bool unlocked = false;
        zone.onUnlock = new UnityEvent();
        zone.onUnlock.AddListener(() => unlocked = true);

        door.SetActive(true);
        bool applied = zone.TryApplyDroppedItem(item);

        Assert.IsNull(HallGlobalStateHost.Instance);
        Assert.IsTrue(applied);
        Assert.IsTrue(unlocked);
        Assert.IsFalse(zone.enabled);
        Assert.IsTrue(door.GetComponent<Collider2D>().enabled);
    }
}
