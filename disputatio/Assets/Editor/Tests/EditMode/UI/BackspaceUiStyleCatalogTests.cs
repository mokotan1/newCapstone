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
        Assert.IsTrue(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("StudyRoomCutScene"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("POAnimation"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("GoPrisonAnimation"));
        Assert.IsTrue(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("BetaEnd"));

        Assert.IsFalse(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("Hall_playerble"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("MaidRoom"));
        Assert.IsFalse(BackspaceUiSceneApplier.IsSceneBackspaceExcludedSceneName("BasementHallway"));
    }
}
