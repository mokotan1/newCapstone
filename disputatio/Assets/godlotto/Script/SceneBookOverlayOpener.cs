using System;
using System.Collections;
using System.Collections.Generic;
using Fungus;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Opens BookOverlayPanelA with collectible text resolved from the active scene name.
/// </summary>
public class SceneBookOverlayOpener : MonoBehaviour
{
    [Tooltip("비우면 현재 씬 이름으로 자동 결정합니다.")]
    [SerializeField] private string collectibleId;

    [Tooltip("비우면 씬 안의 BookOverlayPanelA를 찾고, 없으면 Resources/BookOverlayPanelA를 생성합니다.")]
    [SerializeField] private BookOverlayPagedReader overlayPanel;

    [Tooltip("클릭 시 함께 꺼둘 기존 패널입니다.")]
    [SerializeField] private GameObject[] legacyPanelsToHide;

    private Button button;
    private string openedCollectibleId;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(Open);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(Open);
    }

    public void Open()
    {
        if (!TryResolveContent(collectibleId, out var content))
            return;

        foreach (var panel in legacyPanelsToHide)
        {
            if (panel != null)
                panel.SetActive(false);
        }

        var reader = overlayPanel != null ? overlayPanel : SceneBookOverlayRuntime.EnsureOverlayPanel();
        if (reader == null)
            return;

        reader.SetContent(content.title, SceneBookOverlayRuntime.BuildDisplayBody(content));
        openedCollectibleId = content.id;
        SceneBookOverlayRuntime.BindReward(reader, openedCollectibleId);
        reader.gameObject.SetActive(true);
    }

    public static bool TryResolveContent(string explicitId, out CollectibleBookContent content)
    {
        string id = string.IsNullOrWhiteSpace(explicitId)
            ? SceneBookOverlayRuntime.ResolveCollectibleId(SceneManager.GetActiveScene().name)
            : explicitId.Trim();

        return CollectibleBookCatalog.TryGet(id, out content);
    }
}

public struct CollectibleBookContent
{
    public string id;
    public string title;
    public string body;
}

public static class CollectibleBookCatalog
{
    private const string ResourcePath = "CollectibleScripts";
    private static Dictionary<string, CollectibleBookContent> byId;

    [Serializable]
    private class Root
    {
        public Item[] items;
    }

    [Serializable]
    private class Item
    {
        public string id;
        public string type;
        public string title;
        public string body;
    }

    public static bool TryGet(string id, out CollectibleBookContent content)
    {
        EnsureLoaded();
        if (!string.IsNullOrWhiteSpace(id) && byId.TryGetValue(id.Trim(), out content))
            return true;

        content = default;
        Debug.LogWarning($"[CollectibleBookCatalog] 일기/책 데이터를 찾지 못했습니다. id={id}");
        return false;
    }

    private static void EnsureLoaded()
    {
        if (byId != null)
            return;

        byId = new Dictionary<string, CollectibleBookContent>(StringComparer.OrdinalIgnoreCase);
        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null)
        {
            Debug.LogWarning($"[CollectibleBookCatalog] Resources/{ResourcePath}.json 파일을 찾지 못했습니다.");
            return;
        }

        Root root;
        try
        {
            root = JsonUtility.FromJson<Root>(asset.text);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CollectibleBookCatalog] JSON 파싱 실패: {ex.Message}");
            return;
        }

        if (root?.items == null)
            return;

        foreach (var item in root.items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id))
                continue;

            byId[item.id] = new CollectibleBookContent
            {
                id = item.id,
                title = item.title,
                body = item.body
            };
        }
    }
}

public sealed class SceneBookOverlayRuntime : MonoBehaviour
{
    private const string OverlayResourcePath = "BookOverlayPanelA";
    private static SceneBookOverlayRuntime instance;
    private static BookOverlayPagedReader overlayPanel;

    private readonly Dictionary<string, string> collectibleIdByScene =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "MaidRoom", "ACT2_DIARY_MAID_001" },
            { "StudyRoom", "ACT2_DIARY_OWNER_001" },
            { "WifeRoom", "ACT3_DIARY_WIFE_001" },
            { "BedRoom", "ACT3_DIARY_OWNER_002" },
            { "MainBedroom", "ACT3_DIARY_OWNER_002" }
        };

    private readonly Dictionary<string, string[]> legacyPanelNamesByScene =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "MaidRoom", new[] { "Diary_Panel" } },
            { "StudyRoom", new[] { "DiaryPanel" } },
            { "BedRoom", new[] { "BookPanel" } },
            { "MainBedroom", new[] { "BookPanel" } }
        };

    private readonly Dictionary<string, string[]> buttonNamesByScene =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "WifeRoom", new[] { "UpDrawerOpen", "DownDrawerOpen" } },
            { "BedRoom", new[] { "Bookcase" } },
            { "MainBedroom", new[] { "Bookcase" } },
            { "MaidRoom", new[] { "Bed" } }
        };

    private bool monitoringLegacyPanels;
    private BookOverlayPagedReader rewardReader;
    private string rewardCollectibleId;
    private bool rewardGranted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        var go = new GameObject(nameof(SceneBookOverlayRuntime));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SceneBookOverlayRuntime>();
        SceneManager.sceneLoaded += instance.OnSceneLoaded;
        instance.BindCurrentScene();
    }

    public static string ResolveCollectibleId(string sceneName)
    {
        if (instance == null)
            Bootstrap();

        return instance != null && instance.collectibleIdByScene.TryGetValue(sceneName, out string id)
            ? id
            : string.Empty;
    }

    public static BookOverlayPagedReader EnsureOverlayPanel()
    {
        if (overlayPanel != null)
            return overlayPanel;

        overlayPanel = FindObjectOfType<BookOverlayPagedReader>(true);
        if (overlayPanel != null)
        {
            StretchOverlayRoot(overlayPanel.gameObject);
            return overlayPanel;
        }

        var prefab = Resources.Load<GameObject>(OverlayResourcePath);
        if (prefab == null)
        {
            Debug.LogWarning($"[SceneBookOverlayRuntime] Resources/{OverlayResourcePath}.prefab 파일을 찾지 못했습니다.");
            return null;
        }

        var panel = Instantiate(prefab);
        panel.name = OverlayResourcePath;
        DontDestroyOnLoad(panel);
        StretchOverlayRoot(panel);
        overlayPanel = panel.GetComponent<BookOverlayPagedReader>();
        panel.SetActive(false);
        return overlayPanel;
    }

    public static void BindReward(BookOverlayPagedReader reader, string collectibleId)
    {
        if (instance == null)
            Bootstrap();

        if (instance == null || reader == null)
            return;

        instance.BindRewardInternal(reader, collectibleId);
    }

    public static string BuildDisplayBody(CollectibleBookContent content)
    {
        if (string.Equals(SceneManager.GetActiveScene().name, "StudyRoom", StringComparison.OrdinalIgnoreCase)
            && string.Equals(content.id, "ACT2_DIARY_OWNER_001", StringComparison.OrdinalIgnoreCase))
        {
            if (IsStudyRoomRewardAlreadyClaimed())
                return BuildStudyRoomAlreadySolvedBody(content.body);

            return BuildStudyRoomDiaryBody(content.body);
        }

        return BuildSinglePageBody(content.body);
    }

    public static bool ShouldUseAlreadySolvedStudyRoomBody(
        string sceneName,
        string collectibleId,
        bool diarySolved,
        bool hasTutorKey)
    {
        return string.Equals(sceneName, "StudyRoom", StringComparison.OrdinalIgnoreCase)
            && string.Equals(collectibleId, "ACT2_DIARY_OWNER_001", StringComparison.OrdinalIgnoreCase)
            && hasTutorKey;
    }

    public static string BuildStudyRoomAlreadySolvedBody(string body)
    {
        string source = string.IsNullOrWhiteSpace(body) ? string.Empty : body.Trim();
        return source + "\f[체셔]\n나는 이미 문제를 풀었어. 더 얻을 열쇠는 없어.";
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindCurrentScene();
    }

    private void BindCurrentScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (!collectibleIdByScene.ContainsKey(sceneName))
            return;

        EnsureOverlayPanel();
        AttachButtons(sceneName);

        if (!monitoringLegacyPanels)
            StartCoroutine(MonitorLegacyPanels(sceneName));
    }

    private void BindRewardInternal(BookOverlayPagedReader reader, string collectibleId)
    {
        if (rewardReader != null)
        {
            rewardReader.LastPageShown -= OnRewardReaderLastPageShown;
            rewardReader.Closed -= OnRewardReaderClosed;
        }

        rewardReader = reader;
        rewardCollectibleId = collectibleId;
        rewardGranted = false;
        rewardReader.LastPageShown += OnRewardReaderLastPageShown;
        rewardReader.Closed += OnRewardReaderClosed;

        var presenter = rewardReader.GetComponent<BookOverlayScenePageItemPresenter>();
        if (presenter == null)
            presenter = rewardReader.gameObject.AddComponent<BookOverlayScenePageItemPresenter>();
        presenter.Bind(rewardReader, collectibleId);
    }

    private void OnRewardReaderLastPageShown(BookOverlayPagedReader reader)
    {
        TryGrantRewardForCurrentBook();
    }

    private void OnRewardReaderClosed(BookOverlayPagedReader reader)
    {
        if (reader != null && reader.HasShownLastPageSinceOpen)
            TryGrantRewardForCurrentBook();
    }

    private void TryGrantRewardForCurrentBook()
    {
        if (rewardGranted)
            return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (!string.Equals(sceneName, "StudyRoom", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(rewardCollectibleId, "ACT2_DIARY_OWNER_001", StringComparison.OrdinalIgnoreCase))
            return;

        if (IsStudyRoomRewardAlreadyClaimed())
        {
            rewardGranted = true;
            return;
        }

        rewardGranted = true;
        SetFlowchartBool("DiarySolved", true);
        ExecuteActiveSceneBlock("DiaryBackspace");
        SetSceneObjectActive("CardStack_Button", true);
    }

    private static void StretchOverlayRoot(GameObject panel)
    {
        if (panel == null)
            return;

        var rect = panel.GetComponent<RectTransform>();
        if (rect == null)
        {
            panel.transform.localScale = Vector3.one;
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.anchoredPosition = Vector2.zero;

        StretchCenterPanel(panel.transform);
        PlaceCloseButtonInsidePanel(panel.transform);
        ApplyReadableTextSizes(panel.transform);

        var reader = panel.GetComponent<BookOverlayPagedReader>();
        if (reader != null)
            reader.SetMaxCharactersPerPage(110);
    }

    private static void StretchCenterPanel(Transform overlayRoot)
    {
        Transform centerPanel = FindChildByName(overlayRoot, "CenterPanel");
        var rect = centerPanel != null ? centerPanel.GetComponent<RectTransform>() : null;
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.04f, 0.06f);
        rect.anchorMax = new Vector2(0.96f, 0.94f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static void PlaceCloseButtonInsidePanel(Transform overlayRoot)
    {
        Transform closeButton = FindChildByName(overlayRoot, "CloseButton");
        var rect = closeButton != null ? closeButton.GetComponent<RectTransform>() : null;
        if (rect == null)
            return;

        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(52f, 52f);
        rect.anchoredPosition = new Vector2(-32f, -32f);
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();
    }

    private static void ApplyReadableTextSizes(Transform overlayRoot)
    {
        SetTextSize(overlayRoot, "EyebrowText", 26, 1f);
        SetTextSize(overlayRoot, "TitleText", 54, 1f);
        SetRectHeight(overlayRoot, "TitleText", 82f);
        SetTextSize(overlayRoot, "BodyText", 40, 1.18f);
        SetTextSize(overlayRoot, "CloseText", 34, 1f);
        SetTextSize(overlayRoot, "PageIndicatorText", 28, 1f);
        SetTextSize(overlayRoot, "PreviousLabel", 28, 1f);
        SetTextSize(overlayRoot, "NextLabel", 28, 1f);
    }

    private static void SetTextSize(Transform overlayRoot, string objectName, int fontSize, float lineSpacing)
    {
        Transform textTransform = FindChildByName(overlayRoot, objectName);
        var text = textTransform != null ? textTransform.GetComponent<Text>() : null;
        if (text == null)
            return;

        text.fontSize = fontSize;
        text.resizeTextForBestFit = false;
        text.lineSpacing = lineSpacing;
        text.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private static void SetRectHeight(Transform overlayRoot, string objectName, float height)
    {
        Transform rectTransform = FindChildByName(overlayRoot, objectName);
        var rect = rectTransform != null ? rectTransform.GetComponent<RectTransform>() : null;
        if (rect == null)
            return;

        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        if (string.Equals(root.name, targetName, StringComparison.Ordinal))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static string BuildStudyRoomDiaryBody(string body)
    {
        string source = string.IsNullOrWhiteSpace(body) ? string.Empty : body.Trim();

        return source
            + "\f[새 단어 카드]\n책장 사이에서 두 장의 카드가 드러난다."
            + "\f[열쇠]\n맨 뒷장 안쪽에 작은 열쇠가 끼워져 있다.";
    }

    private static string BuildSinglePageBody(string body)
    {
        string source = string.IsNullOrWhiteSpace(body) ? string.Empty : body.Trim();
        return source + "\f";
    }

    private static bool IsStudyRoomRewardAlreadyClaimed()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return ShouldUseAlreadySolvedStudyRoomBody(
            sceneName,
            "ACT2_DIARY_OWNER_001",
            GetActiveSceneOrGlobalBool("DiarySolved"),
            GetActiveSceneOrGlobalBool("HaveTutorKey"));
    }

    private static bool GetActiveSceneOrGlobalBool(string key)
    {
        Flowchart fc = FindActiveSceneFlowchart();
        if (fc != null && fc.GetBooleanVariable(key))
            return true;

        return FlowchartLocator.GetFungusGlobalBoolean(key);
    }

    private void AttachButtons(string sceneName)
    {
        if (!buttonNamesByScene.TryGetValue(sceneName, out var buttonNames))
            return;

        var buttons = FindObjectsOfType<Button>(true);
        foreach (var button in buttons)
        {
            if (!NameMatches(button.gameObject.name, buttonNames))
                continue;

            var opener = button.GetComponent<SceneBookOverlayOpener>();
            if (opener == null)
                opener = button.gameObject.AddComponent<SceneBookOverlayOpener>();
        }
    }

    private IEnumerator MonitorLegacyPanels(string sceneName)
    {
        monitoringLegacyPanels = true;
        var wait = new WaitForSecondsRealtime(0.1f);

        while (string.Equals(SceneManager.GetActiveScene().name, sceneName, StringComparison.OrdinalIgnoreCase))
        {
            if (legacyPanelNamesByScene.TryGetValue(sceneName, out var legacyNames))
            {
                foreach (var panel in FindSceneObjectsByName(legacyNames))
                {
                    if (panel.activeInHierarchy)
                    {
                        OpenSceneContent();
                        panel.SetActive(false);
                    }
                }
            }

            yield return wait;
        }

        monitoringLegacyPanels = false;
    }

    private void OpenSceneContent()
    {
        if (!SceneBookOverlayOpener.TryResolveContent(null, out var content))
            return;

        var reader = EnsureOverlayPanel();
        if (reader == null)
            return;

        reader.SetContent(content.title, BuildDisplayBody(content));
        BindRewardInternal(reader, content.id);
        reader.gameObject.SetActive(true);
    }

    private static void SetFlowchartBool(string key, bool value)
    {
        Flowchart fc = FindActiveSceneFlowchart() ?? FlowchartLocator.Find();
        if (fc != null && !string.IsNullOrEmpty(key))
            fc.SetBooleanVariable(key, value);
    }

    private static void ExecuteActiveSceneBlock(string blockName)
    {
        Flowchart fc = FindActiveSceneFlowchart();
        if (fc == null || string.IsNullOrEmpty(blockName))
            return;

        fc.ExecuteBlock(blockName);
    }

    private static void GrantItemPickupByVariable(string fungusVariableName)
    {
        if (string.IsNullOrEmpty(fungusVariableName))
            return;

        var pickups = Resources.FindObjectsOfTypeAll<ItemPickup>();
        foreach (var pickup in pickups)
        {
            if (pickup == null || pickup.gameObject.scene != SceneManager.GetActiveScene())
                continue;
            if (!string.Equals(pickup.fungusVariableName, fungusVariableName, StringComparison.Ordinal))
                continue;

            if (!pickup.gameObject.activeSelf)
                pickup.gameObject.SetActive(true);

            pickup.PickUpDirect();
            return;
        }

        SetFlowchartBool(fungusVariableName, true);
        Debug.LogWarning($"[SceneBookOverlayRuntime] '{fungusVariableName}' ItemPickup을 찾지 못해 Fungus 변수만 설정했습니다.");
    }

    private static void SetSceneObjectActive(string objectName, bool active)
    {
        if (string.IsNullOrEmpty(objectName))
            return;

        var transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (var tr in transforms)
        {
            if (tr == null || tr.gameObject.scene != SceneManager.GetActiveScene())
                continue;
            if (!string.Equals(tr.gameObject.name, objectName, StringComparison.Ordinal))
                continue;

            tr.gameObject.SetActive(active);
            return;
        }
    }

    private static Flowchart FindActiveSceneFlowchart()
    {
        var flowcharts = Resources.FindObjectsOfTypeAll<Flowchart>();
        foreach (var flowchart in flowcharts)
        {
            if (flowchart != null && flowchart.gameObject.scene == SceneManager.GetActiveScene())
                return flowchart;
        }

        return null;
    }

    private static IEnumerable<GameObject> FindSceneObjectsByName(string[] names)
    {
        var transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (var tr in transforms)
        {
            if (tr == null || tr.gameObject.scene != SceneManager.GetActiveScene())
                continue;
            if (NameMatches(tr.gameObject.name, names))
                yield return tr.gameObject;
        }
    }

    private static bool NameMatches(string objectName, string[] names)
    {
        foreach (string name in names)
        {
            if (string.Equals(objectName, name, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}

public sealed class BookOverlayScenePageItemPresenter : MonoBehaviour
{
    private readonly Dictionary<GameObject, OriginalRectState> originalStates = new Dictionary<GameObject, OriginalRectState>();
    private readonly List<GameObject> displayedObjects = new List<GameObject>();

    private BookOverlayPagedReader reader;
    private RectTransform displayRoot;
    private Text bodyText;
    private string collectibleId;

    public void Bind(BookOverlayPagedReader targetReader, string targetCollectibleId)
    {
        if (reader != null)
        {
            reader.PageShown -= OnPageShown;
            reader.Closed -= OnReaderClosed;
        }

        RestoreDisplayedObjects();
        reader = targetReader;
        collectibleId = targetCollectibleId;

        if (reader == null)
            return;

        displayRoot = ResolveDisplayRoot(reader.transform);
        bodyText = FindChildByName(reader.transform, "BodyText")?.GetComponent<Text>();
        reader.PageShown += OnPageShown;
        reader.Closed += OnReaderClosed;
        Refresh(reader.CurrentPageIndex, reader.PageCount);
    }

    private void OnDestroy()
    {
        if (reader != null)
        {
            reader.PageShown -= OnPageShown;
            reader.Closed -= OnReaderClosed;
        }

        RestoreDisplayedObjects();
    }

    private void OnPageShown(BookOverlayPagedReader source, int pageIndex, int pageCount)
    {
        Refresh(pageIndex, pageCount);
    }

    private void OnReaderClosed(BookOverlayPagedReader source)
    {
        RestoreDisplayedObjects();
        SetBodyTextVisible(true);
    }

    private void Refresh(int pageIndex, int pageCount)
    {
        RestoreDisplayedObjects();
        bool shouldPresentStudyRewards = IsStudyRoomOwnerDiary();
        SetBodyTextVisible(!shouldPresentStudyRewards || pageIndex == 0);

        if (!shouldPresentStudyRewards || displayRoot == null)
            return;

        if (pageIndex == 1)
            ShowCardsPage();
        else if (pageIndex == 2)
            ShowKeyPage();
    }

    private bool IsStudyRoomOwnerDiary()
    {
        return string.Equals(SceneManager.GetActiveScene().name, "StudyRoom", StringComparison.OrdinalIgnoreCase)
            && string.Equals(collectibleId, "ACT2_DIARY_OWNER_001", StringComparison.OrdinalIgnoreCase);
    }

    private void ShowCardsPage()
    {
        GameObject wordCard = FindSceneObject("WordCard2");
        GameObject filterCard = FindSceneObject("FilterCard2");

        ShowObjectInOverlay(wordCard, new Vector2(0.08f, 0.16f), new Vector2(0.48f, 0.80f));
        ShowObjectInOverlay(filterCard, new Vector2(0.52f, 0.16f), new Vector2(0.92f, 0.80f));
    }

    private void ShowKeyPage()
    {
        GameObject key = FindSceneObject("TutorRoomKey");
        ShowObjectInOverlay(key, new Vector2(0.30f, 0.18f), new Vector2(0.70f, 0.78f));
    }

    private void ShowObjectInOverlay(GameObject item, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (item == null || displayRoot == null)
            return;

        CaptureOriginalState(item);
        item.transform.SetParent(displayRoot, false);
        item.SetActive(true);
        displayedObjects.Add(item);

        var rect = item.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.anchoredPosition = Vector2.zero;
            rect.SetAsLastSibling();
        }
        else
        {
            item.transform.localPosition = Vector3.zero;
            item.transform.localScale = Vector3.one;
        }

        PreserveImageAspect(item);
    }

    private void RestoreDisplayedObjects()
    {
        for (int i = displayedObjects.Count - 1; i >= 0; i--)
        {
            GameObject item = displayedObjects[i];
            if (item == null || !originalStates.TryGetValue(item, out var state))
                continue;

            item.transform.SetParent(state.parent, false);
            if (state.parent != null)
                item.transform.SetSiblingIndex(Mathf.Min(state.siblingIndex, state.parent.childCount - 1));
            item.SetActive(state.activeSelf);

            var rect = item.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = state.anchorMin;
                rect.anchorMax = state.anchorMax;
                rect.offsetMin = state.offsetMin;
                rect.offsetMax = state.offsetMax;
                rect.pivot = state.pivot;
                rect.localScale = state.localScale;
                rect.anchoredPosition = state.anchoredPosition;
            }
            else
            {
                item.transform.localPosition = state.localPosition;
                item.transform.localScale = state.localScale;
            }
        }

        displayedObjects.Clear();
    }

    private void CaptureOriginalState(GameObject item)
    {
        if (item == null || originalStates.ContainsKey(item))
            return;

        var rect = item.GetComponent<RectTransform>();
        originalStates[item] = new OriginalRectState
        {
            parent = item.transform.parent,
            siblingIndex = item.transform.GetSiblingIndex(),
            activeSelf = item.activeSelf,
            anchorMin = rect != null ? rect.anchorMin : Vector2.zero,
            anchorMax = rect != null ? rect.anchorMax : Vector2.zero,
            offsetMin = rect != null ? rect.offsetMin : Vector2.zero,
            offsetMax = rect != null ? rect.offsetMax : Vector2.zero,
            pivot = rect != null ? rect.pivot : Vector2.zero,
            anchoredPosition = rect != null ? rect.anchoredPosition : Vector2.zero,
            localPosition = item.transform.localPosition,
            localScale = item.transform.localScale
        };
    }

    private void SetBodyTextVisible(bool visible)
    {
        if (bodyText != null)
            bodyText.enabled = visible;
    }

    private static RectTransform ResolveDisplayRoot(Transform root)
    {
        Transform centerPanel = FindChildByName(root, "CenterPanel");
        if (centerPanel != null)
            return centerPanel.GetComponent<RectTransform>();

        return root.GetComponent<RectTransform>();
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        if (string.Equals(root.name, targetName, StringComparison.Ordinal))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        var transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (var tr in transforms)
        {
            if (tr == null || tr.gameObject.scene != SceneManager.GetActiveScene())
                continue;
            if (string.Equals(tr.gameObject.name, objectName, StringComparison.Ordinal))
                return tr.gameObject;
        }

        Debug.LogWarning($"[BookOverlayScenePageItemPresenter] 씬에서 '{objectName}' 오브젝트를 찾지 못했습니다.");
        return null;
    }

    private static void PreserveImageAspect(GameObject item)
    {
        var images = item.GetComponentsInChildren<Image>(true);
        foreach (var image in images)
            image.preserveAspect = true;
    }

    private struct OriginalRectState
    {
        public Transform parent;
        public int siblingIndex;
        public bool activeSelf;
        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 offsetMin;
        public Vector2 offsetMax;
        public Vector2 pivot;
        public Vector2 anchoredPosition;
        public Vector3 localPosition;
        public Vector3 localScale;
    }
}
