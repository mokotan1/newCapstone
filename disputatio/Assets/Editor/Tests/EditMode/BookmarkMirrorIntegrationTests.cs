using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public class BookmarkMirrorIntegrationTests
{
    const string BookmarkMirrorAssetPath = "Assets/godlotto/Item/BookmarkMirror.asset";
    const string FilterCardAssetPath = "Assets/godlotto/Item/FilterCard.asset";
    const string PuzzleBookTextPath = "Assets/Resources/MaidRoomPuzzleBook.txt";
    const string TooltipTablePath = "Assets/Resources/Scenario/item_tooltip_table.csv";
    const string StudyRoomScenePath = "Assets/Scenes/Mokotan/First Floor/1floorRight/StudyRoom.unity";
    const string MaidRoomScenePath = "Assets/Scenes/Mokotan/First Floor/1floorRight/MaidRoom.unity";

    const string BookmarkMirrorGuid = "877726e16099412aaf58c39b648f843d";
    const string FilterCardGuid = "fdbb615d89f38ee478245637d6a26e32";
    const string FilterCardBookDropZoneGuid = "d642306d1d264999bb405247b88e52a6";
    const string DropZoneGuid = "4f55531d3b5b3ca469012281fbf096c3";
    const string PuzzleBookPageItemGateGuid = "7a3c9e1d4f8b2a6c5d0e4f1b8c2a9d3e";

    [TearDown]
    public void TearDown()
    {
        ItemRegistry.ResetCacheForTest();
    }

    [Test]
    public void BookmarkMirrorAsset_Exists_AndHasIconAssigned()
    {
        Item bookmarkMirror = AssetDatabase.LoadAssetAtPath<Item>(BookmarkMirrorAssetPath);

        Assert.IsNotNull(bookmarkMirror, "BookmarkMirror.asset must exist.");
        Assert.AreEqual("BookmarkMirror", bookmarkMirror.itemName);
        Assert.AreEqual(17, bookmarkMirror.itemId);
        Assert.IsNotNull(bookmarkMirror.icon, "BookmarkMirror icon sprite must be assigned.");
    }

    [Test]
    public void MaidRoomPuzzleBook_IncludesBookmarkMirrorPage()
    {
        string text = File.ReadAllText(PuzzleBookTextPath);

        Assert.IsTrue(text.Contains("반쪽 숫자"), "Page 2 title should describe the half-revealed code.");
        Assert.IsTrue(text.Contains("책갈피 거울"), "Page 2 should mention the bookmark mirror pickup.");
        Assert.IsTrue(text.Contains("서재로 가서"), "Page 2 should link the mirror to the StudyRoom puzzle.");
    }

    [Test]
    public void TooltipTable_HasBookmarkMirrorRows()
    {
        string csv = File.ReadAllText(TooltipTablePath);
        var rows = csv.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("17,BookmarkMirror,"))
            .ToList();

        Assert.GreaterOrEqual(rows.Count, 3, "Tooltip table should include acquisition, usage, and description rows.");
        Assert.IsTrue(rows.Any(row => row.Contains("책갈피 거울")));
        Assert.IsTrue(rows.Any(row => row.Contains(BookmarkMirrorAssetPath)));
    }

    [Test]
    public void StudyRoomScene_DiaryMirrorDropZone_RequiresBookmarkMirror_NotFilterCard()
    {
        string sceneYaml = File.ReadAllText(StudyRoomScenePath);
        string mirrorDropZoneBlock = ExtractMonoBehaviourBlock(sceneYaml, FilterCardBookDropZoneGuid);

        Assert.IsNotNull(mirrorDropZoneBlock, "StudyRoom must contain a FilterCardBookDropZone for the diary mirror puzzle.");
        Assert.IsTrue(
            mirrorDropZoneBlock.Contains($"guid: {BookmarkMirrorGuid}"),
            "Diary mirror drop zone must require BookmarkMirror.");
        Assert.IsFalse(
            mirrorDropZoneBlock.Contains($"guid: {FilterCardGuid}"),
            "Diary mirror drop zone must not require FilterCard.");
        Assert.IsTrue(
            mirrorDropZoneBlock.Contains("consumeItemOnDrop: 0"),
            "BookmarkMirror should remain reusable after dropping it on the diary clue.");
    }

    [Test]
    public void MaidRoomScene_BookmarkMirrorPickup_AppearsOnPuzzleBookFirstPage()
    {
        string sceneYaml = File.ReadAllText(MaidRoomScenePath);
        string gateBlock = ExtractMonoBehaviourBlock(sceneYaml, PuzzleBookPageItemGateGuid);

        Assert.IsNotNull(gateBlock, "MaidRoom PuzzlePanel must gate the BookmarkMirror pickup by puzzle book page.");
        Assert.IsTrue(
            gateBlock.Contains("visibleOnPageIndex: 0"),
            "BookmarkMirror should be obtainable from the first puzzle book page.");
        Assert.IsTrue(
            gateBlock.Contains("pickupObject: {fileID: 1892094432}"),
            "The puzzle book page gate should control the BookmarkMirror pickup object.");
    }

    [Test]
    public void StudyRoomScene_BookshelfDropZones_StillRequireFilterCard()
    {
        string sceneYaml = File.ReadAllText(StudyRoomScenePath);
        var bookshelfBlocks = ExtractAllMonoBehaviourBlocks(sceneYaml, DropZoneGuid);

        Assert.GreaterOrEqual(bookshelfBlocks.Count, 1, "StudyRoom bookshelf drop zones should remain configured.");
        foreach (string block in bookshelfBlocks)
        {
            Assert.IsTrue(
                block.Contains($"guid: {FilterCardGuid}"),
                "Bookshelf/word-card drop zones must still require FilterCard.");
            Assert.IsFalse(
                block.Contains($"guid: {BookmarkMirrorGuid}"),
                "Bookshelf/word-card drop zones must not require BookmarkMirror.");
        }
    }

    [Test]
    public void ItemLookup_FindById17_ReturnsBookmarkMirror()
    {
        Item item = ItemLookup.FindById(17);

        Assert.IsNotNull(item);
        Assert.AreEqual("BookmarkMirror", item.itemName);
        Assert.IsNotNull(item.icon);
    }

    static string ExtractMonoBehaviourBlock(string sceneYaml, string scriptGuid)
    {
        return ExtractAllMonoBehaviourBlocks(sceneYaml, scriptGuid).FirstOrDefault();
    }

    static System.Collections.Generic.List<string> ExtractAllMonoBehaviourBlocks(string sceneYaml, string scriptGuid)
    {
        var blocks = new System.Collections.Generic.List<string>();
        string marker = "guid: " + scriptGuid + ", type: 3}";
        int searchIndex = 0;

        while (true)
        {
            int scriptIndex = sceneYaml.IndexOf(marker, searchIndex, System.StringComparison.Ordinal);
            if (scriptIndex < 0)
                break;

            int blockStart = sceneYaml.LastIndexOf("--- !u!114", scriptIndex, System.StringComparison.Ordinal);
            int blockEnd = sceneYaml.IndexOf("--- !u!", scriptIndex + marker.Length, System.StringComparison.Ordinal);
            if (blockStart < 0)
                break;

            string block = blockEnd < 0
                ? sceneYaml.Substring(blockStart)
                : sceneYaml.Substring(blockStart, blockEnd - blockStart);

            blocks.Add(block);
            searchIndex = scriptIndex + marker.Length;
        }

        return blocks;
    }
}
