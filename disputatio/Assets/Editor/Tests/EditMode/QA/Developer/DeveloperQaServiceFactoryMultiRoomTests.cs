#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

/// <summary>
/// Wave 1/2: factory must register StudyRoom, Kitchen, MainMenu, MaidRoom, and Hall.
/// </summary>
[TestFixture]
public sealed class DeveloperQaServiceFactoryMultiRoomTests
{
    [Test]
    public void Create_RegistersStudyRoomKitchenAndMainMenuCapabilities()
    {
        IDeveloperQaService service = DeveloperQaServiceFactory.Create();
        var ids = service.ListCapabilities().Select(c => c.Id).ToArray();

        Assert.That(ids, Does.Contain("studyroom.mirror.probe"));
        Assert.That(ids, Does.Contain("kitchen.faucet.click"));
        Assert.That(ids, Does.Contain("mainmenu.start.click"));
    }

    [Test]
    public void Create_RegistersMaidRoomAndHallCapabilities()
    {
        IDeveloperQaService service = DeveloperQaServiceFactory.Create();
        var ids = service.ListCapabilities().Select(c => c.Id).ToArray();

        Assert.That(ids, Does.Contain("maidroom.food.click-tray"));
        Assert.That(ids, Does.Contain("maidroom.food.assert-effect"));
        Assert.That(ids, Does.Contain("hall.nav.click-kitchen-entry"));
        Assert.That(ids, Does.Contain("hall.nav.assert-route"));
    }
}
#endif
