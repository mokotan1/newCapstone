using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class BloodPoolSpreadTests
{
    GameObject root;

    [TearDown]
    public void TearDown()
    {
        if (root != null)
            Object.DestroyImmediate(root);
    }

    [Test]
    public void ClampSpreadWidth_RespectsMax()
    {
        Assert.AreEqual(80f, BloodPoolSpreadPolicy.ClampSpreadWidth(120f, 80f), 0.0001f);
        Assert.AreEqual(40f, BloodPoolSpreadPolicy.ClampSpreadWidth(40f, 80f), 0.0001f);
    }

    [Test]
    public void ComputeEvictionCount_TrimsWhenOverLimit()
    {
        Assert.AreEqual(0, BloodPoolSpreadPolicy.ComputeEvictionCount(3, 8));
        Assert.AreEqual(2, BloodPoolSpreadPolicy.ComputeEvictionCount(10, 8));
        Assert.AreEqual(10, BloodPoolSpreadPolicy.ComputeEvictionCount(10, 0));
    }

    [Test]
    public void RegisterImpact_SpawnsSpreadStainsWhenPoolEnabled()
    {
        var pool = CreatePool();
        pool.SetPoolEnabled(true);
        pool.SetSpreadEnabledForTests(true);
        pool.SetSpreadBurstsPerImpactForTests(2);

        pool.RegisterImpactForTests(Vector2.zero, 8f);

        Assert.GreaterOrEqual(pool.ActiveSpreadStainCountForTests, 2);
    }

    [Test]
    public void RegisterImpact_PoolDisabled_DoesNotSpawnSpreadStains()
    {
        var pool = CreatePool();
        pool.SetPoolEnabled(false);
        pool.SetSpreadEnabledForTests(true);
        pool.SetSpreadBurstsPerImpactForTests(2);

        pool.RegisterImpactForTests(Vector2.zero, 8f);

        Assert.AreEqual(0, pool.ActiveSpreadStainCountForTests);
    }

    [Test]
    public void ResetPool_ClearsSpreadStains()
    {
        var pool = CreatePool();
        pool.SetPoolEnabled(true);
        pool.SetSpreadEnabledForTests(true);
        pool.SetSpreadBurstsPerImpactForTests(2);

        pool.RegisterImpactForTests(Vector2.zero, 8f);
        Assert.Greater(pool.ActiveSpreadStainCountForTests, 0);

        pool.ResetPool();
        Assert.AreEqual(0, pool.ActiveSpreadStainCountForTests);
    }

    [Test]
    public void RegisterImpact_EvictsOldestSpreadStainsWhenOverLimit()
    {
        var pool = CreatePool();
        pool.SetPoolEnabled(true);
        pool.SetSpreadEnabledForTests(true);
        pool.SetMaxRetainedSpreadStainsForTests(3);
        pool.SetSpreadBurstsPerImpactForTests(2);

        pool.RegisterImpactForTests(new Vector2(-20f, 0f), 8f);
        pool.RegisterImpactForTests(new Vector2(0f, 0f), 8f);
        pool.RegisterImpactForTests(new Vector2(20f, 0f), 8f);

        Assert.AreEqual(3, pool.ActiveSpreadStainCountForTests);
    }

    BloodPool CreatePool()
    {
        root = new GameObject("BloodPoolSpreadTests");
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(root.transform, false);

        var poolGo = new GameObject("BloodPool", typeof(RectTransform), typeof(BloodPool));
        poolGo.transform.SetParent(canvasGo.transform, false);
        return poolGo.GetComponent<BloodPool>();
    }
}
