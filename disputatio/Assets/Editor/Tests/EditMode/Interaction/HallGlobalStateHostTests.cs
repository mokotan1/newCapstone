using NUnit.Framework;
using UnityEngine;

[TestFixture]
public sealed class HallGlobalStateHostTests
{
    [TearDown]
    public void TearDown()
    {
        HallGlobalStateHost.ResetForTests();
    }

    [Test]
    public void EnsureInstance_IsSoleDontDestroyHost()
    {
        HallGlobalStateHost first = HallGlobalStateHost.EnsureInstance();
        HallGlobalStateHost second = HallGlobalStateHost.EnsureInstance();
        Assert.AreSame(first, second);
        Assert.IsTrue(HallGlobalStateHost.Exists);
    }

    [Test]
    public void SetBool_GetBool_RoundTripsWithoutFungus()
    {
        HallGlobalStateHost host = HallGlobalStateHost.EnsureInstance();
        host.SetBool("HaveBasementKey", true);
        Assert.IsTrue(host.GetBool("HaveBasementKey"));
        host.SetBool("HaveBasementKey", false);
        Assert.IsFalse(host.GetBool("HaveBasementKey"));
    }

    [Test]
    public void PrevScene_StringRoundTrip()
    {
        HallGlobalStateHost host = HallGlobalStateHost.EnsureInstance();
        host.SetString(HallGlobalStateKeys.PrevScene, SceneNames.HallPlayable);
        Assert.AreEqual(SceneNames.HallPlayable, host.GetString(HallGlobalStateKeys.PrevScene));
    }
}
