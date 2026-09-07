using NUnit.Framework;

[TestFixture]
public class CheshireLiveStreamDisplayTests
{
    [Test]
    public void Append_EmptyCurrent_ReturnsDelta()
    {
        Assert.AreEqual("첫 글자", CheshireLiveStreamDisplay.Append("", "첫 글자"));
        Assert.AreEqual("첫 글자", CheshireLiveStreamDisplay.Append(null, "첫 글자"));
    }

    [Test]
    public void Append_ConcatenatesChunks()
    {
        Assert.AreEqual("안녕, 체셔.", CheshireLiveStreamDisplay.Append("안녕, ", "체셔."));
    }

    [Test]
    public void Append_IgnoresNullOrEmptyDelta()
    {
        Assert.AreEqual("유지", CheshireLiveStreamDisplay.Append("유지", null));
        Assert.AreEqual("유지", CheshireLiveStreamDisplay.Append("유지", ""));
    }
}
