using NUnit.Framework;

public class DeveloperModeItemPickerGuiTests
{
    [Test]
    public void TryParseQuantity_RejectsZeroAndNonNumeric()
    {
        Assert.IsFalse(DeveloperModeItemPickerGui.TryParseQuantity("0", out _));
        Assert.IsFalse(DeveloperModeItemPickerGui.TryParseQuantity("abc", out _));
        Assert.IsFalse(DeveloperModeItemPickerGui.TryParseQuantity(string.Empty, out _));
    }

    [Test]
    public void TryParseQuantity_AcceptsPositiveInteger()
    {
        Assert.IsTrue(DeveloperModeItemPickerGui.TryParseQuantity("3", out int quantity));
        Assert.AreEqual(3, quantity);
    }

    [Test]
    public void TryParseQuantity_ClampsToMax()
    {
        Assert.IsTrue(DeveloperModeItemPickerGui.TryParseQuantity("500", out int quantity));
        Assert.AreEqual(DeveloperModeItemGrantService.MaxGrantQuantity, quantity);
    }
}
