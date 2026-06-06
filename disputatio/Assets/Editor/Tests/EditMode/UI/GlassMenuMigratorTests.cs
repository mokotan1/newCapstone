using System;
using System.Collections.Generic;
using Fungus;
using NUnit.Framework;

[TestFixture]
public class GlassMenuMigratorTests
{
    [Test]
    public void FindConsecutiveMenuRuns_SingleMenu_ReturnsOneRun()
    {
        var types = new List<Type>
        {
            typeof(Say),
            typeof(Fungus.Menu),
            typeof(Call),
        };

        var runs = GlassMenuMigrator.FindConsecutiveMenuRuns(types);

        Assert.AreEqual(1, runs.Count);
        Assert.AreEqual(new[] { 1 }, runs[0]);
    }

    [Test]
    public void FindConsecutiveMenuRuns_ConsecutiveMenus_ReturnsOneMergedRun()
    {
        var types = new List<Type>
        {
            typeof(Say),
            typeof(Fungus.Menu),
            typeof(Fungus.Menu),
            typeof(Fungus.Menu),
            typeof(Call),
        };

        var runs = GlassMenuMigrator.FindConsecutiveMenuRuns(types);

        Assert.AreEqual(1, runs.Count);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, runs[0]);
    }

    [Test]
    public void FindConsecutiveMenuRuns_ClearMenuBetweenMenus_SplitsRuns()
    {
        var types = new List<Type>
        {
            typeof(Fungus.Menu),
            typeof(Fungus.ClearMenu),
            typeof(Fungus.Menu),
        };

        var runs = GlassMenuMigrator.FindConsecutiveMenuRuns(types);

        Assert.AreEqual(2, runs.Count);
        CollectionAssert.AreEqual(new[] { 0 }, runs[0]);
        CollectionAssert.AreEqual(new[] { 2 }, runs[1]);
    }

    [Test]
    public void FindConsecutiveMenuRuns_NonMenuBetweenMenus_SplitsRuns()
    {
        var types = new List<Type>
        {
            typeof(Fungus.Menu),
            typeof(Say),
            typeof(Fungus.Menu),
        };

        var runs = GlassMenuMigrator.FindConsecutiveMenuRuns(types);

        Assert.AreEqual(2, runs.Count);
        CollectionAssert.AreEqual(new[] { 0 }, runs[0]);
        CollectionAssert.AreEqual(new[] { 2 }, runs[1]);
    }
}
