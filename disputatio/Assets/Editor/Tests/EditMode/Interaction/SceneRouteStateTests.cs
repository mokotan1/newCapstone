using NUnit.Framework;

public class SceneRouteStateTests
{
    [TearDown]
    public void TearDown()
    {
        SceneRouteState.ResetForTests();
    }

    [Test]
    public void RecordDepartedScene_MakesTheMostRecentNonBlankSceneAvailableForBackNavigation()
    {
        SceneRouteState.RecordDepartedScene("Hallway_Left");
        SceneRouteState.RecordDepartedScene("Kitchen");

        Assert.That(SceneRouteState.TryGetPreviousScene(out string previous), Is.True);
        Assert.That(previous, Is.EqualTo("Kitchen"));
    }

    [Test]
    public void RecordDepartedScene_BlankNameDoesNotReplaceThePreviousScene()
    {
        SceneRouteState.RecordDepartedScene("Hallway_Left");
        SceneRouteState.RecordDepartedScene(" ");

        Assert.That(SceneRouteState.TryGetPreviousScene(out string previous), Is.True);
        Assert.That(previous, Is.EqualTo("Hallway_Left"));
    }
}
