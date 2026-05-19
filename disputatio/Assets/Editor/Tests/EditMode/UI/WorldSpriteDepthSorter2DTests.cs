using NUnit.Framework;
using UnityEngine;

public class WorldSpriteDepthSorter2DTests
{
    [Test]
    public void SortActiveSceneSprites_PreservesUiSortingLayer()
    {
        AssertSortingLayerOrInconclusive("Ui");

        GameObject go = new GameObject("UiSprite");
        try
        {
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Ui";
            sr.sortingOrder = 25;

            WorldSpriteDepthSorter2D.SortActiveSceneSprites();

            Assert.AreEqual("Ui", sr.sortingLayerName,
                "World depth sorter must not migrate Ui-layer sprites to Default.");
            Assert.AreEqual(25, sr.sortingOrder,
                "World depth sorter must not rewrite sortingOrder of Ui-layer sprites.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void SortActiveSceneSprites_PreservesUiBackGroundSortingLayer()
    {
        AssertSortingLayerOrInconclusive("UiBackGround");

        GameObject go = new GameObject("UiBackGroundSprite");
        try
        {
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "UiBackGround";
            sr.sortingOrder = 7;

            WorldSpriteDepthSorter2D.SortActiveSceneSprites();

            Assert.AreEqual("UiBackGround", sr.sortingLayerName);
            Assert.AreEqual(7, sr.sortingOrder);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void SortActiveSceneSprites_StillSortsDefaultLayerSprites()
    {
        GameObject go = new GameObject("WorldSprite");
        try
        {
            go.transform.position = new Vector3(0f, 2f, 0f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Default";
            sr.sortingOrder = 0;

            WorldSpriteDepthSorter2D.SortActiveSceneSprites();

            Assert.AreEqual("Default", sr.sortingLayerName);
            Assert.AreEqual(10000 - Mathf.RoundToInt(2f * 10f), sr.sortingOrder,
                "Default-layer world sprites must still receive Y-based depth ordering.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void AssertSortingLayerOrInconclusive(string layerName)
    {
        foreach (SortingLayer layer in SortingLayer.layers)
        {
            if (layer.name == layerName)
                return;
        }

        Assert.Inconclusive($"Sorting layer '{layerName}' is not registered in this project's TagManager.");
    }
}
