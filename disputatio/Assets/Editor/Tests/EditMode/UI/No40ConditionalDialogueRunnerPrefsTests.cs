using System.Linq;
using NUnit.Framework;

[TestFixture]
public sealed class No40ConditionalDialogueRunnerPrefsTests
{
    [Test]
    public void PrefsKeys_AreDistinctAndNonEmpty()
    {
        string[] keys =
        {
            No40ConditionalDialogueRunner.PrefsKeys.MansionHubVisited,
            No40ConditionalDialogueRunner.PrefsKeys.FirstEntryPlayed,
            No40ConditionalDialogueRunner.PrefsKeys.FirstDeathLinePlayed,
            No40ConditionalDialogueRunner.PrefsKeys.BloodPathLinePlayed,
        };

        Assert.That(keys, Is.All.Not.Null.And.Not.Empty);
        Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Length));
    }
}
