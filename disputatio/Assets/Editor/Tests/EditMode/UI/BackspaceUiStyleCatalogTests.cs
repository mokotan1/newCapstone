using NUnit.Framework;

public class BackspaceUiStyleCatalogTests
{
    [Test]
    public void SelectedStyles_MatchApprovedChoices()
    {
        Assert.AreEqual(BackspacePanelCloseStyle.CornerFold, BackspaceUiStyleCatalog.SelectedPanelCloseStyle);
        Assert.AreEqual(BackspaceChatCloseStyle.Nameplate, BackspaceUiStyleCatalog.SelectedChatCloseStyle);
        Assert.AreEqual(BackspaceSceneNavigationStyle.TopLeftRibbon, BackspaceUiStyleCatalog.SelectedSceneNavigationStyle);
    }

    [Test]
    public void InteractionPanelVariants_ExposeAllThreeRequestedPrefabs()
    {
        var variants = BackspaceUiStyleCatalog.InteractionPanelVariants;

        Assert.AreEqual(3, variants.Length);
        Assert.AreEqual(BackspaceInteractionPanelStyle.CornerFold, variants[0].style);
        Assert.AreEqual(BackspaceInteractionPanelStyle.SideTab, variants[1].style);
        Assert.AreEqual(BackspaceInteractionPanelStyle.BottomKey, variants[2].style);
        Assert.IsTrue(variants[0].prefabPath.EndsWith("InteractionPanel_CornerFold.prefab"));
        Assert.IsTrue(variants[1].prefabPath.EndsWith("InteractionPanel_SideTab.prefab"));
        Assert.IsTrue(variants[2].prefabPath.EndsWith("InteractionPanel_BottomKey.prefab"));
    }

    [Test]
    public void SceneApplier_RecognizesOnlySceneNavigationBackspaceNames()
    {
        Assert.IsTrue(BackspaceUiSceneApplier.IsSceneBackspaceName("SceneBackNavigator_Ribbon"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsSceneBackspaceName("SceneBackRibbon"));

        Assert.IsFalse(BackspaceUiSceneApplier.IsSceneBackspaceName("Backspace"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsSceneBackspaceName("BackspaceCornerFold"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsSceneBackspaceName("PanelBackspace"));
    }

    [Test]
    public void SceneApplier_RecognizesLegacySceneBackspaceNameForCleanup()
    {
        Assert.IsTrue(BackspaceUiSceneApplier.IsLegacySceneBackspaceName("Backspace"));

        Assert.IsFalse(BackspaceUiSceneApplier.IsLegacySceneBackspaceName("SceneBackNavigator_Ribbon"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsLegacySceneBackspaceName("SceneBackRibbon"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsLegacySceneBackspaceName("BackspaceCornerFold"));
    }

    [Test]
    public void SceneApplier_ExcludesFlowLockedScenesFromSceneBackspace()
    {
        Assert.IsTrue(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("MainMenuScene"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("Opening_Office"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("Hall_animate"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("Hall_playerble"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("StudyRoomCutScene"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("POAnimation"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("GoPrisonAnimation"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("BetaEnd"));

        Assert.IsFalse(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("MaidRoom"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("BasementHallway"));
    }

    [Test]
    public void SceneBackCanvas_SortsBehindPanels()
    {
        Assert.AreEqual("Ui", BackspaceUiPrefabBuilder.SceneBackCanvasSortingLayerName);
        Assert.Less(BackspaceUiPrefabBuilder.SceneBackCanvasSortingOrder, 0);
        Assert.Greater(BackspaceUiPrefabBuilder.PanelBackCanvasSortingOrder, BackspaceUiPrefabBuilder.SceneBackCanvasSortingOrder);
    }

    [Test]
    public void SceneApplier_RecognizesCurrentBackspaceObjectNames()
    {
        Assert.IsTrue(BackspaceUiSceneApplier.IsCurrentBackspaceObjectName("SceneBackNavigator_Ribbon"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsCurrentBackspaceObjectName("SceneBackRibbon"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsCurrentBackspaceObjectName("BackspaceCornerFold"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsCurrentBackspaceObjectName("BackspaceNameplate"));

        Assert.IsFalse(BackspaceUiSceneApplier.IsCurrentBackspaceObjectName("backspace"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsCurrentBackspaceObjectName("LockBackspace"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsCurrentBackspaceObjectName("PanelBackspace"));
    }

    [Test]
    public void SceneApplier_RecognizesPanelBackspaceCandidateNames()
    {
        Assert.IsTrue(BackspaceUiSceneApplier.IsPanelBackspaceCandidateName("backspace"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsPanelBackspaceCandidateName("LockBackspace"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsPanelBackspaceCandidateName("PuzzleBookBackspace"));

        Assert.IsFalse(BackspaceUiSceneApplier.IsPanelBackspaceCandidateName("SceneBackNavigator_Ribbon"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsPanelBackspaceCandidateName("SceneBackRibbon"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsPanelBackspaceCandidateName("BackspaceNameplate"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsPanelBackspaceCandidateName("CloseButton"));
    }

    [Test]
    public void SceneApplier_RecognizesKnownInteractionPanels()
    {
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownInteractionPanelName("DiaryPanel"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownInteractionPanelName("TrashBox_pannel"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownInteractionPanelName("WhiteBoardPanel"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownInteractionPanelName("WallclockPanel"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownInteractionPanelName("ChestPanel"));

        Assert.IsFalse(BackspaceUiSceneApplier.IsKnownInteractionPanelName("SceneBackNavigator_Ribbon"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsKnownInteractionPanelName("Main Camera"));
    }

    [Test]
    public void SceneApplier_RecognizesSceneSpecificInteractionPanelTargets()
    {
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownInteractionPanelTarget("BasementResearchRoom", "Panel"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownInteractionPanelTarget("MaidRoom", "PuzzlePanel"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownInteractionPanelTarget("Kitchen", "Sink_Pannel"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownInteractionPanelTarget("WifeRoom", "WallclockPanel"));

        Assert.IsFalse(BackspaceUiSceneApplier.IsKnownInteractionPanelTarget("Opening_Office", "Panel"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsKnownInteractionPanelTarget("Hall_playerble", "Panel"));
    }

    [Test]
    public void SceneApplier_RecognizesScenesThatContainKnownInteractionPanelTargets()
    {
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownInteractionPanelTargetScene("MaidRoom"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownInteractionPanelTargetScene("Kitchen"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownInteractionPanelTargetScene("BasementResearchRoom"));

        Assert.IsFalse(BackspaceUiSceneApplier.IsKnownInteractionPanelTargetScene("Opening_Office"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsKnownInteractionPanelTargetScene("Hall_playerble"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsKnownInteractionPanelTargetScene("CreateEffect"));
    }

    [Test]
    public void SceneApplier_RecognizesChatbotPanelTargets()
    {
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownChatbotPanelTarget("TutorRoom", "Parret_Panel"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownChatbotPanelTarget("WifeRoom", "Parret_Panel"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownChatbotPanelTarget("Hall_playerble", "Parret_Panel"));

        Assert.IsFalse(BackspaceUiSceneApplier.IsKnownChatbotPanelTarget("Opening_Office", "Parret_Panel"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsKnownChatbotPanelTarget("TutorRoom", "WhiteBoardPanel"));
    }

    [Test]
    public void SceneApplier_RecognizesScenesThatContainKnownChatbotPanelTargets()
    {
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownChatbotPanelTargetScene("TutorRoom"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownChatbotPanelTargetScene("ChildRoom"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsKnownChatbotPanelTargetScene("Hall_playerble"));

        Assert.IsFalse(BackspaceUiSceneApplier.IsKnownChatbotPanelTargetScene("Opening_Office"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsKnownChatbotPanelTargetScene("CreateEffect"));
    }

    [Test]
    public void SceneApplier_MapsPanelsWithLegacyCloseBlocks()
    {
        Assert.AreEqual("DiaryBackspace", BackspaceUiSceneApplier.ResolveLegacyCloseBlockName("StudyRoom", "DiaryPanel"));
        Assert.AreEqual("CardStackBackspace", BackspaceUiSceneApplier.ResolveLegacyCloseBlockName("StudyRoom", "CardStackPanel"));
        Assert.AreEqual("PanelBackspace", BackspaceUiSceneApplier.ResolveLegacyCloseBlockName("Prison", "NotePanel"));
        Assert.AreEqual("LockBackspace", BackspaceUiSceneApplier.ResolveLegacyCloseBlockName("PrisonEntrance", "LockPanel"));
        Assert.AreEqual("PanelBackspace", BackspaceUiSceneApplier.ResolveLegacyCloseBlockName("MaidRoom", "Diary_Panel"));

        Assert.AreEqual(string.Empty, BackspaceUiSceneApplier.ResolveLegacyCloseBlockName("Kitchen", "Sink_Pannel"));
        Assert.AreEqual(string.Empty, BackspaceUiSceneApplier.ResolveLegacyCloseBlockName("TutorRoom", "WhiteBoardPanel"));
    }
}
