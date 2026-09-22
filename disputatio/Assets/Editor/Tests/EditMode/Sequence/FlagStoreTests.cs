using Godlotto.Sequence;
using NUnit.Framework;
using System;

[TestFixture]
public class FlagStoreTests
{
    [Test]
    public void SetBool_GetBool_RoundTrips()
    {
        var store = new FlagStore();
        store.SetBool("GetBottle", true);
        Assert.That(store.GetBool("GetBottle"), Is.True);
        Assert.That(store.Has("GetBottle"), Is.True);
    }

    [Test]
    public void GetBool_MissingKey_ReturnsDefault()
    {
        var store = new FlagStore();
        Assert.That(store.GetBool("missing"), Is.False);
        Assert.That(store.GetBool("missing", true), Is.True);
    }

    [Test]
    public void SetInt_And_SetString_RoundTrip()
    {
        var store = new FlagStore();
        store.SetInt("CorrectAnswerCount", 3);
        store.SetString("SceneName", "Kitchen");
        Assert.That(store.GetInt("CorrectAnswerCount"), Is.EqualTo(3));
        Assert.That(store.GetString("SceneName"), Is.EqualTo("Kitchen"));
    }

    [Test]
    public void Clear_RemovesAllKeys()
    {
        var store = new FlagStore();
        store.SetBool("GetBottle", true);
        store.Clear();
        Assert.That(store.Has("GetBottle"), Is.False);
    }

    [Test]
    public void SetBool_EmptyKey_Throws()
    {
        var store = new FlagStore();
        Assert.Throws<ArgumentException>(() => store.SetBool("", true));
        Assert.Throws<ArgumentException>(() => store.SetBool(null, true));
    }

    [Test]
    public void SetValueWithDifferentTypeForExistingKey_Throws()
    {
        var store = new FlagStore();
        store.SetBool("shared", true);

        var ex = Assert.Throws<InvalidOperationException>(() => store.SetInt("shared", 1));

        StringAssert.Contains("shared", ex.Message);
        Assert.That(store.GetBool("shared"), Is.True);
    }

    [Test]
    public void GetValueWithDifferentTypeForExistingKey_Throws()
    {
        var store = new FlagStore();
        store.SetString("shared", "Kitchen");

        var ex = Assert.Throws<InvalidOperationException>(() => store.GetBool("shared"));

        StringAssert.Contains("shared", ex.Message);
    }

    [Test]
    public void SetBool_WhitespaceKey_Throws()
    {
        var store = new FlagStore();

        Assert.Throws<ArgumentException>(() => store.SetBool("   ", true));
    }
}
