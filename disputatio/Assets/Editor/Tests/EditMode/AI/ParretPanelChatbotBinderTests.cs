using NUnit.Framework;
using Fungus;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ParretPanelChatbotBinderTests
{
    private readonly List<GameObject> _createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject createdObject in _createdObjects)
        {
            if (createdObject != null)
                Object.DestroyImmediate(createdObject);
        }
        _createdObjects.Clear();
    }

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
        Assert.AreSame(typeof(StudyRoomChatbot), ParretPanelChatbotBinder.ResolveChatbotType("StudyRoom"));
    }

    [Test]
    public void ResolveChatbotType_FallsBackToGlobalChatbot_ForUnknownScene()
    {
        Assert.AreSame(
            typeof(GlobalChatbot),
            ParretPanelChatbotBinder.ResolveChatbotType("SomeOtherScene"));
    }

    [Test]
    public void ResolveExistingOrInstantiateSayDialog_CreatesDedicatedPrefab_WhenSceneInstanceIsMissing()
    {
        var prefabObject = new GameObject("SayDialogChatbotPrefab");
        _createdObjects.Add(prefabObject);
        var prefabSayDialog = prefabObject.AddComponent<SayDialog>();

        SayDialog resolved = ChatSayDialogResolver.ResolveExistingOrInstantiate(
            "SayDialogChatbot",
            prefabSayDialog);
        _createdObjects.Add(resolved.gameObject);

        Assert.NotNull(resolved);
        Assert.AreEqual("SayDialogChatbot", resolved.gameObject.name);
        Assert.IsFalse(resolved.gameObject.activeSelf);
        Assert.AreNotSame(prefabSayDialog, resolved);
        Assert.AreSame(resolved, SayDialog.ActiveSayDialog);
    }

    [Test]
    public void EnsurePanelSayDialogSync_AttachesSyncToPanelRoot()
    {
        var panel = new GameObject("Parret_Panel");
        _createdObjects.Add(panel);
        var sayObject = new GameObject("SayDialogChatbot");
        _createdObjects.Add(sayObject);
        var sayDialog = sayObject.AddComponent<SayDialog>();

        TutorPanelSayDialogSync sync = ParretPanelChatbotBinder.EnsurePanelSayDialogSync(panel, sayDialog);

        Assert.NotNull(sync);
        Assert.AreSame(sync, panel.GetComponent<TutorPanelSayDialogSync>());
    }

    [Test]
    public void EnsureCheshireLogButton_CreatesBookmarkButtonAboveInputRightEdge()
    {
        var panel = new GameObject("Parret_Panel", typeof(RectTransform));
        _createdObjects.Add(panel);

        DialogueLogButton button = ParretPanelChatbotBinder.EnsureCheshireLogButton(panel);

        Assert.NotNull(button);
        Assert.AreEqual("CheshireLogButton", button.gameObject.name);
        Assert.IsFalse(button.UseOverlaySortingForTests);
        Assert.IsNull(button.GetComponent<Canvas>());
        Assert.NotNull(button.GetComponent<Button>());
        Assert.NotNull(button.GetComponent<Image>());
        Assert.NotNull(button.GetComponentInChildren<TextMeshProUGUI>(true));

        var rect = button.GetComponent<RectTransform>();
        Assert.AreEqual(new Vector2(0.95f, 0f), rect.anchorMin);
        Assert.AreEqual(new Vector2(0.95f, 0f), rect.anchorMax);
        Assert.AreEqual(new Vector2(1f, 0f), rect.pivot);
        Assert.AreEqual(new Vector2(-12f, 252f), rect.anchoredPosition);
        Assert.AreEqual(new Vector2(52f, 128f), rect.sizeDelta);
    }

    [Test]
    public void StripInlineFunctionTags_RemovesToolMarkupFromDisplayedText()
    {
        string result = ChatResponseDisplayText.StripInlineFunctionTags(
            "푸드덕! <function=give_hint></function> 힌트가 필요하군?");

        Assert.AreEqual("푸드덕! 힌트가 필요하군?", result);
    }
}
