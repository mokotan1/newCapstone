using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class MenuAnchorLayoutTests
{
    [Test]
    public void Resolve_BottomCenter_AnchorsToBottomMiddle()
    {
        MenuAnchorLayout.Resolve(MenuAnchor.BottomCenter, out var min, out var max, out var pivot);

        Assert.AreEqual(new Vector2(0.5f, 0f), min);
        Assert.AreEqual(new Vector2(0.5f, 0f), max);
        Assert.AreEqual(new Vector2(0.5f, 0f), pivot);
    }

    [Test]
    public void Resolve_Center_AnchorsToMiddle()
    {
        MenuAnchorLayout.Resolve(MenuAnchor.Center, out var min, out var max, out var pivot);

        Assert.AreEqual(new Vector2(0.5f, 0.5f), min);
        Assert.AreEqual(new Vector2(0.5f, 0.5f), max);
        Assert.AreEqual(new Vector2(0.5f, 0.5f), pivot);
    }

    [Test]
    public void Resolve_TopRight_AnchorsToTopRight()
    {
        MenuAnchorLayout.Resolve(MenuAnchor.TopRight, out var min, out var max, out var pivot);

        Assert.AreEqual(new Vector2(1f, 1f), min);
        Assert.AreEqual(new Vector2(1f, 1f), max);
        Assert.AreEqual(new Vector2(1f, 1f), pivot);
    }

    [Test]
    public void Apply_SetsRectTransformAnchorsAndOffset()
    {
        var go = new GameObject("panel", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();

        MenuAnchorLayout.Apply(rt, MenuAnchor.BottomCenter, new Vector2(0f, 120f));

        Assert.AreEqual(new Vector2(0.5f, 0f), rt.anchorMin);
        Assert.AreEqual(new Vector2(0.5f, 0f), rt.pivot);
        Assert.AreEqual(new Vector2(0f, 120f), rt.anchoredPosition);

        Object.DestroyImmediate(go);
    }
}
