public enum BackspacePanelCloseStyle
{
    PaperTab,
    Seal,
    CornerFold
}

public enum BackspaceChatCloseStyle
{
    MiniCorner,
    Nameplate,
    SoftArrow
}

public enum BackspaceSceneNavigationStyle
{
    TopLeftRibbon,
    BottomPlate,
    LeftHandle
}

public enum BackspaceInteractionPanelStyle
{
    CornerFold,
    SideTab,
    BottomKey
}

public readonly struct BackspaceInteractionPanelVariant
{
    public BackspaceInteractionPanelVariant(BackspaceInteractionPanelStyle style, string prefabPath)
    {
        this.style = style;
        this.prefabPath = prefabPath;
    }

    public readonly BackspaceInteractionPanelStyle style;
    public readonly string prefabPath;
}

public static class BackspaceUiStyleCatalog
{
    public const BackspacePanelCloseStyle SelectedPanelCloseStyle = BackspacePanelCloseStyle.CornerFold;
    public const BackspaceChatCloseStyle SelectedChatCloseStyle = BackspaceChatCloseStyle.Nameplate;
    public const BackspaceSceneNavigationStyle SelectedSceneNavigationStyle = BackspaceSceneNavigationStyle.TopLeftRibbon;

    public const string PrefabRoot = "Assets/godlotto/Prefab/Backspace";
    public const string PanelClosePrefabPath = PrefabRoot + "/PanelBackspace_CornerFold.prefab";
    public const string ChatClosePrefabPath = PrefabRoot + "/ChatbotBackspace_Nameplate.prefab";
    public const string SceneBackPrefabPath = PrefabRoot + "/SceneBackNavigator_Ribbon.prefab";

    public static readonly BackspaceInteractionPanelVariant[] InteractionPanelVariants =
    {
        new BackspaceInteractionPanelVariant(
            BackspaceInteractionPanelStyle.CornerFold,
            PrefabRoot + "/InteractionPanel_CornerFold.prefab"),
        new BackspaceInteractionPanelVariant(
            BackspaceInteractionPanelStyle.SideTab,
            PrefabRoot + "/InteractionPanel_SideTab.prefab"),
        new BackspaceInteractionPanelVariant(
            BackspaceInteractionPanelStyle.BottomKey,
            PrefabRoot + "/InteractionPanel_BottomKey.prefab")
    };
}
