using System.Reflection;
using Fungus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ItemPickupSuppressTests
{
    GameObject flowchartObject;
    GameObject pickupObject;
    Item testItem;

    [SetUp]
    public void SetUp()
    {
        flowchartObject = new GameObject("ItemPickupSuppressFlowchart");
        var flowchart = flowchartObject.AddComponent<Flowchart>();
        AddIntegerVariable(flowchart, ItemAcquisitionTracker.FungusVariableKey, 0);
        AddBooleanVariable(flowchart, FungusVariableKeys.HaveMaidKey, true);

        testItem = ScriptableObject.CreateInstance<Item>();
        testItem.itemId = 8;
        testItem.itemName = "MaidRoom_Key";

        pickupObject = new GameObject("MaidRoomKey");
        var pickup = pickupObject.AddComponent<ItemPickup>();
        pickup.item = testItem;
        pickup.fungusVariableName = FungusVariableKeys.HaveMaidKey;
        SetPrivateField(pickup, "targetFlowchart", flowchart);
    }

    [TearDown]
    public void TearDown()
    {
        if (pickupObject != null)
            Object.DestroyImmediate(pickupObject);
        if (testItem != null)
            Object.DestroyImmediate(testItem);
        if (flowchartObject != null)
            Object.DestroyImmediate(flowchartObject);
    }

    [Test]
    public void Start_SuppressIfAlreadyTaken_LogsAndDestroysWhenHaveMaidKeyTrue()
    {
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
            @"\[ItemPickup\] Destroy 'MaidRoomKey': Fungus bool 'HaveMaidKey' is already true\."));

        InvokeStart(pickupObject.GetComponent<ItemPickup>());

        Assert.IsTrue(pickupObject == null, "MaidRoomKey should be destroyed when HaveMaidKey is already true.");
    }

    [Test]
    public void Start_SuppressIfAlreadyTaken_LogsAndDestroysWhenItemAlreadyAcquired()
    {
        var flowchart = flowchartObject.GetComponent<Flowchart>();
        flowchart.SetBooleanVariable(FungusVariableKeys.HaveMaidKey, false);
        ItemAcquisitionTracker.MarkAcquired(flowchart, testItem);

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
            @"\[ItemPickup\] Destroy 'MaidRoomKey': itemId 8 already in AcquiredItemsMask\."));

        InvokeStart(pickupObject.GetComponent<ItemPickup>());

        Assert.IsTrue(pickupObject == null, "MaidRoomKey should be destroyed when itemId is already acquired.");
    }

    static void InvokeStart(ItemPickup pickup)
    {
        MethodInfo start = typeof(ItemPickup).GetMethod(
            "Start",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(start);
        start.Invoke(pickup, null);
    }

    static void AddBooleanVariable(Flowchart target, string key, bool value)
    {
        var variable = target.gameObject.AddComponent<BooleanVariable>();
        variable.Key = key;
        variable.Value = value;
        target.Variables.Add(variable);
    }

    static void AddIntegerVariable(Flowchart target, string key, int value)
    {
        var variable = target.gameObject.AddComponent<IntegerVariable>();
        variable.Key = key;
        variable.Value = value;
        target.Variables.Add(variable);
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }
}
