# 대사 로그(백로그) — Unity 에디터 연동 가이드 (Cursor용)

> **목표**: 이미 작성된 C# 스크립트를 Unity 씬/프리팹에 연결해 대사 로그 기능을 동작시킨다.
> 이 문서만 보고 에디터 작업을 완료할 수 있도록 모든 경로·계층·설정값을 명시한다.
> **C# 스크립트는 절대 수정하지 말 것.** 에디터(씬/프리팹/인스펙터) 작업만 한다.

## 에디터 자동 구성 (수동만)

씬·프리팹 연동은 **컴파일/도메인 리로드 시 자동 실행되지 않는다.** (의도치 않은 IntroScene 전환·저장 방지)

Unity 메뉴에서 아래를 **직접** 실행한다:

| 메뉴 | 스타일 | entryPrefab | 패널 |
|------|--------|-------------|------|
| **`Tools ▸ Godlotto ▸ Setup Dialogue Log (Editor Guide)`** | ① Parchment Codex (기본) | `DialogueLogEntry_Parchment.prefab` | inset 10%/9% 양피지 |
| **`Tools ▸ Godlotto ▸ Setup Dialogue Log ▸ Parchment Codex (①)`** | ① | 동일 | 동일 |
| **`Tools ▸ Godlotto ▸ Setup Dialogue Log ▸ Legacy Notebook`** | 기존 다크 | `DialogueLogEntry.prefab` | 전체 화면 다크 |
| **`Tools ▸ Godlotto ▸ Setup Dialogue Log ▸ Dark Confession (⑤)`** | ⑤ | `DialogueLogEntry_DarkConfession.prefab` | 전체 화면 다크 |

공통 C# 헬퍼 (①·⑤ 구현 재사용):

- `DialogueLogVisualStyle` — 스타일 enum
- `DialogueLogStylePalette` — mockups.html rgba 팔레트 (`ParchmentCodex`, `DarkConfession`)
- `DialogueLogEntryView` — 화자/본문/구분선 분리 바인딩
- `DialogueLogLogic.IsNarration`, `FormatSpeakerLine`, `FormatSpeakerRichText` — 포맷 헬퍼

`DialogueLogPanel`은 `entryPrefab`에 `DialogueLogEntryView`가 있으면 구조화 렌더링, 없으면 기존 단일 TMP + `FormatEntry`로 폴백한다.

---

## 0. 사전 상태 (이미 완료됨 — 건드리지 말 것)

| 파일 | 역할 |
|------|------|
| `Assets/godlotto/Script/DialogueLog/DialogueLogEntry.cs` | 로그 1줄 데이터(이름+본문) struct |
| `Assets/godlotto/Script/DialogueLog/DialogueLogPanel.cs` | 싱글톤. 대사 캡처 + 패널 토글/렌더. **씬에 배치 + 인스펙터 연결 필요** |
| `Assets/godlotto/Script/DialogueLog/DialogueLogButton.cs` | 로그 열기 버튼 프록시. **버튼 오브젝트에 컴포넌트만 부착하면 자동 연결** |

동작 원리: `DialogueLogPanel`이 Fungus의 `WriterSignals.OnWriterState` 이벤트를 구독해
대사 1줄이 끝날 때(`WriterState.End`)마다 화자 이름·본문을 메모리 리스트에 누적한다.
`L` 키 또는 로그 버튼으로 스크롤 패널을 띄운다. **Fungus 패키지는 수정하지 않았다.**

확정 사양:
- 저장 범위: **세션 메모리만** (씬 전환엔 유지, 게임 종료 시 소멸)
- 열기: **버튼 + 단축키 `L`** (둘 다)
- 항목: **화자 이름 + 대사 텍스트** (초상화/음성 없음)

---

## 1. `DialogueLogPanel.cs` 의 인스펙터 필드 (연결 대상)

```
[Header("UI")]
GameObject  logPanel          // 로그 전체 패널 루트 (닫혀있을 때 비활성)
ScrollRect  scrollRect        // 스크롤 영역. content에 Vertical Layout Group 권장
GameObject  entryPrefab       // 항목 1줄 프리팹 (루트/자식에 TMP_Text 필수)

[Header("입력")]
KeyCode     logHotkey = L     // 기본값 그대로 두면 됨

[Header("캔버스 정렬 (SayDialog 위로)")]
string  canvasSortingLayerName = "Setting"   // 기본값 유효 (Sorting Layer에 존재함)
int     canvasSortingOrder    = 60           // 설정 패널(50)보다 위
```

> 항목 텍스트는 코드가 자동으로 `<b>이름</b>\n본문` 형태(리치 텍스트)로 채운다.
> 따라서 **entryPrefab의 TMP_Text는 Rich Text가 켜져 있어야 한다(기본 켜짐).**

---

## 2. 배치할 씬

**`Assets/Scenes/godlotto/IntroScene.unity`** — 여기에 `InGameSettingsPanel`(동일한 씬 유지 싱글톤)이
이미 배치돼 있다. `DialogueLogPanel`도 **같은 IntroScene에 1개만** 배치한다.
`PersistAcrossScenes => true` 라서 이후 모든 게임 씬까지 자동 유지된다.

> 참고 템플릿: `InGameSettingsPanel`의 캔버스/패널 구성을 그대로 흉내 내면 가장 안전하다.
> (캔버스 sorting, 입력 차단, timeScale 정지 로직이 동일하게 설계되어 있음.)

---

## 3. 만들 오브젝트 계층 (IntroScene 안)

```
DialogueLogManager                (빈 GameObject)
└─ Canvas  (Screen Space - Overlay)        ← Canvas + CanvasScaler + GraphicRaycaster
   │   - Sort Order는 코드가 런타임에 Setting/60으로 올림 (직접 설정 불필요)
   └─ LogPanel                   (★ 시작 시 비활성 / inspector의 logPanel)
      ├─ DimBackground           (Image, 반투명 검정, Stretch full)  ← 선택
      ├─ TitleText               (TMP, "대사 기록" 등)              ← 선택
      ├─ CloseButton             (Button)                          ← 선택, 아래 5번 참고
      └─ Scroll View             (ScrollRect)   ← inspector의 scrollRect
         ├─ Viewport             (Image + Mask, Stretch)
         │  └─ Content           (RectTransform)
         │       + Vertical Layout Group (Child Force Expand: Width 체크)
         │       + Content Size Fitter   (Vertical Fit: Preferred Size)
         └─ Scrollbar Vertical   (선택)
```

`DialogueLogManager`(또는 Canvas) 오브젝트에 **`DialogueLogPanel` 컴포넌트**를 부착한다.

### entryPrefab (항목 1줄 프리팹)

**① Parchment Codex** (`DialogueLogEntry_Parchment.prefab`):

- 루트: `LayoutElement` + `DialogueLogEntryView` + `VerticalLayoutGroup`
- `SpeakerRow/SpeakerText` — 화자(❧ 장식, 굵게)
- `BodyText` — 본문(나레이션은 italic)
- `Separator` — 항목 하단 얇은 구분선

**⑤ Dark Confession** (`DialogueLogEntry_DarkConfession.prefab`):

- 루트: `LayoutElement` + `DialogueLogEntryView` + `VerticalLayoutGroup`
- `SpeakerRow` — `HorizontalLayoutGroup`: `SpeakerText`(대문자·핏빛) + `SpeakerLine`(가로 1px, rgba 184,71,58,0.5)
- `BodyText` — 본문(나레이션은 italic, 화자 행 숨김)
- Content `VerticalLayoutGroup.spacing` = 18 (항목 간 넓은 간격)

**Legacy** (`DialogueLogEntry.prefab`):

- 루트(RectTransform) + **`TMP_Text`**
  - Font Asset: **`Assets/Font/NanumGothic SDF`**
  - Enable **Auto Size** 끄고 적당한 폰트 크기, **Wrapping: Enabled**
  - Rich Text 켜짐 — `FormatEntry`가 `<b>이름</b>\n본문` 채움

---

## 4. `DialogueLogPanel` 인스펙터 연결 (요약 체크리스트)

- [ ] `Log Panel` ← LogPanel 오브젝트
- [ ] `Scroll Rect` ← Scroll View의 ScrollRect
- [ ] `Entry Prefab` ← DialogueLogEntry 프리팹
- [ ] `Log Hotkey` = `L` (기본값)
- [ ] `Canvas Sorting Layer Name` = `Setting` (기본값)
- [ ] `Canvas Sorting Order` = `60` (기본값)
- [ ] **LogPanel 오브젝트는 씬 저장 시 비활성(체크 해제) 상태로 둘 것** (코드가 Awake에서 꺼주긴 하지만 명시적으로)

---

## 5. 로그 열기 버튼 (SayDialog 프리팹에 추가)

대사창에 "로그" 버튼을 넣는다. **씬 유지 싱글톤이라 OnClick에 인스펙터 드래그 연결이 불가**하므로
전용 프록시 컴포넌트를 쓴다.

대상 프리팹(스토리 대사에 쓰는 것 — 우선순위):
- `Assets/godlotto/Prefab/SayDialogGothic.prefab` (나눔고딕 기본 대사창으로 추정)
- 필요 시 `SayDialog.prefab`, `SayDialogP5.prefab` 등 실제 사용되는 대사창에도 동일 적용

작업:
1. 해당 SayDialog 프리팹 안, 기존 ContinueButton/이름 근처에 **Button** 하나 추가 (아이콘/텍스트 "로그").
2. 그 Button 오브젝트에 **`DialogueLogButton` 컴포넌트**를 부착한다. → 끝.
   - `DialogueLogButton`이 `Awake`에서 자동으로 `onClick → DialogueLogPanel.Instance.Toggle()` 연결.
   - **인스펙터 OnClick 수동 연결 불필요.**
3. (선택) 3번의 `CloseButton`에도 동일하게 `DialogueLogButton`을 붙이면 닫기 버튼으로 동작(토글).

---

## 6. 검증 (에디터 Play 모드)

1. **IntroScene부터** 플레이 시작 → 대사 몇 줄 진행
2. `L` 키 또는 로그 버튼 → 패널이 SayDialog **위에** 뜨고, 지나간 대사가 **이름+본문 순서대로** 표시
3. 패널 열린 동안 화면 클릭/스페이스로 대사가 진행되지 않는지(차단) 확인
4. `ESC` 또는 `L` 또는 닫기 버튼으로 닫고 → 게임 정상 진행
5. **씬 전환**(예: Intro→Main→Kitchen) 후 다시 로그 열기 → 이전 씬 대사가 **유지**되는지 확인
6. 나레이션(화자 이름 없는 대사)도 깨지지 않고 본문만 표시되는지 확인
7. 스크롤이 항목이 많을 때 정상 작동하고, 열 때 **맨 아래(최신)** 로 스크롤되는지 확인

### 흔한 함정
- 항목이 안 보임 → `entryPrefab`에 TMP_Text가 없거나, Content에 Vertical Layout Group/Content Size Fitter 누락
- `L` 눌러도 무반응 → `DialogueLogPanel`이 IntroScene에 배치 안 됐거나 `logPanel` 참조 누락 (`Instance == null`)
- 대사가 캡처 안 됨 → IntroScene이 아닌 씬부터 플레이해서 싱글톤이 생성되지 않은 경우. 반드시 IntroScene부터 시작
- 패널이 대사창에 가림 → Canvas가 코드의 Setting/60 정렬을 못 받는 구조. LogPanel이 Canvas 하위에 있는지 확인
