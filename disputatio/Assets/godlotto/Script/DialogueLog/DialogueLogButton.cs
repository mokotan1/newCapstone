using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대사 로그를 여는 버튼용 프록시. <see cref="Button"/>이 있는 오브젝트에 이 컴포넌트를
/// 붙이기만 하면 자동으로 클릭 → <see cref="DialogueLogPanel.Toggle"/>이 연결된다.
///
/// <para><see cref="DialogueLogPanel"/>은 씬을 넘나드는 싱글톤이라 다른 씬/프리팹의
/// Button.OnClick에 인스pector로 드래그 연결할 수 없다. 그래서 이 컴포넌트가 런타임에
/// 정적 <c>Instance</c>를 통해 호출을 위임한다.</para>
/// </summary>
[RequireComponent(typeof(Button))]
public class DialogueLogButton : MonoBehaviour
{
    const string SettingSortingLayer = "Setting";
    const int LogButtonSortingOrder = 65;

    Canvas overlayCanvas;
    GraphicRaycaster overlayRaycaster;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Toggle);
        EnsureTopRaycastOrder();
    }

    void OnEnable()
    {
        EnsureTopRaycastOrder();
    }

    /// <summary>인스펙터에서 직접 OnClick에 연결하고 싶을 때도 쓸 수 있는 public 진입점.</summary>
    public void Toggle()
    {
        if (DialogueLogPanel.Instance != null)
            DialogueLogPanel.Instance.Toggle();
    }

    void EnsureTopRaycastOrder()
    {
        transform.SetAsLastSibling();

        if (overlayCanvas == null)
            overlayCanvas = GetComponent<Canvas>();
        if (overlayCanvas == null)
            overlayCanvas = gameObject.AddComponent<Canvas>();

        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingLayerName = SettingSortingLayer;
        overlayCanvas.sortingOrder = LogButtonSortingOrder;

        if (overlayRaycaster == null)
            overlayRaycaster = GetComponent<GraphicRaycaster>();
        if (overlayRaycaster == null)
            overlayRaycaster = gameObject.AddComponent<GraphicRaycaster>();

        var background = GetComponent<Image>();
        if (background != null)
            background.raycastPadding = new Vector4(12f, 12f, 12f, 12f);
    }
}
