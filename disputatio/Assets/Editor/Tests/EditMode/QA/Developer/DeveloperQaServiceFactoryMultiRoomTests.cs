#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using Godlotto.QA.Developer;
using Godlotto.QA.SceneAdapters;
using NUnit.Framework;

/// <summary>
/// Wave 1/2/3: factory must register StudyRoom, Kitchen, MainMenu, MaidRoom, Hall,
/// ChildRoom, WifeRoom, and BedRoom.
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

    [Test]
    public void Create_RegistersChildWifeAndBedCapabilities()
    {
        IDeveloperQaService service = DeveloperQaServiceFactory.Create();
        var ids = service.ListCapabilities().Select(c => c.Id).ToArray();

        Assert.That(ids, Does.Contain("childroom.seals.click-seal5"));
        Assert.That(ids, Does.Contain("childroom.seals.assert-controller"));
        Assert.That(ids, Does.Contain("wiferoom.wallclock.click"));
        Assert.That(ids, Does.Contain("wiferoom.wallclock.assert-controller"));
        Assert.That(ids, Does.Contain("bedroom.book.click"));
        Assert.That(ids, Does.Contain("bedroom.book.assert-controller"));
    }
}
#endif
