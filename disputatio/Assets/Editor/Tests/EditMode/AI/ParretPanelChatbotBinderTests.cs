using NUnit.Framework;

public class ParretPanelChatbotBinderTests
{
    [Test]
    public void ResolveChatbotType_ReturnsGlobalChatbot_ForMainHallPlayable()
    {
        Assert.AreSame(
            typeof(GlobalChatbot),
            ParretPanelChatbotBinder.ResolveChatbotType("Hall_playerble"));
    }

    [Test]
    public void ResolveChatbotType_ReturnsRoomChatbot_ForMappedRoomScenes()
    {
        Assert.AreSame(typeof(WifeRoomChatbot), ParretPanelChatbotBinder.ResolveChatbotType("WifeRoom"));
        Assert.AreSame(typeof(SonRoomChatbot), ParretPanelChatbotBinder.ResolveChatbotType("ChildRoom"));
        Assert.AreSame(typeof(MainBedroomChatbot), ParretPanelChatbotBinder.ResolveChatbotType("BedRoom"));
        Assert.AreSame(typeof(KitchenChatbot), ParretPanelChatbotBinder.ResolveChatbotType("Kitchen"));
        Assert.AreSame(typeof(TutorChatbot), ParretPanelChatbotBinder.ResolveChatbotType("TutorRoom"));
    }

    [Test]
    public void ResolveChatbotType_FallsBackToGlobalChatbot_ForUnknownScene()
    {
        Assert.AreSame(
            typeof(GlobalChatbot),
            ParretPanelChatbotBinder.ResolveChatbotType("SomeOtherScene"));
    }
}
