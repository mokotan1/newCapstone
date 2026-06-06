using System;
using System.Collections;
using System.Collections.Generic;
using Fungus;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 다크 글래스 선택지 메뉴 프리젠터. 옵션 수만큼 버튼을 동적 생성하며,
/// Fungus 원본 <see cref="Fungus.MenuDialog"/>와 달리 고정 버튼 풀을 쓰지 않습니다.
/// 타이머/슬라이더는 없습니다.
/// </summary>
public class GlassMenuDialog : MonoBehaviour
{
    [Tooltip("위치를 잡는 글래스 패널 루트.")]
    [SerializeField] private RectTransform panelRoot;

    [Tooltip("선택지 버튼이 쌓이는 컨테이너(VerticalLayoutGroup + ContentSizeFitter 권장).")]
    [SerializeField] private RectTransform optionContainer;

    [Tooltip("동적 생성할 다크 글래스 버튼 프리팹(Button + 자식 TextMeshProUGUI).")]
    [SerializeField] private Button optionButtonPrefab;

    private readonly List<Button> spawnedButtons = new List<Button>();

    /// <summary>현재 활성 다이얼로그(Fungus MenuDialog의 ActiveMenuDialog와 동일 개념).</summary>
    public static GlassMenuDialog ActiveGlassMenuDialog { get; set; }

    /// <summary>
    /// 테스트 seam. 설정되면 옵션 클릭 시 블록을 직접 실행하는 대신 이 핸들러를 호출합니다.
    /// 프로덕션에서는 null이며 실제 블록 실행 경로를 사용합니다.
    /// </summary>
    public static Action<Block> BlockExecutorForTests;

    /// <summary>씬에서 찾고, 없으면 Resources에서 자동 스폰합니다(Fungus 패턴).</summary>
    public static GlassMenuDialog GetMenuDialog()
    {
        if (ActiveGlassMenuDialog == null)
        {
            var found = FindFirstObjectByType<GlassMenuDialog>(FindObjectsInactive.Include);
            if (found != null)
            {
                ActiveGlassMenuDialog = found;
            }
            else
            {
                var prefab = Resources.Load<GameObject>("Prefabs/GlassMenuDialog");
                if (prefab != null)
                {
                    var go = Instantiate(prefab);
                    go.name = "GlassMenuDialog";
                    go.SetActive(false);
                    ActiveGlassMenuDialog = go.GetComponent<GlassMenuDialog>();
                }
            }
        }

        if (ActiveGlassMenuDialog != null)
            ActiveGlassMenuDialog.CheckEventSystem();

        return ActiveGlassMenuDialog;
    }

    private void CheckEventSystem()
    {
        if (EventSystem.current == null && FindFirstObjectByType<EventSystem>(FindObjectsInactive.Exclude) == null)
        {
            var prefab = Resources.Load<GameObject>("Prefabs/EventSystem");
            if (prefab != null)
            {
                var go = Instantiate(prefab);
                go.name = "EventSystem";
            }
        }
    }

    private void OnEnable()
    {
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>패널을 앵커 프리셋 + 오프셋 위치로 배치합니다.</summary>
    public void ApplyPlacement(MenuAnchor anchor, Vector2 offset)
    {
        if (panelRoot != null)
            MenuAnchorLayout.Apply(panelRoot, anchor, offset);
    }

    /// <summary>선택지 버튼 하나를 동적 생성해 컨테이너에 추가합니다.</summary>
    public bool AddOption(string text, bool interactable, Block targetBlock)
    {
        if (optionButtonPrefab == null || optionContainer == null)
        {
            Debug.LogWarning("[GlassMenuDialog] 프리팹/컨테이너 참조가 없습니다.");
            return false;
        }

        var button = Instantiate(optionButtonPrefab, optionContainer);
        button.gameObject.SetActive(true);
        button.interactable = interactable;

        var label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = text;

        var captured = targetBlock;
        button.onClick.AddListener(() => OnOptionSelected(captured));

        spawnedButtons.Add(button);

        if (panelRoot != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);

        return true;
    }

    private void OnOptionSelected(Block targetBlock)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        Clear();
        gameObject.SetActive(false);

        if (BlockExecutorForTests != null)
        {
            BlockExecutorForTests(targetBlock);
            return;
        }

        if (targetBlock != null)
        {
            var flowchart = targetBlock.GetFlowchart();
            flowchart.StartCoroutine(CallBlock(targetBlock));
        }
    }

    private IEnumerator CallBlock(Block block)
    {
        yield return new WaitForEndOfFrame();
        block.StartExecution();
    }

    /// <summary>생성된 모든 선택지 버튼을 제거합니다.</summary>
    public void Clear()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            if (spawnedButtons[i] != null)
            {
                spawnedButtons[i].onClick.RemoveAllListeners();
                Destroy(spawnedButtons[i].gameObject);
            }
        }
        spawnedButtons.Clear();
    }

    /// <summary>다이얼로그 GameObject 활성 상태를 설정합니다.</summary>
    public void SetActive(bool state)
    {
        gameObject.SetActive(state);
    }

    /// <summary>현재 표시 중인지 여부.</summary>
    public bool IsActive()
    {
        return gameObject.activeInHierarchy;
    }
}
