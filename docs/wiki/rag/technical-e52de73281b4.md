---
source_id: technical:e52de73281b4
source_path: docs/fungus-room-migration-plan.md
source_sha256: e52de73281b4929b7f344995a979065b6e0f0fc9ecaa9ec29c5cc386377b3697
source_type: md
category: technical
title: fungus-room-migration-plan
status: extracted
rag_eligible: true
---

# 방/퍼즐 씬 Fungus 점진 이관 설계

> 작성일: 2026-06-06
> 범위: 고위험 룸/퍼즐 씬 6개 (`Kitchen`, `ChildRoom`, `StudyRoom`, `MaidRoom`, `BedRoom`, `WifeRoom`)
> 전제: [fungus-migration-audit.md](./fungus-migration-audit.md), 복도/입장 씬 `CorridorEntranceController` 패턴 완료
> 참고: `fungus-migration-audit.md`는 Prompt 4 이전 정적 감사 스냅샷입니다. Prompt 4 완료 후 `CorridorEntranceController`가 적용된 입장 씬은 본 문서의 룸 이관 범위에서 제외합니다.
> **이 문서는 설계만 다룹니다. 코드 변경은 하지 않습니다.**

---

## 0. 목표와 금지 사항

### 목표

Fungus Flowchart를 **한 번에 제거하지 않고**, 씬마다 아래 4축으로 분리한다.

| 축 | 의미 | 이관 우선순위 |
|----|------|--------------|
| **상태 (State)** | bool/진행 플래그, 퍼즐 완료, 아이템 소비 기록 | 2단계 (패널·클릭 안정 후) |
| **클릭 (Click)** | 월드/UI 진입점, 중복 클릭 방지, `ExecuteBlock` 직결 | **1단계 (최우선)** |
| **패널 (Panel)** | `SetActive`, 백스페이스, 모달 열기/닫기 | 1~2단계 |
| **퍼즐 (Puzzle)** | 드래그, 자물쇠, 봉인, 레시피 등 게임 규칙 | 기존 C# 유지·확장, Fungus outcome만 흡수 |

### 금지

- 6개 룸 **동시** 대규모 리팩터
- `Variablemanager` / `FlowchartLocator` **전역 일괄 교체**
- 기존 C# 퍼즐 코어(`CombinationLock`, `SealManager`, `FilterCardBookDropZone` 등) **로직 재작성**
- Say 대사를 C# 문자열로 **일괄 이전**

### 이미 있는 공통 기반 (재사용)

```
InteractionInputGate
SceneInteractionController.TryInteract
FungusDialogueBridge.ExecuteBlockSafely
SceneTransitionService / BackNavigator
ClickInteractionCleanup
CorridorEntranceController (복도·입장 참조 패턴)
WorldItemDropZone / InventorySlot (드롭·열쇠)
```

---

## 1. 씬 공통 아키텍처 (권장)

각 룸 씬 Flowchart GameObject에 **씬 전용 Controller** 1개를 둔다.

```
┌─────────────────────────────────────────────────────────┐
│  Room*Controller (씬별)                                  │
├─────────────────────────────────────────────────────────┤
│  [Click]  OnInteraction(id) → TryInteract → Fungus block │
│  [Panel]  OpenPanel / ClosePanel / OnPanelBackspace      │
│  [State]  Room*State (SerializeField + Fungus 미러)      │
│  [Puzzle] 기존 컴포넌트에 위임 (Lock, Seal, Book…)       │
│  [Outcome] BlockSignals.OnBlockEnd → LoadScene/GoBack   │
└─────────────────────────────────────────────────────────┘
         │ Say/Menu만              │ LoadScene, SetActive,
         ▼                         SetVariable, InvokeMethod
    Fungus Flowchart              C# (점진 흡수)
```

**원칙**

1. **진입점은 항상 C#** — `ObjectClicked`, `FungusClickTrigger`, UI `ExecuteBlock` 제거·대체.
2. **Fungus 블록은 “연출 단위”** — 클릭 한 번당 하나의 Say/Menu 시퀀스; 분기·상태 변경은 블록 **끝**에서 C# outcome.
3. **변수는 당분간 이중 기록** — C# `Room*State`가 source of truth를 향해 가되, `FlowchartLocator` bool 미러는 단계적으로만 제거.
4. **한 씬·한 축·한 PR** — 예: `BedRoom`의 “클릭 진입점만” PR, 다음 PR에서 “패널 SetActive”.

---

## 2. 씬별 상세 설계

### 2.1 Kitchen.unity (30 blocks, risk 115) — 최고 위험

**추천 Controller:** `KitchenInteractionController`
**보조 상태:** `KitchenPuzzleState` (Flowchart GameObject 컴포넌트, Fungus bool 미러)

#### R6-E 1차 이관 (2026-06-09) — 싱크/병/수도꼭지/드롭

| 상태 변수 | Fungus SetVariable/If (enabled) | C# 책임 (신규) | Fungus 유지 |
|-----------|----------------------------------|----------------|-------------|
| `GetBottle` | `Bottle_Clicked` If, `Sink`/`LookUp_Sink` 분기 | **읽기만** (`KitchenPuzzleState.HasBottle`) | 냉장고·복도 체인 SetVariable |
| `BottleClicked` | `Faucet` If, `Bottle_Clicked` SetVariable | 게이트 미러 + 블록 종료 시 `SetBottleClicked` | Say/패널 분기 |
| `FaucetClicked` | `FilledBottle` If, `Faucet` SetVariable | 게이트 미러 + `Faucet` 종료 시 `SetFaucetClicked` | `addKey` Call 등 연출 |
| `BottleDragged` | `Bottle_Dragged` SetVariable, `LookUp_Sink` If | **게이트** (`bottle_drag` 중복 차단) + 종료 시 `SetBottleDragged` | 패널 SetActive 등 |
| `isClicked` / `isTalking` | UI 잠금 전역 | 미이관 (기존 `ClickInteractionCleanup`) | 전역 잠금 |

**코드:** `KitchenPuzzleState`, `KitchenSinkInteractionGate`, `KitchenInteractionController` 오버라이드.
**씬:** `Kitchen.unity` Flowchart에 `KitchenPuzzleState` 배선.
**마이그레이터:** `Tools/Godlotto/Migrate/Kitchen R6-E Sink Puzzle State`

#### 블록 목록 (감사 기준)

`Start`, `Door_Clicked`, `Door_toHall_Clicked`, `refrigeratorClicked`, `TrashBox_Clicked`, `Sink`, `LookUp_Sink`, `Bottle_Clicked`, `Bottle_Dragged`, `Food_Dragged`, `Faucet`, `FilledBottle`, `burner`, `forActiveBurnerPannel`, `OnBurner_Yes/No`, `OffBurner_Yes/No`, `fripan`, `onFri`, `offFri`, `parret`, `give`, `dontGive`, `yes`, `no`, `addKey`, `Enable`, `Nothing`, `PannelBackspace`

#### C#으로 **먼저** 옮길 것

| 축 | 대상 | 비고 |
|----|------|------|
| **클릭 진입점** | `Door_Clicked`, `Door_toHall_Clicked`, `refrigeratorClicked`, `TrashBox_Clicked`, `Sink`, `Bottle_Clicked`, `fripan`, `parret` | `FungusClickTrigger` 5개 **제거·흡수**; `Clickable2D` 비활성 후 Controller 폴링 |
| **클릭 진입점** | UI `ExecuteBlock`: `burner`, `Faucet`, `FilledBottle`, `Bottle_Clicked` | `OnInteraction("burner")` 등으로 재배선 |
| **중복 클릭** | 모든 `OnInteraction` | `SceneInteractionController` + 씬별 cooldown |
| **드롭 진입점** | `Bottle_Dragged`, `Food_Dragged` | `WorldItemDropZone.onUnlock` → `OnInteraction("bottle_drag")` / `"food_drag"` (Fungus `ExecuteBlock` 제거) |
| **패널** | 버너 패널, 프라이팬, 앵무새 패널, `PannelBackspace` | Fungus `SetActive` 38건 → `KitchenPanelRegistry` |
| **LoadScene** | `Door_Clicked`, `Door_toHall_Clicked` | `OnBlockEnd` outcome (Hall_Left 등) |
| **SetVariable** | 병/음식/버너/키 관련 bool 25건 | `KitchenPuzzleState` + 필요 시 Fungus 미러 1줄 |

#### Fungus에 **당분간** 남길 것

| 유형 | 블록 예 | 이유 |
|------|---------|------|
| **Say** | 싱크·냉장고·버너·앵무새 대사 전반 (33 Say) | 대사량 최다, 기획 수정 빈번 |
| **Menu** | `OnBurner_Yes/No`, `give`/`dontGive`, `yes`/`no` | 선택지 문구·분기 연출 |
| **Wait / Fade** | 일부 연출 블록 | 단기 연출 |
| **InvokeMethod** | 앵무새 AI 관련 (7건) | 2차 이관 — C# 서비스로 대체 전까지 유지 |

#### 분리 순서 (씬 내부)

1. 클릭·드롭 진입점 + `FungusClickTrigger` 제거
2. 패널 `SetActive`
3. `SetVariable` → `KitchenPuzzleState`
4. Menu outcome → C# (선택지 UI는 후순위)
5. InvokeMethod / Parrot AI

#### 의존 씬 (동시 작업 금지)

`UtilityRoom`, `Hall_Left`, `Hallway_Left` (전기·병·주방 체인)

---

### 2.2 ChildRoom.unity (18 blocks, risk 79)

**추천 Controller:** `ChildRoomPuzzleController`
**기존 C#:** `SealManager`, `DragManager2D`, `WorldItemDropZone`, `SnapTarget`

#### 블록 목록

`Start`, `BackSpace_Clicked`, `Bedfloor_Clicked`, `Drawer_Clicked`, `DrawerOpen`, `DrawerClose`, `Chest_Clicked`, `Table_Clicked`, `Button_Clicked`, `Parrot_Clicked`, `BedDrawer_Backspace`, `Panel_Backspace`, `Drag_seal5/6/7`, `allSealsComplete`, `Select_Yes/No`

#### C#으로 **먼저** 옮길 것

| 축 | 대상 | 비고 |
|----|------|------|
| **클릭** | `Bedfloor`, `Drawer`, `Chest`, `Table`, `Button`, `Parrot` ObjectClicked | 진입점 단일화 |
| **클릭** | `DrawerOpen` / `DrawerClose` UI Button `ExecuteBlock` | `OnInteraction("drawer_open/close")` |
| **드롭** | `Drag_seal5/6/7` | `onUnlock` → `OnInteraction("seal_N")`; seal bool은 `WorldItemDropZone`이 이미 기록 |
| **퍼즐 outcome** | `allSealsComplete` | `SealManager.onAllSealsComplete` → Controller (현재 `ExecuteBlock` 연결) |
| **패널** | `BedDrawer_Backspace`, `Panel_Backspace` | SetActive 43건 중 패널 닫기 |
| **LoadScene** | `BackSpace_Clicked` → `Select_Yes` | `BackNavigator` / 고정 복귀 (`2floorHallway_Left`) |
| **SetVariable** | `seal1~7`, `allSealsComplete` | `SealManager`가 이미 감시·설정 — Fungus 중복 SetVariable 제거만 |

#### Fungus에 **당분간** 남길 것

| 유형 | 블록 | 이유 |
|------|------|------|
| **Say** | (1건) | 거의 없음 — 유지 비용 낮음 |
| **Menu** | `Select_Yes/No` (복귀 확인) | 복도 패턴과 동일 |

#### 분리 순서 (씬 내부)

1. 월드 클릭 + 서랍 UI 진입점
2. `Drag_seal*` / `allSealsComplete` outcome
3. 패널 백스페이스
4. `BackSpace` 복귀 흐름
5. 남은 SetActive 정리

#### 의존 씬

`ChildEntrance`, `DressingRoom`, `SealManager` 프리팹/씬 배치

---

### 2.3 StudyRoom.unity (17 blocks, risk 49)

**추천 Controller:** `StudyRoomPuzzleController`
**기존 C#:** `FilterCardBookDropZone`, `FilterCardBoundedDrag`, `FilterCardRotator`, `SceneBookOverlayOpener`, `BibleSpreadUI`, `BookPanelController`

#### 블록 목록

`Start`, `Bible_Clicked`, `BibleBackspace`, `BookCase1~4_Clicked`, `CardStack_Clicked`, `CardStackBackspace`, `Diary_Clicked`, `DiaryBackspace`, `DeskBackspace`, `StudyRoombackspace`, `UnlockSuccess`, `Hall_RightCross`, `SelectPre`, `Select_No`

#### C#으로 **먼저** 옮길 것

| 축 | 대상 | 비고 |
|----|------|------|
| **클릭** | `Bible`, `BookCase1~4` ObjectClicked | 책장 → BookCase 씬 LoadScene 5건 |
| **클릭** | `CardStack_Clicked`, `Diary_Clicked` UI `ExecuteBlock` | 이미 C# 패널과 연동 — Fungus 직결만 제거 |
| **드롭/퍼즐** | `UnlockSuccess` | `onUnlock` UnityEvent → Controller outcome |
| **패널** | `*Backspace` 블록 6종 | 패널 닫기 + `ClickInteractionCleanup` |
| **LoadScene** | `BookCase*_Clicked`, `Hall_RightCross` | C# `SceneTransitionService` |
| **SetVariable** | 해금·카드·다이어리 플래그 11건 | `StudyRoomProgressState`; 일기 보상은 `SceneBookOverlayOpener`와 정렬 |

#### Fungus에 **당분간** 남길 것

| 유형 | 블록 | 이유 |
|------|------|------|
| **Say** | (1건) | 최소 |
| **Menu** | `SelectPre`, `Select_No` | 서재 내비 확인 연출 |

#### 분리 순서 (씬 내부)

1. UI `ExecuteBlock` (`CardStack`, `Diary`) 제거
2. 책장·성경 클릭 → LoadScene outcome
3. `UnlockSuccess` / 백스페이스 패널
4. `SetVariable` 정리
5. FilterCard/BookOverlay와 상태 동기화 검증 ([filtercard 설계](./superpowers/specs/2026-05-22-studyroom-filtercard-book-panel-design.md) 준수)

#### 의존 씬

`BookCase1~4`, `PrisonEntrance`, `StudyEntrance`, `Hall_RightCross`

---

### 2.4 MaidRoom.unity (14 blocks, risk 65)

**추천 Controller:** `MaidRoomPuzzleController`
**기존 C#:** `CombinationLock`, `BookPanelController`, `PuzzleBookLoader`, `WorldItemDropZone`

#### 블록 목록

`Start`, `CookBook_Clicked`, `CookBook_SelectYes/No`, `PuzzleBook_Clicked`, `PuzzleBook_SelectYes/No`, `KeyShelf_Clicked`, `KeyShelf_SelectYes/No`, `drawer`, `food`, `UnlockSuccess`, `PanelBackspace`

#### C#으로 **먼저** 옮길 것

| 축 | 대상 | 비고 |
|----|------|------|
| **클릭** | `CookBook`, `PuzzleBook`, `KeyShelf`, `drawer`, `food` | 11 clickables 중 Fungus 핸들러 분리 |
| **Menu outcome** | `*_SelectYes/No` | Yes → 패널 열기/LoadScene, No → `resetIsClicked` (복도 패턴) |
| **퍼즐** | `UnlockSuccess` | `CombinationLock.onUnlockSuccess`가 이미 C# — Fungus `ExecuteBlock` 제거 |
| **패널** | `PanelBackspace`, 요리책/퍼즐북 패널 | `BookPanelController`와 연동 |
| **SetVariable** | `solved`, 열쇠·서랍 플래그 15건 | `MaidRoomPuzzleState`; `CombinationLock`의 `solved` bool 유지 |

#### Fungus에 **당분간** 남길 것

| 유형 | 블록 | 이유 |
|------|------|------|
| **Say** | 요리책·열쇠선반·서랍 대사 (7건) | 기획 대사 |
| **Menu** | `*_SelectYes/No` 문구 | 단기 유지 후 C# Choice로 이전 가능 |

#### 분리 순서 (씬 내부)

1. 오브젝트 클릭 진입점 (`CookBook`, `PuzzleBook`, `KeyShelf`, `drawer`, `food`)
2. `UnlockSuccess` / `CombinationLock` outcome 통합
3. `PanelBackspace`
4. `SetVariable` / `solved` 동기화
5. Menu 텍스트만 Fungus에 남기기

---

### 2.5 BedRoom.unity (14 blocks, risk 53)

**추천 Controller:** `BedRoomInteractionController`
**기존 C#:** `BookPanelController`, `WorldItemDropZone` (`onUnlock`)

#### 블록 목록

`Start`, `BackSpace_Clicked`, `Bookcase_Clicked`, `Safe_Clicked`, `Book_Clicked`, `Book_Yes/No`, `BookPanel_Backspace`, `Button_Clicked`, `Parrot_Clicked`, `Panel_Backspace`, `onUnlock`, `Select_Yes/No`

#### C#으로 **먼저** 옮길 것

| 축 | 대상 | 비고 |
|----|------|------|
| **클릭** | `Bookcase`, `Safe`, `Book`, `Button`, `Parrot` | 월드 클릭 통합 |
| **드롭** | `onUnlock` | 금고 해금 — outcome C# |
| **패널** | `BookPanel_Backspace`, `Panel_Backspace` | SetActive 29건 중심 |
| **LoadScene** | `BackSpace` → `Select_Yes` | `2floorHallway_Right` |
| **SetVariable** | 금고·책·버튼 플래그 7건 | `BedRoomState` |

#### Fungus에 **당분간** 남길 것

| 유형 | 블록 | 이유 |
|------|------|------|
| **Say** | (2건) | 소량 |
| **Menu** | `Book_Yes/No`, `Select_Yes/No` | 확인 대사 |

#### 분리 순서 (씬 내부)

1. 클릭 진입점
2. `onUnlock` 금고 outcome
3. 패널 백스페이스
4. `BackSpace` 복귀
5. SetVariable

**선정 이유:** Say 적고 패턴이 WifeRoom과 유사해 **첫 룸 파일럿**으로 적합.

---

### 2.6 WifeRoom.unity (14 blocks, risk 51)

**추천 Controller:** `WifeRoomPuzzleController`
**기존 C#:** `WorldItemDropZone`, `CombinationLock`(유사 패턴 가능)

#### 블록 목록

`Start`, `BackSpace_Clicked`, `Wallclock_Clicked`, `Wallclock_Backspace`, `DressDoor_Clicked`, `Dressingtable_Clicked`, `Drawer_Clicked`, `DownDrawer_Clicked`, `Drawer_Backspace`, `Lock_Backspace`, `Parrot_Clicked`, `UnlockSuccess`, `Select_Yes/No`

#### C#으로 **먼저** 옮길 것

| 축 | 대상 | 비고 |
|----|------|------|
| **클릭** | `Wallclock`, `DressDoor`, `Dressingtable`, `Drawer`, `DownDrawer`, `Parrot` | |
| **퍼즐** | `UnlockSuccess` | 드롭존/자물쇠 해금 outcome |
| **패널** | `*_Backspace`, 시계·화장대·서랍 패널 | SetActive 12건 |
| **LoadScene** | `BackSpace` 흐름 | `2floorHallway_Right` |
| **SetVariable** | 시계·서랍·해금 18건 | `WifeRoomState` |

#### Fungus에 **당분간** 남길 것

| 유형 | 블록 | 이유 |
|------|------|------|
| **Say** | (3건) | |
| **Menu** | `Select_Yes/No` | |

#### 분리 순서

BedRoom과 동일 5단계; 시계·화장대 패널이 추가됨.

---

## 3. Controller 명명 규칙

| 씬 | Controller | State (선택) | 역할 |
|----|------------|--------------|------|
| Kitchen | `KitchenInteractionController` | `KitchenPuzzleState` | 주방 전체 상호작용·패널·병/음식 상태 |
| ChildRoom | `ChildRoomPuzzleController` | `ChildRoomSealState` | 봉인·서랍·복귀 (SealManager 래핑) |
| StudyRoom | `StudyRoomPuzzleController` | `StudyRoomProgressState` | 책장·카드·다이어리·해금 |
| MaidRoom | `MaidRoomPuzzleController` | `MaidRoomPuzzleState` | 책·자물쇠·서랍 |
| BedRoom | `BedRoomInteractionController` | `BedRoomState` | 책·금고·복귀 |
| WifeRoom | `WifeRoomPuzzleController` | `WifeRoomState` | 시계·화장대·서랍 |

네임스페이스: `Godlotto.Interaction` (복도 Controller와 동일).

---

## 4. 마이그레이션 순서 (전체)

복도/입장(Phase 2) 완료를 전제로, **룸만** 단계화한다.

```
Phase R0 — 룸 공통 스캐폴드 (씬 YAML 변경 없음)
  · RoomInteractionController 베이스 패턴 문서화 (본 문서)
  · Editor 마이그레이터 스텁: CorridorEntranceSceneMigrator와 대칭 API
  · 씬별 interactionId ↔ blockName 레지스트리 초안

Phase R1 — BedRoom (파일럿)
  · 클릭 진입점만 이관, 회귀 최소

Phase R2 — WifeRoom
  · BedRoom 패턴 복제 + 시계/화장대 패널

Phase R3 — MaidRoom
  · CombinationLock / BookPanel 기존 C#와 outcome 연결

Phase R4 — StudyRoom
  · FilterCard·BookOverlay 기존 C# 보존, ExecuteBlock 3건 제거

Phase R5 — ChildRoom
  · SealManager 유지, allSealsComplete·Drag_seal 진입만 C#

Phase R6 — Kitchen (최종)
  · FungusClickTrigger 제거, UI+DropZone+월드 클릭 일원화
```

### 동시 작업 금지 조합

| 조합 | 이유 |
|------|------|
| Kitchen + UtilityRoom + Hall_Left | 1층 좌측 병/전기/주방 체인 |
| StudyRoom + BookCase1~4 + PrisonEntrance | 서재·지하 키·LoadScene 체인 |
| ChildRoom + ChildEntrance + DressingRoom | 봉인·옷장·입장 체인 |
| MaidRoom + MaidEntrance | 열쇠·onUnlock 공유 (입장은 이미 C#) |
| 6룸 중 2개 이상 “전체 블록” 동시 | 회귀 범위 폭발 |

---

## 5. 단계별 수동 테스트 체크리스트

각 Phase 완료 후 **해당 씬만** 플레이 모드에서 확인한다. 체크리스트는 축별로 공통 템플릿을 쓰고, 씬 특화 항목을 추가한다.

### 5.1 공통 (모든 Phase)

- [ ] 씬 진입 시 `Start` 블록 1회 실행, 이후 월드 클릭 가능
- [ ] Say 진행 중 월드 클릭 무시 (`isTalking` / `InteractionInputGate`)
- [ ] 동일 오브젝트 연타 시 블록 중복 실행 없음
- [ ] 패널 열림 → 백스페이스/닫기 → `isClicked` 리셋, 월드 클릭 복구
- [ ] Ribbon `Back` → 확인 메뉴(있는 경우) → 복귀 씬 정확
- [ ] 씬 재입장 시 해금·퍼즐 완료 상태 유지 (Fungus bool / checkpoint)
- [ ] EditMode: 해당 Controller 단위 테스트 추가·통과

### 5.2 Phase R1 — BedRoom

- [ ] 책장 클릭 → 의도한 Say/패널
- [ ] 금고 클릭 → 잠금/해금 분기
- [ ] 올바른 아이템 드롭 → `onUnlock` 후 금고 열림, Fungus `ExecuteBlock` 없이 동작
- [ ] 책 패널 Yes/No → 패널·인벤 상태 일치
- [ ] 앵무새 클릭 (있을 경우) → 대사 1회
- [ ] Back → `2floorHallway_Right`

### 5.3 Phase R2 — WifeRoom

- [ ] 시계 클릭 → 패널 열림, 백스페이스로 닫힘
- [ ] 화장대·서랍(상/하) 클릭 분기
- [ ] `UnlockSuccess` 후 진행 플래그·오브젝트 활성 상태
- [ ] Back → `2floorHallway_Right`

### 5.4 Phase R3 — MaidRoom

- [ ] 요리책 / 퍼즐북 / 열쇠선반 클릭 → Menu → Yes/No
- [ ] `CombinationLock` 정답 → `UnlockSuccess` outcome, 아이템 등장
- [ ] `drawer`, `food` 클릭
- [ ] `PanelBackspace`로 모든 패널 닫힘
- [ ] Back → `Hallway_Right`

### 5.5 Phase R4 — StudyRoom

- [ ] FilterCard 드롭 → 카드·회전 버튼 (기존 C# 동작 유지)
- [ ] CardStack / Diary UI → Fungus `ExecuteBlock` 없이 패널
- [ ] 성경·책장1~4 → 해당 BookCase / LoadScene
- [ ] `UnlockSuccess` 후 Prison·서재 진행 가능
- [ ] 일기 보상 (`SceneBookOverlayOpener`) 1회만 지급
- [ ] Back → `Hallway_Right`

### 5.6 Phase R5 — ChildRoom

- [ ] 침대·서랍·상자·테이블 클릭
- [ ] `DrawerOpen` / `DrawerClose` UI
- [ ] 5·6·7 인장 인벤 드롭 → `Drag_seal*` outcome, bool 저장
- [ ] 7개 봉인 완료 → `allSealsComplete` **1회만** (재입장 시 재실행 없음)
- [ ] Back → `2floorHallway_Left`

### 5.7 Phase R6 — Kitchen

- [ ] `FungusClickTrigger` 경로 없음 — 모든 클릭이 Controller 경유
- [ ] 냉장고·쓰레기통·싱크·병·프라이팬·앵무새 클릭
- [ ] 병 싱크 드롭 / 음식 버너 드롭 → 상태·패널
- [ ] 버너 UI On/Off Menu → 상태 반영
- [ ] 수도꼭지·채운 병 UI
- [ ] 문 → Hall / Hall_Left LoadScene
- [ ] `PannelBackspace`로 패널 전부 닫힘
- [ ] UtilityRoom 전기 ON 후 주방 오브젝트 상태 연동 (통합 플레이)

---

## 6. PR·커밋 단위 권장

| PR | 범위 | 예시 제목 |
|----|------|-----------|
| 1 | R0 스캐폴드 + 테스트 | `feat(interaction): room controller scaffold` |
| 2 | BedRoom 클릭만 | `migrate(bedroom): wire click entry to BedRoomInteractionController` |
| 3 | BedRoom 패널+outcome | `migrate(bedroom): panels and onUnlock outcomes` |
| 4~ | 씬별 2~3 PR | Wife → Maid → Study → Child → Kitchen |

**한 PR에 포함할 최대 범위:** 한 씬 · 한 축(클릭 / 패널 / 상태) · 관련 테스트 · 해당 씬 YAML만.

---

## 7. 완료 정의 (씬당)

해당 룸 씬이 “이관 완료”로 간주되려면:

1. `ObjectClicked` / `FungusClickTrigger` / UI `ExecuteBlock` **진입점 0건** (Say를 부르는 `ExecuteBlockSafely`는 Controller 내부만)
2. `LoadScene` / `CallMethod(GoBack)` Fungus 명령 **비활성** — C# outcome 처리
3. `SetActive`는 패널 ID 기반 C# API로 **90% 이상** 대체 (연출용 일시 깜빡임 제외)
4. `SetVariable`은 씬 로컬 상태로 **읽기/쓰기 단일 경로** (글로벌 키는 미러만)
5. Fungus 블록은 **Say + Menu + Wait/Fade** 위주로 잔존
6. §5 해당 씬 체크리스트 **전항목 통과**
7. [fungus-migration-audit.md](./fungus-migration-audit.md) §2 ExecuteBlock 표에서 해당 씬 **0건**

---

## 8. 참고

- 복도/입장 이관: `CorridorEntranceController`, `docs/fungus-migration-audit.md` Phase 2
- StudyRoom FilterCard: [2026-05-22-studyroom-filtercard-book-panel-design.md](./superpowers/specs/2026-05-22-studyroom-filtercard-book-panel-design.md)
- 전역 변수 키: `FungusVariableKeys`, `WorldItemDropZone.PersistBoolKeyForItem`
- 노션 캡스톤 스펙 §10 구현 현황 갱신은 **씬당 Phase 완료 시** 점진 반영

---

*본 문서는 정적 감사·기존 C# 코드 분석 기준입니다. 씬 YAML 세부 배선은 Unity 에디터에서 블록명 ↔ `clickableObject` 교차 검증 후 마이그레이터 레지스트리에 반영합니다.*
