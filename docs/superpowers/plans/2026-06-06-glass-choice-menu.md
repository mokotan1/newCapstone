# Glass Choice Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fungus의 기본 선택지 UI를 대체하는 다크 글래스 톤의 선택지 메뉴를, 개수·문구·분기·위치(앵커+오프셋)를 단일 Fungus 커스텀 커맨드에서 지정할 수 있도록 구현한다.

**Architecture:** Fungus 원본 `Menu`/`MenuDialog`는 건드리지 않는다. 신규 `GlassMenu : Command`가 옵션 리스트(문구+타깃 Block)와 앵커·오프셋을 들고, 런타임에 신규 `GlassMenuDialog : MonoBehaviour` 프리젠터를 찾아(없으면 Resources에서 자동 스폰) 옵션 수만큼 다크 글래스 버튼을 **동적 인스턴스화**한다. 순수 로직(앵커→RectTransform 매핑, 연결 블록 수집)은 분리해 EditMode에서 테스트한다.

**Tech Stack:** Unity (uGUI + TextMeshPro), Fungus, C#, Unity Test Framework (NUnit, EditMode). 모든 런타임 코드는 `Assembly-CSharp`, 에디터 코드는 `Assembly-CSharp-Editor`(프로젝트에 asmdef 없음).

**설계 문서:** `docs/superpowers/specs/2026-06-06-glass-choice-menu-design.md`

**테스트 실행 방법:** Unity 에디터에서 `Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All` (또는 해당 픽스처만 Run). 각 Task의 "Expected"는 Test Runner 결과 기준이다. (선택) CLI: `& "<UnityEditorPath>\Unity.exe" -batchmode -projectPath "C:\Users\user\Documents\GitHub\newCapstone\disputatio" -runTests -testPlatform EditMode -quit`

**커밋 주의:** 현재 `main` 브랜치다. 구현 시작 전 feature 브랜치(예: `feature/glass-choice-menu`)를 생성하고 거기서 커밋한다. `.meta` 파일은 항상 `.cs`/프리팹과 함께 add/commit 한다.

---

## File Structure

신규 파일:

- `disputatio/Assets/godlotto/Script/FungusCommands/Menu/MenuAnchor.cs`
  — `MenuAnchor` enum + `MenuAnchorLayout` 순수 static 헬퍼(앵커→anchorMin/Max/pivot).
- `disputatio/Assets/godlotto/Script/FungusCommands/Menu/GlassMenuDialog.cs`
  — 동적 프리젠터 MonoBehaviour.
- `disputatio/Assets/godlotto/Script/FungusCommands/Menu/GlassMenuOption.cs`
  — `[Serializable]` 옵션 데이터.
- `disputatio/Assets/godlotto/Script/FungusCommands/Menu/GlassMenu.cs`
  — Fungus 커스텀 커맨드.
- `disputatio/Assets/godlotto/Script/Editor/GlassMenuPrefabBuilder.cs`
  — 다크 글래스 프리팹을 코드로 생성하는 에디터 메뉴.
- `disputatio/Assets/Editor/Tests/EditMode/UI/MenuAnchorLayoutTests.cs`
- `disputatio/Assets/Editor/Tests/EditMode/UI/GlassMenuTests.cs`
- `disputatio/Assets/Editor/Tests/EditMode/UI/GlassMenuDialogTests.cs`

생성 산출물(에디터 빌더 실행 결과):

- `disputatio/Assets/godlotto/Resources/Prefabs/GlassMenuOptionButton.prefab`
- `disputatio/Assets/godlotto/Resources/Prefabs/GlassMenuDialog.prefab`

---

## Task 1: 앵커 enum + 레이아웃 헬퍼 (순수 로직, TDD)

**Files:**
- Create: `disputatio/Assets/godlotto/Script/FungusCommands/Menu/MenuAnchor.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/UI/MenuAnchorLayoutTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

`disputatio/Assets/Editor/Tests/EditMode/UI/MenuAnchorLayoutTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class MenuAnchorLayoutTests
{
    [Test]
    public void Resolve_BottomCenter_AnchorsToBottomMiddle()
    {
        MenuAnchorLayout.Resolve(MenuAnchor.BottomCenter, out var min, out var max, out var pivot);

        Assert.AreEqual(new Vector2(0.5f, 0f), min);
        Assert.AreEqual(new Vector2(0.5f, 0f), max);
        Assert.AreEqual(new Vector2(0.5f, 0f), pivot);
    }

    [Test]
    public void Resolve_Center_AnchorsToMiddle()
    {
        MenuAnchorLayout.Resolve(MenuAnchor.Center, out var min, out var max, out var pivot);

        Assert.AreEqual(new Vector2(0.5f, 0.5f), min);
        Assert.AreEqual(new Vector2(0.5f, 0.5f), max);
        Assert.AreEqual(new Vector2(0.5f, 0.5f), pivot);
    }

    [Test]
    public void Resolve_TopRight_AnchorsToTopRight()
    {
        MenuAnchorLayout.Resolve(MenuAnchor.TopRight, out var min, out var max, out var pivot);

        Assert.AreEqual(new Vector2(1f, 1f), min);
        Assert.AreEqual(new Vector2(1f, 1f), max);
        Assert.AreEqual(new Vector2(1f, 1f), pivot);
    }

    [Test]
    public void Apply_SetsRectTransformAnchorsAndOffset()
    {
        var go = new GameObject("panel", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();

        MenuAnchorLayout.Apply(rt, MenuAnchor.BottomCenter, new Vector2(0f, 120f));

        Assert.AreEqual(new Vector2(0.5f, 0f), rt.anchorMin);
        Assert.AreEqual(new Vector2(0.5f, 0f), rt.pivot);
        Assert.AreEqual(new Vector2(0f, 120f), rt.anchoredPosition);

        Object.DestroyImmediate(go);
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

Run: Test Runner ▸ EditMode ▸ `MenuAnchorLayoutTests`
Expected: 컴파일 에러 또는 FAIL — `MenuAnchor`/`MenuAnchorLayout` 미정의.

- [ ] **Step 3: 최소 구현 작성**

`disputatio/Assets/godlotto/Script/FungusCommands/Menu/MenuAnchor.cs`:

```csharp
using UnityEngine;

/// <summary>
/// 선택지 메뉴 패널을 화면 어디에 정렬할지 정하는 9분할 앵커 프리셋.
/// </summary>
public enum MenuAnchor
{
    TopLeft, TopCenter, TopRight,
    MiddleLeft, Center, MiddleRight,
    BottomLeft, BottomCenter, BottomRight
}

/// <summary>
/// <see cref="MenuAnchor"/>를 uGUI RectTransform의 anchor/pivot 값으로 변환하는 순수 헬퍼.
/// MonoBehaviour 의존이 없어 EditMode에서 단독 테스트 가능합니다.
/// </summary>
public static class MenuAnchorLayout
{
    /// <summary>앵커 프리셋을 anchorMin/anchorMax/pivot 값으로 변환합니다(min==max==pivot).</summary>
    public static void Resolve(MenuAnchor anchor, out Vector2 min, out Vector2 max, out Vector2 pivot)
    {
        float x = HorizontalFactor(anchor);
        float y = VerticalFactor(anchor);
        min = new Vector2(x, y);
        max = new Vector2(x, y);
        pivot = new Vector2(x, y);
    }

    /// <summary>RectTransform에 앵커 프리셋을 적용하고 오프셋을 anchoredPosition으로 설정합니다.</summary>
    public static void Apply(RectTransform target, MenuAnchor anchor, Vector2 offset)
    {
        Resolve(anchor, out var min, out var max, out var pivot);
        target.anchorMin = min;
        target.anchorMax = max;
        target.pivot = pivot;
        target.anchoredPosition = offset;
    }

    static float HorizontalFactor(MenuAnchor a)
    {
        switch (a)
        {
            case MenuAnchor.TopLeft:
            case MenuAnchor.MiddleLeft:
            case MenuAnchor.BottomLeft:
                return 0f;
            case MenuAnchor.TopRight:
            case MenuAnchor.MiddleRight:
            case MenuAnchor.BottomRight:
                return 1f;
            default:
                return 0.5f;
        }
    }

    static float VerticalFactor(MenuAnchor a)
    {
        switch (a)
        {
            case MenuAnchor.TopLeft:
            case MenuAnchor.TopCenter:
            case MenuAnchor.TopRight:
                return 1f;
            case MenuAnchor.BottomLeft:
            case MenuAnchor.BottomCenter:
            case MenuAnchor.BottomRight:
                return 0f;
            default:
                return 0.5f;
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: Test Runner ▸ EditMode ▸ `MenuAnchorLayoutTests`
Expected: 4개 테스트 PASS.

- [ ] **Step 5: 커밋**

```bash
git add "disputatio/Assets/godlotto/Script/FungusCommands/Menu/MenuAnchor.cs" \
        "disputatio/Assets/godlotto/Script/FungusCommands/Menu/MenuAnchor.cs.meta" \
        "disputatio/Assets/Editor/Tests/EditMode/UI/MenuAnchorLayoutTests.cs" \
        "disputatio/Assets/Editor/Tests/EditMode/UI/MenuAnchorLayoutTests.cs.meta"
git commit -m "feat: add MenuAnchor preset and RectTransform layout helper"
```

(`.meta` 파일은 Unity가 자동 생성한다. 에디터에서 한 번 포커스해 생성된 뒤 함께 커밋한다.)

---

## Task 2: 옵션 데이터 타입 `GlassMenuOption`

**Files:**
- Create: `disputatio/Assets/godlotto/Script/FungusCommands/Menu/GlassMenuOption.cs`

데이터 전용 타입(직렬화). 단독 동작이 없어 별도 단위 테스트는 두지 않고 Task 3의 커맨드 테스트에서 함께 검증한다.

- [ ] **Step 1: 구현 작성**

`disputatio/Assets/godlotto/Script/FungusCommands/Menu/GlassMenuOption.cs`:

```csharp
using System;
using Fungus;
using UnityEngine;

/// <summary>
/// <see cref="GlassMenu"/>가 표시할 선택지 한 개. 리스트의 길이가 곧 선택지 개수입니다.
/// </summary>
[Serializable]
public class GlassMenuOption
{
    [Tooltip("버튼에 표시할 문구. Fungus 변수 치환을 지원합니다.")]
    [TextArea] public string text = "Option";

    [Tooltip("이 선택지를 고르면 실행할 블록.")]
    public Block targetBlock;

    [Tooltip("false면 표시되지만 선택할 수 없습니다(비활성/회색).")]
    public bool interactable = true;
}
```

- [ ] **Step 2: 컴파일 확인**

Unity 에디터로 포커스 → 콘솔에 컴파일 에러 없음 확인.

- [ ] **Step 3: 커밋**

```bash
git add "disputatio/Assets/godlotto/Script/FungusCommands/Menu/GlassMenuOption.cs" \
        "disputatio/Assets/godlotto/Script/FungusCommands/Menu/GlassMenuOption.cs.meta"
git commit -m "feat: add GlassMenuOption serializable choice data"
```

---

## Task 3: 프리젠터 `GlassMenuDialog` (위치/Clear/AddOption, TDD)

**Files:**
- Create: `disputatio/Assets/godlotto/Script/FungusCommands/Menu/GlassMenuDialog.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/UI/GlassMenuDialogTests.cs`

프로젝트 관례(예: `FungusDialogueBridge.ExecuteBlockHandlerForTests`)에 맞춰, 클릭 시 블록 실행을 테스트 가능한 **static seam**으로 분리한다.

- [ ] **Step 1: 실패하는 테스트 작성**

`disputatio/Assets/Editor/Tests/EditMode/UI/GlassMenuDialogTests.cs`:

```csharp
using Fungus;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[TestFixture]
public class GlassMenuDialogTests
{
    GameObject root;
    GlassMenuDialog dialog;
    Button buttonPrefab;

    [SetUp]
    public void SetUp()
    {
        GlassMenuDialog.BlockExecutorForTests = null;

        root = new GameObject("GlassMenuRoot", typeof(RectTransform));
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        var container = new GameObject("Container", typeof(RectTransform));
        container.transform.SetParent(panel.transform, false);

        // 최소 버튼 프리팹(런타임 GameObject로 대용): Button + 자식 TMP 텍스트
        var btnGo = new GameObject("OptionButton", typeof(RectTransform), typeof(Button));
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(btnGo.transform, false);
        buttonPrefab = btnGo.GetComponent<Button>();

        dialog = root.AddComponent<GlassMenuDialog>();
        SetPrivate(dialog, "panelRoot", panel.GetComponent<RectTransform>());
        SetPrivate(dialog, "optionContainer", container.GetComponent<RectTransform>());
        SetPrivate(dialog, "optionButtonPrefab", buttonPrefab);
    }

    [TearDown]
    public void TearDown()
    {
        GlassMenuDialog.BlockExecutorForTests = null;
        if (root != null) Object.DestroyImmediate(root);
        if (buttonPrefab != null) Object.DestroyImmediate(buttonPrefab.gameObject);
    }

    [Test]
    public void ApplyPlacement_SetsPanelAnchorAndOffset()
    {
        dialog.ApplyPlacement(MenuAnchor.BottomCenter, new Vector2(10f, 80f));

        var panel = (RectTransform)GetPrivate(dialog, "panelRoot");
        Assert.AreEqual(new Vector2(0.5f, 0f), panel.anchorMin);
        Assert.AreEqual(new Vector2(10f, 80f), panel.anchoredPosition);
    }

    [Test]
    public void AddOption_SpawnsButtonWithText()
    {
        dialog.AddOption("조심스럽게 펼쳐 읽는다", true, null);

        var container = (RectTransform)GetPrivate(dialog, "optionContainer");
        Assert.AreEqual(1, container.childCount);
        var label = container.GetChild(0).GetComponentInChildren<TextMeshProUGUI>();
        Assert.AreEqual("조심스럽게 펼쳐 읽는다", label.text);
    }

    [Test]
    public void Clear_RemovesSpawnedButtons()
    {
        dialog.AddOption("A", true, null);
        dialog.AddOption("B", true, null);

        dialog.Clear();

        var container = (RectTransform)GetPrivate(dialog, "optionContainer");
        Assert.AreEqual(0, container.childCount);
    }

    [Test]
    public void OptionClick_ExecutesTargetBlockThroughSeam()
    {
        Block executed = null;
        GlassMenuDialog.BlockExecutorForTests = b => executed = b;
        var block = root.AddComponent<Block>();
        block.BlockName = "Target";

        dialog.AddOption("go", true, block);
        var container = (RectTransform)GetPrivate(dialog, "optionContainer");
        container.GetChild(0).GetComponent<Button>().onClick.Invoke();

        Assert.AreEqual(block, executed);
    }

    static void SetPrivate(object t, string name, object value)
    {
        var f = t.GetType().GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        f.SetValue(t, value);
    }

    static object GetPrivate(object t, string name)
    {
        var f = t.GetType().GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return f.GetValue(t);
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

Run: Test Runner ▸ EditMode ▸ `GlassMenuDialogTests`
Expected: 컴파일 에러/FAIL — `GlassMenuDialog` 미정의.

- [ ] **Step 3: 최소 구현 작성**

`disputatio/Assets/godlotto/Script/FungusCommands/Menu/GlassMenuDialog.cs`:

```csharp
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
```

> 참고: EditMode 테스트는 `Destroy` 가 즉시 반영되지 않을 수 있으나, `Clear()`는 `Destroy` 호출 후 리스트를 비운다. `childCount` 검증을 위해 테스트에서는 `Destroy`가 프레임 종료 시 처리된다 — 만약 `Clear_RemovesSpawnedButtons`가 `childCount`로 실패하면, 구현의 `Destroy(...)`를 `DestroyImmediate(...)`로 바꾸지 말고 테스트를 `spawnedButtons` 카운트(0) 검증으로 조정한다. (프로덕션은 런타임이라 `Destroy`가 맞다.)

- [ ] **Step 4: 테스트 통과 확인**

Run: Test Runner ▸ EditMode ▸ `GlassMenuDialogTests`
Expected: 4개 PASS. (`Clear` 관련 실패 시 위 참고대로 카운트 검증으로 조정.)

- [ ] **Step 5: 커밋**

```bash
git add "disputatio/Assets/godlotto/Script/FungusCommands/Menu/GlassMenuDialog.cs" \
        "disputatio/Assets/godlotto/Script/FungusCommands/Menu/GlassMenuDialog.cs.meta" \
        "disputatio/Assets/Editor/Tests/EditMode/UI/GlassMenuDialogTests.cs" \
        "disputatio/Assets/Editor/Tests/EditMode/UI/GlassMenuDialogTests.cs.meta"
git commit -m "feat: add GlassMenuDialog dynamic choice presenter"
```

---

## Task 4: 커스텀 커맨드 `GlassMenu` (TDD)

**Files:**
- Create: `disputatio/Assets/godlotto/Script/FungusCommands/Menu/GlassMenu.cs`
- Test: `disputatio/Assets/Editor/Tests/EditMode/UI/GlassMenuTests.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

`disputatio/Assets/Editor/Tests/EditMode/UI/GlassMenuTests.cs`:

```csharp
using System.Collections.Generic;
using Fungus;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class GlassMenuTests
{
    GameObject root;
    GlassMenu command;
    Block blockA;
    Block blockB;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("GlassMenuCmdRoot");
        root.AddComponent<Flowchart>();
        command = root.AddComponent<GlassMenu>();
        blockA = root.AddComponent<Block>();
        blockA.BlockName = "BlockA";
        blockB = root.AddComponent<Block>();
        blockB.BlockName = "BlockB";

        SetPrivate(command, "options", new List<GlassMenuOption>
        {
            new GlassMenuOption { text = "A", targetBlock = blockA, interactable = true },
            new GlassMenuOption { text = "B", targetBlock = blockB, interactable = true },
        });
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
    }

    [Test]
    public void GetConnectedBlocks_ReturnsAllTargets()
    {
        var connected = new List<Block>();
        command.GetConnectedBlocks(ref connected);

        Assert.Contains(blockA, connected);
        Assert.Contains(blockB, connected);
        Assert.AreEqual(2, connected.Count);
    }

    [Test]
    public void GetSummary_ReportsOptionCount()
    {
        Assert.AreEqual("2 options", command.GetSummary());
    }

    [Test]
    public void GetSummary_NoOptions_ReportsError()
    {
        SetPrivate(command, "options", new List<GlassMenuOption>());
        StringAssert.Contains("Error", command.GetSummary());
    }

    [Test]
    public void MayCallBlock_TrueOnlyForTargets()
    {
        var unrelated = root.AddComponent<Block>();
        unrelated.BlockName = "Unrelated";

        Assert.IsTrue(((IBlockCaller)command).MayCallBlock(blockA));
        Assert.IsFalse(((IBlockCaller)command).MayCallBlock(unrelated));
    }

    static void SetPrivate(object t, string name, object value)
    {
        var f = t.GetType().GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        f.SetValue(t, value);
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인**

Run: Test Runner ▸ EditMode ▸ `GlassMenuTests`
Expected: 컴파일 에러/FAIL — `GlassMenu` 미정의.

- [ ] **Step 3: 최소 구현 작성**

`disputatio/Assets/godlotto/Script/FungusCommands/Menu/GlassMenu.cs`:

```csharp
using System.Collections.Generic;
using Fungus;
using UnityEngine;

/// <summary>
/// 다크 글래스 선택지 메뉴를 표시하는 Fungus 커맨드. 옵션 리스트의 길이가 선택지 개수이며,
/// 앵커 프리셋 + 오프셋으로 위치를 지정합니다. Fungus 원본 Menu 커맨드와 달리 커맨드 하나가
/// 여러 선택지를 모두 보유합니다. 블록의 마지막 커맨드로 사용하세요.
/// </summary>
[CommandInfo("Narrative",
             "Glass Menu",
             "다크 글래스 선택지 메뉴를 표시합니다(개수·문구·위치를 한 커맨드에서 지정).")]
[AddComponentMenu("")]
public class GlassMenu : Command, IBlockCaller
{
    [Tooltip("선택지 목록. 리스트 길이가 곧 선택지 개수입니다.")]
    [SerializeField] private List<GlassMenuOption> options = new List<GlassMenuOption>();

    [Tooltip("메뉴 패널을 화면 어디에 정렬할지.")]
    [SerializeField] private MenuAnchor anchor = MenuAnchor.BottomCenter;

    [Tooltip("앵커 기준 픽셀 오프셋.")]
    [SerializeField] private Vector2 menuOffset = Vector2.zero;

    [Tooltip("(선택) 특정 GlassMenuDialog로 override. 비우면 씬/Resources에서 자동 사용.")]
    [SerializeField] private GlassMenuDialog setMenuDialog;

    public override void OnEnter()
    {
        if (setMenuDialog != null)
            GlassMenuDialog.ActiveGlassMenuDialog = setMenuDialog;

        var dialog = GlassMenuDialog.GetMenuDialog();
        if (dialog == null)
        {
            GameLog.LogWarning("[GlassMenu] GlassMenuDialog를 찾거나 생성할 수 없습니다.");
            Continue();
            return;
        }

        dialog.SetActive(true);
        dialog.Clear();
        dialog.ApplyPlacement(anchor, menuOffset);

        var flowchart = GetFlowchart();
        foreach (var option in options)
        {
            if (option == null)
                continue;
            string displayText = flowchart.SubstituteVariables(option.text);
            dialog.AddOption(displayText, option.interactable, option.targetBlock);
        }

        Continue();
    }

    public override void GetConnectedBlocks(ref List<Block> connectedBlocks)
    {
        foreach (var option in options)
        {
            if (option != null && option.targetBlock != null)
                connectedBlocks.Add(option.targetBlock);
        }
    }

    public bool MayCallBlock(Block block)
    {
        foreach (var option in options)
        {
            if (option != null && option.targetBlock == block)
                return true;
        }
        return false;
    }

    public override string GetSummary()
    {
        if (options == null || options.Count == 0)
            return "Error: No options";
        return options.Count + " options";
    }

    public override Color GetButtonColor()
    {
        return new Color32(184, 210, 235, 255);
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: Test Runner ▸ EditMode ▸ `GlassMenuTests`
Expected: 4개 PASS. (회귀 확인: `Run All`로 Task 1·3 테스트도 PASS 유지.)

- [ ] **Step 5: 커밋**

```bash
git add "disputatio/Assets/godlotto/Script/FungusCommands/Menu/GlassMenu.cs" \
        "disputatio/Assets/godlotto/Script/FungusCommands/Menu/GlassMenu.cs.meta" \
        "disputatio/Assets/Editor/Tests/EditMode/UI/GlassMenuTests.cs" \
        "disputatio/Assets/Editor/Tests/EditMode/UI/GlassMenuTests.cs.meta"
git commit -m "feat: add GlassMenu Fungus command for glass choice menu"
```

---

## Task 5: 다크 글래스 프리팹 생성기 (에디터)

**Files:**
- Create: `disputatio/Assets/godlotto/Script/Editor/GlassMenuPrefabBuilder.cs`

`BackspaceUiPrefabBuilder` 관례를 따라 메뉴 항목에서 프리팹을 코드로 생성한다. 버튼 프리팹과 다이얼로그 프리팹을 `Assets/godlotto/Resources/Prefabs/` 에 만든다(자동 스폰용 `Resources/Prefabs/GlassMenuDialog` 경로 충족).

- [ ] **Step 1: 빌더 구현 작성**

`disputatio/Assets/godlotto/Script/Editor/GlassMenuPrefabBuilder.cs`:

```csharp
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 다크 글래스 선택지 메뉴 프리팹(버튼 + 다이얼로그)을 코드로 생성하는 에디터 도구.
/// 메뉴: Tools ▸ Godlotto ▸ Build Glass Menu Prefabs.
/// </summary>
public static class GlassMenuPrefabBuilder
{
    const string Dir = "Assets/godlotto/Resources/Prefabs";
    const string ButtonPath = Dir + "/GlassMenuOptionButton.prefab";
    const string DialogPath = Dir + "/GlassMenuDialog.prefab";

    // 다크 글래스 팔레트
    static readonly Color PanelFill = new Color(0f, 0f, 0f, 0.35f);
    static readonly Color GlassFill = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color GoldLine = new Color32(212, 175, 110, 255);
    static readonly Color LightText = new Color32(238, 242, 248, 255);

    [MenuItem("Tools/Godlotto/Build Glass Menu Prefabs")]
    public static void Build()
    {
        Directory.CreateDirectory(Dir);

        var buttonPrefab = BuildButtonPrefab();
        BuildDialogPrefab(buttonPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GlassMenuPrefabBuilder] 프리팹 생성 완료: " + DialogPath);
    }

    static GameObject BuildButtonPrefab()
    {
        var go = new GameObject("GlassMenuOptionButton",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(360f, 48f);

        var img = go.GetComponent<Image>();
        img.color = GlassFill;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = GoldLine;
        outline.effectDistance = new Vector2(1f, 1f);

        var button = go.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = GlassFill;
        colors.highlightedColor = new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.18f);
        colors.pressedColor = new Color(GoldLine.r, GoldLine.g, GoldLine.b, 0.30f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.03f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(16f, 0f);
        labelRt.offsetMax = new Vector2(-16f, 0f);
        var label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = "Option";
        label.color = LightText;
        label.fontSize = 22f;
        label.alignment = TextAlignmentOptions.MidlineLeft;

        var saved = PrefabUtility.SaveAsPrefabAsset(go, ButtonPath);
        Object.DestroyImmediate(go);
        return saved;
    }

    static void BuildDialogPrefab(GameObject buttonPrefab)
    {
        // 루트: 자체 Canvas(자동 스폰 시 단독 렌더 가능)
        var root = new GameObject("GlassMenuDialog",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
            typeof(GlassMenuDialog));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // 패널(위치 잡히는 글래스 루트)
        var panel = new GameObject("Panel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(root.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0f);
        panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = new Vector2(0f, 120f);
        panel.GetComponent<Image>().color = PanelFill;

        var panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = GoldLine;
        panelOutline.effectDistance = new Vector2(1f, 1f);

        var vlg = panel.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(14, 14, 14, 14);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 컨테이너 = 패널 자신(버튼이 패널의 VerticalLayoutGroup에 쌓이도록)
        var dialog = root.GetComponent<GlassMenuDialog>();
        SetSerialized(dialog, "panelRoot", panelRt);
        SetSerialized(dialog, "optionContainer", panelRt);
        SetSerialized(dialog, "optionButtonPrefab", buttonPrefab.GetComponent<Button>());

        PrefabUtility.SaveAsPrefabAsset(root, DialogPath);
        Object.DestroyImmediate(root);
    }

    static void SetSerialized(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
```

> 참고: `panelRoot`/`optionButtonPrefab`이 `[SerializeField] private`이므로 빌더는 `SerializedObject`로 주입한다. 자동 스폰 경로(`Resources.Load("Prefabs/GlassMenuDialog")`)를 만족시키려면 다이얼로그 프리팹이 반드시 `*/Resources/Prefabs/GlassMenuDialog.prefab` 위치여야 한다.

- [ ] **Step 2: 빌더 실행 + 프리팹 생성 확인**

Unity 에디터에서 `Tools ▸ Godlotto ▸ Build Glass Menu Prefabs` 실행.
Expected: 콘솔에 "프리팹 생성 완료" 로그. `Assets/godlotto/Resources/Prefabs/` 에 `GlassMenuOptionButton.prefab`, `GlassMenuDialog.prefab` 생성됨. 에러 없음.

- [ ] **Step 3: 커밋**

```bash
git add "disputatio/Assets/godlotto/Script/Editor/GlassMenuPrefabBuilder.cs" \
        "disputatio/Assets/godlotto/Script/Editor/GlassMenuPrefabBuilder.cs.meta" \
        "disputatio/Assets/godlotto/Resources" 
git commit -m "feat: add editor builder and generate glass menu prefabs"
```

(생성된 `.prefab`과 `.meta`, `Resources/` 폴더 메타까지 함께 커밋.)

---

## Task 6: 씬 통합 + 수동 플레이 검증

**Files:**
- Modify: (검증용 임시) 아무 테스트 씬의 Flowchart 한 블록

EditMode 테스트가 못 잡는 실제 런타임 동작(동적 버튼 렌더, 클릭 분기, 위치)을 한 번 확인한다.

- [ ] **Step 1: 테스트 블록 구성**

임의 테스트 씬의 Flowchart에서:
1. 새 블록 `GlassMenuDemo` 생성, 마지막 커맨드로 `Narrative ▸ Glass Menu` 추가.
2. `options`에 2~3개 추가(예: "조심스럽게 펼쳐 읽는다" → 블록 `OpenDiary`, "그대로 서랍에 넣는다" → 블록 `LeaveDiary").
3. 분기 대상 블록 2~3개를 만들고 각 블록에 `Say` 커맨드로 식별용 대사를 넣는다.
4. `anchor = BottomCenter`, `menuOffset = (0, 120)` 설정.

- [ ] **Step 2: 플레이 검증**

Play ▸ `GlassMenuDemo` 블록 실행.
Expected:
- 화면 하단 중앙에 다크 글래스 패널 + 옵션 수만큼 버튼이 뜬다.
- 호버 시 골드 글로우, 클릭 시 패널이 닫히고 해당 타깃 블록의 Say가 실행된다.
- `menuOffset`을 `(0, 300)` 으로 바꾸면 패널이 위로 이동, `anchor`를 `Center`로 바꾸면 중앙에 뜬다.
- 플로우차트 그래프에서 `Glass Menu` 커맨드 블록 → 각 타깃 블록으로 화살표가 그려진다.

- [ ] **Step 3: 검증 후 임시 데모 정리**

데모 블록/씬 변경을 되돌린다(커밋하지 않음). 실제 적용은 기획자가 진행.

- [ ] **Step 4: 전체 EditMode 회귀**

Run: Test Runner ▸ EditMode ▸ Run All
Expected: 신규 3개 픽스처(`MenuAnchorLayoutTests`, `GlassMenuDialogTests`, `GlassMenuTests`) 및 기존 테스트 전부 PASS.

---

## Task 7: 마이그레이션 가이드 문서

**Files:**
- Create: `docs/glass-choice-menu-usage.md`

- [ ] **Step 1: 사용 가이드 작성**

`docs/glass-choice-menu-usage.md` 에 기획자용 사용법을 적는다:
- `Narrative ▸ Glass Menu` 커맨드 추가 위치(블록 마지막).
- `options`(개수=리스트 길이)·`anchor`·`menuOffset`·`setMenuDialog` 필드 의미.
- 분기: 옵션별 `targetBlock` 지정.
- 룩 변경: `Tools ▸ Godlotto ▸ Build Glass Menu Prefabs` 재생성 또는 프리팹 직접 편집(팔레트 색: 패널 검정 35%, 보더 #D4AF6E, 텍스트 #EEF2F8).
- 제약: 타이머 없음, 실제 블러 미지원(근사), 블록 마지막 커맨드 규약.

- [ ] **Step 2: 커밋**

```bash
git add docs/glass-choice-menu-usage.md \
        docs/superpowers/specs/2026-06-06-glass-choice-menu-design.md \
        docs/superpowers/plans/2026-06-06-glass-choice-menu.md
git commit -m "docs: add glass choice menu usage guide, spec, and plan"
```

---

## Self-Review 메모 (작성자 검토 완료)

- **Spec 커버리지:** 다크 글래스 톤(Task 5 팔레트), 타이머 없음(Task 3 — 슬라이더 미구현), 단일 커맨드로 개수·문구(Task 2/4 `options`), 앵커+오프셋(Task 1/3/4), 옵션별 타깃 Block 분기(Task 3/4) — 모두 태스크에 매핑됨.
- **타입 일관성:** `GlassMenuDialog.AddOption(string, bool, Block)` / `ApplyPlacement(MenuAnchor, Vector2)` / `Clear()` / `BlockExecutorForTests` / `MenuAnchorLayout.Resolve·Apply` 시그니처가 호출부(GlassMenu, 테스트)와 일치.
- **에디터 주입:** `[SerializeField] private` 필드는 빌더에서 `SerializedObject`로 주입(Task 5).
- **알려진 리스크:** EditMode에서 `Destroy` 지연(Task 3 Step 3 참고로 대응). Unity 버전별 `FindFirstObjectByType` API는 기존 프로젝트 코드에서 이미 사용 중이라 호환 확인됨.
