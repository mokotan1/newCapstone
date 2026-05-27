using NUnit.Framework;

[TestFixture]
public class BackNavigatorTests
{
    [TestCase("MaidRoom", "Hallway_Right")]
    [TestCase("StudyRoom", "Hallway_Right")]
    [TestCase("PrisonEntrance", "StudyRoom")]
    [TestCase("BedRoom", "2floorHallway_Right")]
    [TestCase("WifeRoom", "2floorHallway_Right")]
    [TestCase("TutorRoom", "2floorHallway_Left")]
    [TestCase("ChildRoom", "2floorHallway_Left")]
    public void TryResolveFixedReturnScene_MapsRoomScenesToApprovedDestinations(string currentSceneName, string expectedDestination)
    {
        Assert.IsTrue(BackNavigator.TryResolveFixedReturnScene(currentSceneName, out string destination));
        Assert.AreEqual(expectedDestination, destination);
    }

    [Test]
    public void TryResolveFixedReturnScene_ReturnsFalseForUnmappedScenes()
    {
        Assert.IsFalse(BackNavigator.TryResolveFixedReturnScene("Kitchen", out string destination));
        Assert.AreEqual(string.Empty, destination);
    }
}
