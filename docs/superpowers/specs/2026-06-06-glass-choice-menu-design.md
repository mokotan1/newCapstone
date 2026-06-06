# 다크 글래스 선택지 메뉴 (Fungus MenuDialog 대체) — 설계

작성일: 2026-06-06

## 목적

Fungus의 기본 선택지 UI(`Menu` 커맨드 + `MenuDialog`)를 프로젝트 전용
**다크 글래스(Dark Glass Minimal)** 톤의 선택지 창으로 대체한다. 기획자가 Fungus
플로우차트에서 **커맨드 하나로 선택지 개수·문구·분기**를 정하고, **메뉴 위치(앵커 +
오프셋)** 까지 지정할 수 있어야 한다.

이 작업은 진행 중인 "Fungus 로직 → C# 이관" 흐름의 일부다. 원본 Fungus의
`Menu.cs` / `MenuDialog.cs` 는 **건드리지 않고**, 병행 가능한 전용 커맨드와
프리젠터를 신규로 추가한다.

## 결정된 요구사항

- 비주얼 톤: **B · 다크 글래스 미니멀** (반투명 패널 + 얇은 금색 라인).
- **타이머 없음.**
- 선택지 개수와 문구를 **하나의 Fungus 커스텀 커맨드**에서 지정.
- 메뉴 위치를 기획자/개발자가 지정: **앵커 프리셋 필드 + Vector2 오프셋 필드**.
- 분기 처리: **옵션별 타깃 Block** (Fungus 정석 방식).

## 기존 구조 참고 (조사 결과)

- 원본 `Fungus.Menu : Command` 는 **선택지 1개당 커맨드 1개**를 쌓고, 각 커맨드가
  하나의 `targetBlock` 을 가리키며 `MenuDialog.AddOption(...)` 을 호출한다.
- `Fungus.MenuDialog` 는 **미리 배치된 고정 버튼 배열(`cachedButtons`)** 을
  재사용한다 → 선택지 최대 개수가 프리팹에 고정됨. 동적 개수에는 부적합.
- 프로젝트의 커스텀 커맨드 예시: `AddItemToInventory : Command`
  (`[CommandInfo(...)]`, `GetSummary`, `GetButtonColor` 패턴). 동일 패턴을 따른다.
- 프로젝트는 `BackspaceUiPrefabBuilder` 처럼 **에디터 스크립트로 프리팹을 코드 생성**
  하는 관례가 있다.

## 채택 접근 (1안): 전용 커맨드 + 전용 동적 프리젠터

선택지 개수가 가변이고 룩을 완전히 커스텀하므로, 고정 버튼 풀을 쓰는 `MenuDialog`
재활용(2안) 대신 **옵션 수만큼 버튼을 동적 인스턴스화**하는 전용 프리젠터를 만든다.
Fungus 원본 클래스는 수정하지 않는다.

## 구성 요소

### ① `GlassMenu : Command` (Fungus 커스텀 커맨드)

`[CommandInfo("Narrative", "Glass Menu", "다크 글래스 선택지 메뉴를 표시합니다")]`

직렬화 필드:

| 필드 | 타입 | 설명 |
|---|---|---|
| `options` | `List<GlassMenuOption>` | **리스트 크기 = 선택지 개수** |
| `anchor` | `MenuAnchor` (enum) | 9분할 프리셋. 기본 `BottomCenter` |
| `menuOffset` | `Vector2` | 앵커 기준 px 오프셋 |
| `setMenuDialog` | `GlassMenuDialog` | (선택) override 대상. 지정 시 이후 메뉴도 이 다이얼로그 사용 |

`GlassMenuOption` (직렬화 구조체/클래스):

| 필드 | 타입 | 설명 |
|---|---|---|
| `text` | `string` (`[TextArea]`) | 버튼 문구. 변수 치환 지원 |
| `targetBlock` | `Block` | 선택 시 실행할 블록 |
| `interactable` | `bool` (기본 true) | false면 표시되지만 비활성(회색) |

`MenuAnchor` enum(3×3): `TopLeft, TopCenter, TopRight, MiddleLeft, Center,
MiddleRight, BottomLeft, BottomCenter, BottomRight`.

메서드:

- `OnEnter()`:
  1. 다이얼로그 확보: `setMenuDialog ?? GlassMenuDialog.GetMenuDialog()`
  2. `dialog.Clear()`
  3. `dialog.ApplyPlacement(anchor, menuOffset)`
  4. 각 옵션: `flowchart.SubstituteVariables(text)` 후
     `dialog.AddOption(displayText, option.interactable, option.targetBlock)`
  5. `dialog.SetActive(true)`
  6. `Continue()`
- `GetConnectedBlocks(ref List<Block>)`: 모든 `targetBlock` 추가 →
  플로우차트에 분기 화살표 표시.
- `GetSummary()`: `"{n} options"` (n=0이면 경고 문구).
- `GetButtonColor()`: 원본 Menu와 유사한 푸른색 계열.
- `IBlockCaller.MayCallBlock(block)`: 옵션 타깃 중 하나면 true.

**규약**: 원본 Fungus와 동일하게 `GlassMenu` 는 **블록의 마지막 커맨드**로 둔다.
(`Continue()` 이후 같은 블록의 후속 커맨드는 즉시 실행되므로.)

### ② `GlassMenuDialog : MonoBehaviour` (동적 프리젠터)

인스펙터 참조:

- `RectTransform panelRoot` — 위치를 잡는 글래스 패널 루트.
- `RectTransform optionContainer` — `VerticalLayoutGroup` + `ContentSizeFitter`.
- `Button optionButtonPrefab` — 다크 글래스 버튼 프리팹(TMP 텍스트 포함).

멤버:

- `static GlassMenuDialog ActiveGlassMenuDialog { get; set; }`
- `static GlassMenuDialog GetMenuDialog()`: 씬에서 탐색 → 없으면
  `Resources.Load<GameObject>("Prefabs/GlassMenuDialog")` 로 자동 스폰
  (Fungus `MenuDialog.GetMenuDialog` 패턴). `CheckEventSystem()` 으로 EventSystem 보장.
- `AddOption(string text, bool interactable, Block targetBlock)`:
  - `optionButtonPrefab` 을 `optionContainer` 자식으로 인스턴스화.
  - TMP 텍스트 = `text`, `button.interactable = interactable`.
  - `onClick`: `EventSystem.current.SetSelectedGameObject(null)` → `Clear()` →
    `gameObject.SetActive(false)` →
    `targetBlock.GetFlowchart().StartCoroutine(CallBlock(targetBlock))`.
    (Fungus `MenuDialog.AddOption` 의 Block 버전과 동일 패턴.)
  - 생성된 버튼을 `List<Button> spawnedButtons` 로 추적.
- `Clear()`: `spawnedButtons` 의 모든 인스턴스 Destroy 후 리스트 비움.
  **슬라이더/타이머 없음.**
- `ApplyPlacement(MenuAnchor anchor, Vector2 offset)`: 앵커 프리셋 → `panelRoot`
  의 `anchorMin/anchorMax/pivot` 설정 후 `anchoredPosition = offset`.
  앵커→(anchor/pivot) 매핑은 **순수 static 함수로 분리**(테스트 대상).
- 옵션 추가 직후 `LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot)` 로
  N개 옵션에 맞춰 패널 크기 갱신. `OnEnable` 에서 `Canvas.ForceUpdateCanvases()`.
- `SetActive(bool)`, `IsActive()`.

### ③ 다크 글래스 비주얼 (프리팹)

- **패널**: 반투명 어두운 채움(검정 약 35% 알파) + 얇은 금색(#D4AF6E ≈
  `(212,175,110)`) 1px 보더 + 미세 세로 그라데이션, 모서리 반경 8px.
- **버튼**: 동일 글래스 톤, 라이트 텍스트(#EEF2F8 ≈ `(238,242,248)`),
  호버/선택 시 골드 글로우(`Button.ColorBlock` 트윈), 선택지 번호 글리프(①②③)는
  옵션(프리팹 토글).
- ⚠️ **블러 주의**: uGUI는 네이티브 backdrop-blur가 없다. 위 방식(반투명 채움 +
  보더 + 그라데이션)으로 글래스를 근사한다(목업 B와 시각적으로 거의 동일). 실제
  블러(UI 블러 셰이더/RenderTexture)는 **후속 옵션**으로 분리한다.

### ④ (선택) `GlassMenuPrefabBuilder` 에디터

`BackspaceUiPrefabBuilder` 관례를 따라, `GlassMenuDialog.prefab` 과 버튼 프리팹을
코드로 생성/갱신하는 에디터 스크립트. 포함 권장(프리팹 재생성·일관성). 미포함 시
프리팹은 수동 제작.

## 데이터 흐름

```
[기획자] GlassMenu 커맨드: 옵션 N개(text+targetBlock) + anchor + menuOffset
   │  런타임 OnEnter
   ▼
GlassMenuDialog.ApplyPlacement(anchor, offset)   → 패널 위치 확정
GlassMenuDialog.AddOption × N                     → 글래스 버튼 동적 생성
   │  플레이어 클릭
   ▼
option.targetBlock.StartExecution()               → 해당 블록으로 분기
```

## 배치 / 어셈블리

- 신규 스크립트는 기존 커스텀 커맨드와 같은 어셈블리에 둔다
  (`AddItemToInventory` 가 `using Fungus;` 로 정상 컴파일되므로 동일 위치/asmdef
  사용). 런타임: `Assets/godlotto/Script/FungusCommands/`(또는 신규
  `.../Menu/`), 에디터: `Assets/godlotto/Script/Editor/`.
- 자동 스폰용 프리팹은 `Resources/Prefabs/GlassMenuDialog.prefab` 경로에 배치.

## 테스트 (EditMode)

기존 `disputatio/Assets/Editor/Tests/EditMode/UI/` 에 추가:

- `GlassMenu.GetConnectedBlocks` 가 모든 타깃 블록을 반환.
- `GetSummary` 가 옵션 수를 정확히 표기(0개 경고 포함).
- 앵커 enum → (anchorMin/anchorMax/pivot) 매핑 순수 함수 검증(9개 프리셋).
- `MayCallBlock` 이 타깃에만 true.

UI 인스턴스화 등 MonoBehaviour 의존부는 순수 로직을 분리해 테스트 가능 범위를 넓힌다.

## 범위에서 제외 (YAGNI)

- 타이머/슬라이더(요청대로 제외)
- `hideIfVisited`(방문 시 숨김)
- 버튼 셔플(랜덤 순서)
- 실제 backdrop-blur 셰이더

필요해지면 후속 작업으로 분리.

## 열린 항목 / 이름

- 클래스명 `GlassMenu` / `GlassMenuDialog` 는 조정 가능(예: `ChoiceMenu`).
- ④ 에디터 빌더 포함 여부는 구현 계획 단계에서 최종 확정.
