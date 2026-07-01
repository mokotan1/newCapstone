using NUnit.Framework;

[TestFixture]
public class CheshireHintRewritePlannerTests
{
    [Test]
    public void TryBuildBottleUseHint_WhenPlayerHasBottleAndAsksUse_ReturnsPayload()
    {
        bool ok = CheshireHintRewritePlanner.TryBuildBottleUseHint(
            "이 병 어디다 써?",
            hasBottle: true,
            out HintRewritePayload payload);

        Assert.IsTrue(ok);
        Assert.IsNotNull(payload);
        Assert.AreEqual("bottle_sink_use", payload.hint_id);
        Assert.AreEqual("bottle", payload.item_id);
        Assert.AreEqual("kitchen_sink", payload.hint_target);
        Assert.AreEqual("direct", payload.hint_level);
        Assert.AreEqual("그 병은 주방 싱크대에서 사용할 수 있다.", payload.base_hint);
        CollectionAssert.Contains(payload.required_terms, "병");
        CollectionAssert.Contains(payload.required_terms, "싱크대");
        CollectionAssert.Contains(payload.forbidden_terms, "열쇠");
        Assert.AreEqual("item_usage_hint", payload.interaction_type);
    }

    [Test]
    public void TryBuildBottleUseHint_WhenPlayerDoesNotHaveBottle_ReturnsFalse()
    {
        bool ok = CheshireHintRewritePlanner.TryBuildBottleUseHint(
            "이 병 어디다 써?",
            hasBottle: false,
            out HintRewritePayload payload);

        Assert.IsFalse(ok);
        Assert.IsNull(payload);
    }

    [Test]
    public void TryBuildBottleUseHint_WhenMessageIsNotUseQuestion_ReturnsFalse()
    {
        bool ok = CheshireHintRewritePlanner.TryBuildBottleUseHint(
            "안녕 체셔",
            hasBottle: true,
            out HintRewritePayload payload);

        Assert.IsFalse(ok);
        Assert.IsNull(payload);
    }
}
