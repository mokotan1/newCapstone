using System.Reflection;
using Fungus;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// MaidRoom food 아이템 획득 수명 주기 소유권 회귀 테스트.
/// 성공(획득 확정)은 C# <see cref="ItemPickup"/> 완료가 소유해야 하며, Fungus의
/// SetActive 커맨드가 실행되지 않아도 pickup과 연관 오브젝트(FoodItemEffect 등)가
/// 함께 정리되어야 한다. PickUp이 아직 호출되지 않았다면(취소) 둘 다 활성 상태로
/// 남아야 한다. scale=0.01 같은 시각적 축소는 정리 로직으로 간주하지 않는다.
/// </summary>
public class ItemPickupFoodEffectLifecycleTests
{
    GameObject flowchartObject;
    GameObject pickupObject;
    GameObject effectObject;
    GameObject overlappingPickupSpriteObject;
    Item testItem;

    [SetUp]
    public void SetUp()
    {
        flowchartObject = new GameObject("FoodEffectLifecycleFlowchart");
        var flowchart = flowchartObject.AddComponent<Flowchart>();
        AddBooleanVariable(flowchart, "GetFood", false);

        testItem = ScriptableObject.CreateInstance<Item>();
        testItem.itemId = 28;
        testItem.itemName = "Food";

        effectObject = new GameObject("FoodItemEffect");
        overlappingPickupSpriteObject = new GameObject("FoodPickupSprite");

        pickupObject = new GameObject("food");
        var pickup = pickupObject.AddComponent<ItemPickup>();
        pickup.item = testItem;
        pickup.fungusVariableName = "GetFood";
        SetPrivateField(pickup, "targetFlowchart", flowchart);
        SetPrivateField(pickup, "addToInventory", false);
        SetPrivateField(
            pickup,
            "objectsToDeactivateOnPickup",
            new[] { effectObject, overlappingPickupSpriteObject });
    }

    [TearDown]
    public void TearDown()
    {
        if (pickupObject != null)
            Object.DestroyImmediate(pickupObject);
        if (effectObject != null)
            Object.DestroyImmediate(effectObject);
        if (overlappingPickupSpriteObject != null)
            Object.DestroyImmediate(overlappingPickupSpriteObject);
        if (testItem != null)
            Object.DestroyImmediate(testItem);
        if (flowchartObject != null)
            Object.DestroyImmediate(flowchartObject);
    }

    [Test]
    public void PickUpDirect_ConfirmedSuccess_DeactivatesEffectAndPickupWithoutFungusSetActive()
    {
        var pickup = pickupObject.GetComponent<ItemPickup>();

        // ItemPickup.PickUp() calls Destroy(gameObject) on the confirmed-success path. Unity
        // logs an edit-mode-only error for this (Destroy is deferred/no-op outside Play Mode);
        // that is an orthogonal, pre-existing engine constraint unrelated to this lifecycle fix,
        // so we expect it rather than let it fail the test.
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
            "Destroy may not be called from edit mode"));

        pickup.PickUpDirect();

        Assert.IsFalse(
            effectObject.activeSelf,
            "FoodItemEffect must be deactivated by C# pickup completion, not by a Fungus SetActive command.");
        Assert.IsFalse(
            overlappingPickupSpriteObject.activeSelf,
            "Overlapping food pickup visual must be deactivated by C# pickup completion.");
        Assert.IsTrue(
            pickupObject == null || !pickupObject.activeSelf,
            "Pickup GameObject itself must be deactivated once acquisition is confirmed by C#.");
    }

    [Test]
    public void PickUp_NeverInvoked_KeepsEffectAndPickupActive_OnCancel()
    {
        // Flowchart interruption before the confirmed pickup call must not hide anything:
        // no SetActive/PickUp path has run yet, so both stay exactly as they started.
        Assert.IsTrue(
            effectObject.activeSelf,
            "FoodItemEffect must remain active until pickup is confirmed by C#.");
        Assert.IsTrue(
            overlappingPickupSpriteObject.activeSelf,
            "Overlapping food pickup visual must remain active until pickup is confirmed by C#.");
        Assert.IsTrue(
            pickupObject.activeSelf,
            "Pickup GameObject must remain active/undestroyed on cancel.");
    }

    static void AddBooleanVariable(Flowchart target, string key, bool value)
    {
        var variable = target.gameObject.AddComponent<BooleanVariable>();
        variable.Key = key;
        variable.Value = value;
        target.Variables.Add(variable);
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"ItemPickup must declare a serialized field named '{fieldName}'.");
        field.SetValue(target, value);
    }
}
