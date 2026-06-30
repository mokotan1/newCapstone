using UnityEngine;
using Fungus;
using UnityEngine.UI;

/// <summary>
/// Hall 등에서 Parret 패널이 켜진 뒤에도 Fungus SayDialog(Overlay)의 CanvasGroup이
/// blocksRaycasts=true로 남아 있으면 그 아래 Screen Space Camera UI(입력 필드)가 클릭을 받지 못합니다.
/// 패널이 열릴 때 한 번 레이캐스트 차단을 끄고, Parret 오브젝트를 임시로 숨깁니다.
/// </summary>
[DisallowMultipleComponent]
public class ParrotPanelUiFix : MonoBehaviour
{
    [SerializeField] private SayDialog targetSayDialog;
    [SerializeField] private CanvasGroup sayDialogCanvasGroup;
    [SerializeField] private Image panelBackgroundImage;
    [SerializeField] private Sprite electricOnBackgroundSprite;
    [SerializeField] private string electricOnVariableName = FungusVariableKeys.ElectricOn;

    [Header("Parret Visibility Settings")]
    [Tooltip("자동으로 찾을 Parret 오브젝트의 이름입니다.")]
    [SerializeField] private string parretObjectName = "Parret";

    [Tooltip("자동으로 찾은 Parret 오브젝트가 여기에 캐싱됩니다. 미리 인스펙터에서 할당해둘 수도 있습니다.")]
    [SerializeField] private GameObject targetParret;

    [Header("Input Panel")]
    [Tooltip("패널 활성화 시 함께 켜질 InputPanelNotebook. 비워두면 자식에서 이름으로 찾습니다.")]
    [SerializeField] private GameObject inputPanelNotebook;

    private Sprite defaultBackgroundSprite;

    private void Awake()
    {
        if (sayDialogCanvasGroup == null && targetSayDialog != null)
            sayDialogCanvasGroup = targetSayDialog.GetComponent<CanvasGroup>();
        
        if (sayDialogCanvasGroup == null)
        {
            var sd = FindFirstObjectByType<SayDialog>();
            if (sd != null)
                sayDialogCanvasGroup = sd.GetComponent<CanvasGroup>();
        }

        if (panelBackgroundImage == null)
            panelBackgroundImage = GetComponent<Image>();

        if (panelBackgroundImage != null)
            defaultBackgroundSprite = panelBackgroundImage.sprite;
    }

    private void OnEnable()
    {
        ModalInputGate.Begin(this, gameObject, blocksHud: true, blocksWorld: true);
        HideDuplicateBackspaceNameplates();

        // 1. 기존 레이캐스트 차단 해제 로직
        if (sayDialogCanvasGroup != null)
        {
            sayDialogCanvasGroup.blocksRaycasts = false;
            sayDialogCanvasGroup.interactable = false;
        }

        // 2. Parret 오브젝트 자동 탐색
        if (targetParret == null)
        {
            targetParret = GameObject.Find(parretObjectName);
        }

        // 3. Parret 임시 비활성화
        if (targetParret != null)
            targetParret.SetActive(false);

        // 4. InputPanelNotebook 자동 탐색 후 활성화
        if (inputPanelNotebook == null)
            inputPanelNotebook = transform.Find("InputPanelNotebook")?.gameObject;
        if (inputPanelNotebook != null)
            inputPanelNotebook.SetActive(true);

        ApplyBackgroundSprite();
    }

    private void OnDisable()
    {
        ModalInputGate.End(this);

        // 패널이 닫힐 때 Parret 다시 활성화
        if (targetParret != null)
            targetParret.SetActive(true);

        // InputPanelNotebook도 함께 비활성화
        if (inputPanelNotebook != null)
            inputPanelNotebook.SetActive(false);
    }

    private void OnDestroy()
    {
        ModalInputGate.End(this);
    }

    private void ApplyBackgroundSprite()
    {
        if (panelBackgroundImage == null)
            return;

        bool isElectricOn = IsElectricOn();
        panelBackgroundImage.sprite = ChoosePanelBackground(
            defaultBackgroundSprite,
            electricOnBackgroundSprite,
            isElectricOn);
    }

    private bool IsElectricOn()
    {
        Flowchart flowchart = FlowchartLocator.Find();
        if (flowchart == null)
            return false;

        string key = string.IsNullOrWhiteSpace(electricOnVariableName)
            ? FungusVariableKeys.ElectricOn
            : electricOnVariableName;
        return flowchart.GetBooleanVariable(key);
    }

    public static Sprite ChoosePanelBackground(Sprite defaultSprite, Sprite electricOnSprite, bool isElectricOn)
    {
        if (!isElectricOn || electricOnSprite == null)
            return defaultSprite;

        return electricOnSprite;
    }

    private void HideDuplicateBackspaceNameplates()
    {
        var closers = FindObjectsByType<PanelBackspaceCloser>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < closers.Length; i++)
        {
            PanelBackspaceCloser closer = closers[i];
            if (closer == null || closer.gameObject == null)
                continue;
            if (closer.transform == transform || closer.transform.IsChildOf(transform))
                continue;
            if (closer.gameObject.name != "BackspaceNameplate")
                continue;
            if (!closer.TargetsPanel(gameObject))
                continue;

            closer.gameObject.SetActive(false);
        }
    }

}
