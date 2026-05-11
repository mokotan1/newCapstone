using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Backspace 버튼 클릭 시 지정한 패널을 비활성화합니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class PanelBackspaceCloser : MonoBehaviour
{
    [Tooltip("닫을 패널입니다. 비워 두면 버튼의 부모 오브젝트를 닫습니다.")]
    [SerializeField] private GameObject targetPanel;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ClosePanel);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(ClosePanel);
    }

    public void ClosePanel()
    {
        GameObject panel = targetPanel != null
            ? targetPanel
            : transform.parent != null
                ? transform.parent.gameObject
                : null;

        if (panel != null)
            panel.SetActive(false);
    }
}
