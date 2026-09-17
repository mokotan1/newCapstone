#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

/// <summary>
/// AC07/AC08: intermediate Hall hops are named Fungus blocks, not a Kitchen shortcut.
/// </summary>
public class HallQaFungusHopTests
{
    [Test]
    public void BlockNameForScene_HallLeft_IsFrontClicked()
    {
        Assert.AreEqual("Front_clicked", HallQaFungusHop.BlockNameForScene(SceneNames.HallLeft));
    }

    [Test]
    public void BlockNameForScene_HallLeft2_IsDoorClicked()
    {
        Assert.AreEqual("Door_Clicked", HallQaFungusHop.BlockNameForScene(SceneNames.HallLeft2));
    }

    [Test]
    public void BlockNameForScene_HallPlayable_HasNoFungusHop()
    {
        Assert.IsNull(HallQaFungusHop.BlockNameForScene(SceneNames.HallPlayable));
    }

    [Test]
    public void MapExecute_OutsidePlayMode_IsEnvironmentBlocked()
    {
        DeveloperQaResult result = HallQaFungusHop.MapExecute(HallQaFungusHop.FrontBlockName);
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
        StringAssert.Contains("Play Mode", result.Message);
    }

    [Test]
    public void MapResetToHall_OutsidePlayMode_IsEnvironmentBlocked()
    {
        DeveloperQaResult result = HallQaFungusHop.MapResetToHall();
        Assert.AreEqual(DeveloperQaResultCode.EnvironmentBlocked, result.Code);
        StringAssert.Contains("Play Mode", result.Message);
    }
}
#endif
