using System.Text.RegularExpressions;
using Fungus;
using NUnit.Framework;
using UnityEngine;

public class ItemAcquisitionTrackerTests
{
    private static readonly Regex Hangul = new Regex(
        @"[\u1100-\u11FF\u3130-\u318F\uAC00-\uD7A3]",
        RegexOptions.Compiled);

    private GameObject flowchartObject;
    private Flowchart flowchart;

    [SetUp]
    public void SetUp()
    {
        flowchartObject = new GameObject("ItemAcquisitionTrackerTestFlowchart");
        flowchart = flowchartObject.AddComponent<Flowchart>();
        AddIntegerVariable(flowchart, ItemAcquisitionTracker.FungusVariableKey, 0);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(flowchartObject);
    }

    [TestCase(1, FungusVariableKeys.GetBottle, "Bottle")]
    [TestCase(2, FungusVariableKeys.GetFood, "Food")]
    [TestCase(4, FungusVariableKeys.GetFilterCard, "FilterCard")]
    [TestCase(17, FungusVariableKeys.GetBookmarkMirror, "BookmarkMirror")]
    [TestCase(21, FungusVariableKeys.GetBibleCommentary, "BibleCommentary")]
    [TestCase(8, FungusVariableKeys.HaveMaidKey, "MaidRoom_Key")]
    [TestCase(12, FungusVariableKeys.HaveBasementKey, "BasementKey")]
    [TestCase(19, FungusVariableKeys.HasBible, "Illustrated Bible")]
    public void MarkAcquired_SetsLinkedFungusBool_WhenItemHasAcquisitionFlag(
        int itemId,
        string boolKey,
        string itemName)
    {
        AddBooleanVariable(flowchart, boolKey, false);
        Item item = CreateItem(itemId, itemName);

        ItemAcquisitionTracker.MarkAcquired(flowchart, item);

        Assert.IsTrue(ItemAcquisitionTracker.IsAcquired(flowchart, itemId));
        Assert.IsTrue(flowchart.GetBooleanVariable(boolKey));

        Object.DestroyImmediate(item);
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void BuildPromptSection_EmptyMask_NonKorean_HasNoHangul(string locale)
    {
        string section = ItemAcquisitionTracker.BuildPromptSection(flowchart, locale);
        Assert.IsFalse(Hangul.IsMatch(section), section);
        Assert.IsFalse(section.Contains("[진행]"), section);
    }

    [Test]
    public void BuildPromptSection_EmptyMask_Korean_KeepsProgressLabel()
    {
        string section = ItemAcquisitionTracker.BuildPromptSection(
            flowchart, CheshireLocaleResolver.Korean);
        StringAssert.Contains("[진행]", section);
    }

    [TestCase(CheshireLocaleResolver.English)]
    [TestCase(CheshireLocaleResolver.Japanese)]
    public void BuildPromptSection_WithItem_NonKorean_HasNoHangulChrome(string locale)
    {
        AddBooleanVariable(flowchart, FungusVariableKeys.GetBottle, false);
        Item item = CreateItem(1, "Bottle");
        ItemAcquisitionTracker.MarkAcquired(flowchart, item);

        string section = ItemAcquisitionTracker.BuildPromptSection(flowchart, locale);
        Assert.IsFalse(Hangul.IsMatch(section), section);
        StringAssert.Contains("Bottle", section);
        Assert.IsFalse(section.Contains("[진행 안내]"), section);

        Object.DestroyImmediate(item);
    }

    private static Item CreateItem(int itemId, string itemName)
    {
        Item item = ScriptableObject.CreateInstance<Item>();
        item.itemId = itemId;
        item.itemName = itemName;
        return item;
    }

    private static void AddBooleanVariable(Flowchart target, string key, bool value)
    {
        var variable = target.gameObject.AddComponent<BooleanVariable>();
        variable.Key = key;
        variable.Value = value;
        target.Variables.Add(variable);
    }

    private static void AddIntegerVariable(Flowchart target, string key, int value)
    {
        var variable = target.gameObject.AddComponent<IntegerVariable>();
        variable.Key = key;
        variable.Value = value;
        target.Variables.Add(variable);
    }
}
