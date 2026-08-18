using NUnit.Framework;

public class KitchenChatbotTests
{
    [Test]
    public void KitchenGiveFoodSecret_English_SubstitutesPages_NoPlaceholdersOrKoreanDesignerHeader()
    {
        string block = CheshireDynamicPromptFragments.KitchenGiveFoodSecret(
            CheshireLocaleResolver.English, 18, 19);

        StringAssert.Contains("18", block);
        StringAssert.Contains("19", block);
        Assert.IsFalse(block.Contains("{pageStart}"), block);
        Assert.IsFalse(block.Contains("{pageEnd}"), block);
        Assert.IsFalse(block.Contains("[설계자 전용]"), block);
    }

    [Test]
    public void BuildGiveFoodSecretDesignBlock_English_DelegatesToFragment()
    {
        string block = KitchenChatbot.BuildGiveFoodSecretDesignBlock(
            CheshireLocaleResolver.English, 18, 19);

        StringAssert.Contains("18", block);
        StringAssert.Contains("19", block);
        Assert.IsFalse(block.Contains("{pageStart}"), block);
        Assert.IsFalse(block.Contains("[설계자 전용]"), block);
    }

    [Test]
    public void KitchenGiveFoodActionText_Korean_ContainsFoodGivenPhrase()
    {
        string text = CheshireDynamicPromptFragments.KitchenGiveFoodActionText(
            CheshireLocaleResolver.Korean);

        StringAssert.Contains("음식을 주었다", text);
    }

    [Test]
    public void KitchenGiveFoodActionText_English_DoesNotContainKoreanActionPhrase()
    {
        string text = CheshireDynamicPromptFragments.KitchenGiveFoodActionText(
            CheshireLocaleResolver.English);

        Assert.IsFalse(text.Contains("음식을 주었다"), text);
        StringAssert.Contains("food", text.ToLowerInvariant());
    }

    [Test]
    public void KitchenGiveFoodActionText_Japanese_DoesNotContainKoreanActionPhrase()
    {
        string text = CheshireDynamicPromptFragments.KitchenGiveFoodActionText(
            CheshireLocaleResolver.Japanese);

        Assert.IsFalse(text.Contains("음식을 주었다"), text);
        StringAssert.Contains("プレイヤー", text);
    }
}
