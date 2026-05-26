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
