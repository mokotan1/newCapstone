using System.Reflection;
using Fungus;
using NUnit.Framework;
using UnityEngine;

public class InventoryManagerFungusSaveSignalTests
{
    GameObject variableManager;
    GameObject inventoryObject;
    InventoryManager inventory;
    Item carried;

    [SetUp]
    public void SetUp()
    {
        variableManager = new GameObject("Variablemanager");
        variableManager.AddComponent<Flowchart>();

        inventoryObject = new GameObject("InventoryManagerFungusSaveSignalTest");
        inventory = inventoryObject.AddComponent<InventoryManager>();
        if (InventoryManager.Instance == null)
            SetSingletonInstance(inventory);

        carried = ScriptableObject.CreateInstance<Item>();
        carried.itemId = 9;
        carried.itemName = "SignalTestItem";
        inventory.ClearItemsForNewGame();
        inventory.AddItem(carried);

        MethodInfo onEnable = typeof(InventoryManager).GetMethod(
            "OnEnable",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(onEnable);
        onEnable.Invoke(inventory, null);
    }

    [TearDown]
    public void TearDown()
    {
        if (carried != null)
            Object.DestroyImmediate(carried);
        if (inventoryObject != null)
            Object.DestroyImmediate(inventoryObject);
        if (variableManager != null)
            Object.DestroyImmediate(variableManager);
    }

    [Test]
    public void SavePointLoaded_DoesNotReplaceInventoryItems()
    {
        Assert.AreEqual(1, inventory.Items.Count);

        SaveManagerSignals.DoSavePointLoaded("Kitchen_Start");

        Assert.AreEqual(1, inventory.Items.Count);
        Assert.AreSame(carried, inventory.Items[0]);
    }

    static void SetSingletonInstance(InventoryManager target)
    {
        FieldInfo field = typeof(SingletonMonoBehaviour<InventoryManager>).GetField(
            "_instance",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field);
        field.SetValue(null, target);
    }
}
