using System;
using Fungus;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ParretPanelChatbotBinder : MonoBehaviour
{
    [Header("Shared Chat UI")]
    [SerializeField] private SayDialog chatSayDialog;
    [SerializeField] private SayDialog chatSayDialogPrefab;
    [SerializeField] private string chatSayDialogObjectName = "SayDialogChatbot";
    [SerializeField] private TMP_InputField userInputField;
    [SerializeField] private Button sendButton;
    [SerializeField] private string localServerUrlOverride = "http://15.134.24.132:8000/chat";

    [Header("Flowchart")]
    [SerializeField] private Flowchart sceneFlowchart;
    [SerializeField] private string fallbackFlowchartObjectName = "Flowchart";

    private BaseChatbot boundChatbot;

    public BaseChatbot BoundChatbot => boundChatbot;

    private void Awake()
    {
        BindForScene(SceneManager.GetActiveScene().name);
    }

    public void BindForScene(string sceneName)
    {
        Type chatbotType = ResolveChatbotType(sceneName);
        BaseChatbot chatbot = GetOrAddChatbot(chatbotType);

        ResolveSharedReferences();
        EnsureCheshireLogButton(gameObject);
        ConfigureChatbot(chatbot);
        WireSendButton(chatbot);

        boundChatbot = chatbot;
    }

    public static Type ResolveChatbotType(string sceneName)
    {
        switch (sceneName)
        {
            case "WifeRoom":
                return typeof(WifeRoomChatbot);
            case "ChildRoom":
                return typeof(SonRoomChatbot);
            case "BedRoom":
                return typeof(MainBedroomChatbot);
            case "Kitchen":
                return typeof(KitchenChatbot);
            case "TutorRoom":
                return typeof(TutorChatbot);
            case "StudyRoom":
                return typeof(StudyRoomChatbot);
            case "Hall_playerble":
                return typeof(GlobalChatbot);
            default:
                return typeof(GlobalChatbot);
        }
    }

    public static TutorPanelSayDialogSync EnsurePanelSayDialogSync(GameObject panelRoot, SayDialog sayDialog)
    {
        if (panelRoot == null || sayDialog == null)
            return null;

        var sync = panelRoot.GetComponent<TutorPanelSayDialogSync>();
        if (sync == null)
            sync = panelRoot.AddComponent<TutorPanelSayDialogSync>();
        sync.Initialize(sayDialog);
        return sync;
    }

    public static DialogueLogButton EnsureCheshireLogButton(GameObject panelRoot)
    {
        if (panelRoot == null)
            return null;

        DialogueLogButton existing = panelRoot.GetComponentInChildren<DialogueLogButton>(true);
        if (existing != null && existing.gameObject.name == "CheshireLogButton")
            return existing;

        DialogueLogButtonSpec.BookmarkButtonSpec spec = DialogueLogButtonSpec.CreateChatbotBookmarkDefaults();
        var buttonGo = new GameObject(
            spec.buttonName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonGo.transform.SetParent(panelRoot.transform, false);
        buttonGo.transform.SetAsLastSibling();

        var rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = spec.anchor;
        rect.anchorMax = spec.anchor;
        rect.pivot = spec.pivot;
        rect.anchoredPosition = spec.anchoredPosition;
        rect.sizeDelta = spec.size;

        var background = buttonGo.GetComponent<Image>();
        background.color = spec.background;
        background.raycastTarget = true;
        background.raycastPadding = new Vector4(12f, 12f, 12f, 12f);

        var button = buttonGo.GetComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = spec.colorBlock.ToUnityColorBlock();

        CreateRule(buttonGo.transform, "TopRule", spec.border, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 3f));
        CreateRule(buttonGo.transform, "SideRule", spec.border, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(3f, 0f));

        var captionGo = new GameObject("Caption", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        captionGo.transform.SetParent(buttonGo.transform, false);
        var captionRect = captionGo.GetComponent<RectTransform>();
        captionRect.anchorMin = Vector2.zero;
        captionRect.anchorMax = Vector2.one;
        captionRect.pivot = new Vector2(0.5f, 0.5f);
        captionRect.anchoredPosition = Vector2.zero;
        captionRect.sizeDelta = Vector2.zero;

        var caption = captionGo.GetComponent<TextMeshProUGUI>();
        caption.text = "로\n그";
        caption.fontSize = spec.captionFontSize;
        caption.color = spec.foreground;
        caption.alignment = TextAlignmentOptions.Center;
        caption.enableAutoSizing = false;
        caption.lineSpacing = 16f;
        caption.raycastTarget = false;
        caption.margin = new Vector4(0f, 12f, 0f, 10f);

        var logButton = buttonGo.AddComponent<DialogueLogButton>();
        logButton.SetUseOverlaySorting(false);
        return logButton;
    }

    private static void CreateRule(
        Transform parent,
        string name,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 sizeDelta)
    {
        var ruleGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        ruleGo.transform.SetParent(parent, false);

        var rect = ruleGo.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = sizeDelta;

        var image = ruleGo.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private BaseChatbot GetOrAddChatbot(Type chatbotType)
    {
        BaseChatbot selected = null;
        BaseChatbot[] chatbots = GetComponentsInChildren<BaseChatbot>(true);

        foreach (BaseChatbot chatbot in chatbots)
        {
            if (chatbot.GetType() == chatbotType && selected == null)
            {
                selected = chatbot;
                continue;
            }

            Destroy(chatbot);
        }

        if (selected != null)
            return selected;

        return (BaseChatbot)gameObject.AddComponent(chatbotType);
    }

    private void ResolveSharedReferences()
    {
        if (userInputField == null)
            userInputField = GetComponentInChildren<TMP_InputField>(true);

        if (sendButton == null)
            sendButton = FindSendButton();

        if (ShouldUseDedicatedChatSayDialog())
            chatSayDialog = FindChatSayDialog();
        EnsurePanelSayDialogSync(gameObject, chatSayDialog);

        if (sceneFlowchart == null)
            sceneFlowchart = ResolveSceneFlowchart();
    }

    private Button FindSendButton()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button.gameObject.name == "SendButton")
                return button;
        }

        return null;
    }

    private bool ShouldUseDedicatedChatSayDialog()
    {
        if (chatSayDialog == null)
            return true;

        if (string.IsNullOrWhiteSpace(chatSayDialogObjectName))
            return false;

        return chatSayDialog.gameObject.name != chatSayDialogObjectName;
    }

    private SayDialog FindChatSayDialog()
    {
        SayDialog dedicatedSayDialog = ChatSayDialogResolver.ResolveExistingOrInstantiate(
            chatSayDialogObjectName,
            chatSayDialogPrefab);
        if (dedicatedSayDialog != null)
            return dedicatedSayDialog;

        return FindFirstObjectByType<SayDialog>(FindObjectsInactive.Include);
    }

    private Flowchart ResolveSceneFlowchart()
    {
        Flowchart flowchart = FlowchartLocator.FindByGameObjectName(fallbackFlowchartObjectName);
        if (flowchart != null)
            return flowchart;

        Flowchart[] flowcharts = FindObjectsByType<Flowchart>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        return flowcharts.Length > 0 ? flowcharts[0] : null;
    }

    private void ConfigureChatbot(BaseChatbot chatbot)
    {
        chatbot.ConfigureSharedBindings(chatSayDialog, userInputField, localServerUrlOverride);

        switch (chatbot)
        {
            case WifeRoomChatbot wifeRoomChatbot:
                wifeRoomChatbot.wifeFlowchart = sceneFlowchart;
                break;
            case SonRoomChatbot sonRoomChatbot:
                sonRoomChatbot.sonFlowchart = sceneFlowchart;
                break;
            case MainBedroomChatbot mainBedroomChatbot:
                mainBedroomChatbot.mainFlowchart = sceneFlowchart;
                break;
            case KitchenChatbot kitchenChatbot:
                kitchenChatbot.kitchenFlowchart = sceneFlowchart;
                break;
            case TutorChatbot tutorChatbot:
                tutorChatbot.flowchart = sceneFlowchart;
                break;
            case StudyRoomChatbot studyRoomChatbot:
                studyRoomChatbot.studyFlowchart = sceneFlowchart;
                break;
            case GlobalChatbot globalChatbot:
                globalChatbot.globalFlowchart = FlowchartLocator.Find() ?? sceneFlowchart;
                break;
        }
    }

    private void WireSendButton(BaseChatbot chatbot)
    {
        if (sendButton == null)
            return;

        sendButton.onClick.RemoveAllListeners();
        sendButton.onClick.AddListener(chatbot.OnSendButtonClick);
    }
}
