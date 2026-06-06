# Fungus Flowchart 의존도 감사 리포트

> 작성일: 2026-06-06
> 범위: `disputatio/Assets/Scenes/**/*.unity` 및 지정 C# 브리지 스크립트
> 목표: Fungus를 **대사/연출 출력 도구**로 축소하고, 클릭·상태·씬전환·입력잠금은 C#으로 이관

---

## 요약

| 지표 | 값 |
|------|-----|
| 분석 씬 수 | 62 |
| Flowchart 블록 보유 씬 | 56 |
| 총 블록 수 (중복 이름 합산) | 399 |
| `ExecuteBlock` 직접 호출 (씬 내) | 43 |
| Clickable2D/Guarded 보유 씬 | 40 |
| ObjectClicked/Guarded 핸들러 보유 씬 | 43 |
| `FungusClickTrigger` 사용 씬 | 1 |
| `Variablemanager` 참조 씬/프리팹 | Hall_playerble, Opening_Office + BackNavigator 프리팹 |

### 핵심 발견

1. **이미 C# 브리지 레이어가 존재** — `InteractionLock`, `GuardedClickable2D`, `GuardedObjectClicked`, `FlowchartLocator`, `ClickInteractionCleanup`, `FungusClickTrigger`가 Fungus 클릭/잠금 문제를 부분 완화함.
2. **고위험 명령이 Say보다 압도적으로 많음** — 상위 씬(Kitchen 등)에서 `SetActive`·`SetVariable`·`If`가 대사(`Say`)보다 10배 이상.
3. **입구 씬 패턴이 반복** — `*Entrance.unity` 6개가 거의 동일한 Menu/LoadScene/Enter* 블록 구조.
4. **`ExecuteBlock` 직결은 43건** — UI Button·UnityEvent(onUnlock)·드롭존에서 Fungus 블록을 직접 호출.
5. **Kitchen은 이중 클릭 경로** — `Clickable2D` + `FungusClickTrigger` + UI Button `ExecuteBlock`이 혼재.

---

## 인프라 스크립트 분석 (이관 기반)

### `Clickable2D.cs` (Fungus, 수정됨)
- `DoPointerClick()`에서 `InteractionLock.AcquireForClick` → `ObjectClicked` 이벤트 발행.
- UI 오버레이·모달 Say 중복 클릭 방지 로직 포함.
- **이관 시**: 월드 클릭 진입점을 C# `IWorldClickable` 등으로 대체하고, Fungus는 `Say`/`Menu` 표시만 호출.

### `InteractionLock.cs` (프로젝트 커스텀)
- `BlockSignals.OnBlockStart/End` 구독으로 Fungus 블록 실행 중 전역 클릭 잠금.
- `ClickInteractionCleanup`, `BookOverlayPagedReader`, 테스트가 의존.
- **이관 시**: Fungus 블록 수명이 아닌 C# `InteractionSession` 수명에 묶어야 함.

### `FlowchartLocator.cs`
- `Variablemanager` 글로벌 Flowchart 및 `GlobalVariables` bool 조회.
- 15+ C# 스크립트가 의존 (`InventoryManager`, `BackNavigator`, `WorldItemDropZone` 등).
- **이관 시**: 게임 상태는 `GameStateService`/`ProgressSnapshot`으로, Fungus 변수는 표시 조건용으로만 축소.

### `ClickInteractionCleanup.cs`
- UI 경계(뒤로가기·지도·패널 닫기)에서 `InteractionLock.ForceUnlock` + `IsClicked`/`WindowClicked` bool 리셋.
- **이관 시**: C# UI 네비게이션의 공통 `OnUiBoundaryCrossed()` 훅으로 승격.

### `FungusClickTrigger.cs`
- `OnMouseDown`으로 `ExecuteBlock` 직접 호출 (Clickable2D/InteractionLock 우회 가능).
- Kitchen 등에서 5개 오브젝트에 부착.
- **이관 시**: 최우선 제거 대상 — C# `WorldClickRouter`로 통합.

### `GuardedClickable2D.cs` / `GuardedObjectClicked.cs`
- 연타·중복 ObjectClicked·UI 오클릭 방지 래퍼.
- MaidEntrance, StudyEntrance, TutorRoom 등 일부에만 적용.
- **이관 시**: C# 클릭 라우터에 흡수 후 Fungus 핸들러 제거.

---

## 1. 씬별 Flowchart 블록 목록

### `Mokotan/Basement.unity` (1 blocks, risk=0)

`Start`

### `Mokotan/Basement/BasementBrickRoom.unity` (1 blocks, risk=0)

`Start`

### `Mokotan/Basement/BasementExtractionRoom.unity` (1 blocks, risk=0)

`Start`

### `Mokotan/Basement/BasementHallway.unity` (6 blocks, risk=5)

`Door_Brick_Clicked`, `Door_Extraction_Clicked`, `Door_Observation_Clicked`, `Door_Research_Clicked`, `Entry_ToUpperBasement_Clicked`, `Start`

### `Mokotan/Basement/BasementObservationRoom.unity` (1 blocks, risk=0)

`Start`

### `Mokotan/Basement/BasementResearchRoom.unity` (3 blocks, risk=2)

`BackSpace_Panel`, `Desk_Clicked`, `Start`

### `Mokotan/CreateAnimate.unity` (1 blocks, risk=0)

`New Block`

### `Mokotan/First Floor/1floorRight/BookCase1.unity` (7 blocks, risk=21)

`AskGoPrison`, `BackSpace_Clicked`, `SelectNo`, `SelectYes`, `Select_No`, `Select_Yes`, `Start`

### `Mokotan/First Floor/1floorRight/BookCase2.unity` (6 blocks, risk=13)

`BackSpace_Clicked`, `BlueMidButton_Clicked`, `PrisonButton_Clicked`, `Select_No`, `Select_Yes`, `Start`

### `Mokotan/First Floor/1floorRight/BookCase2Back.unity` (2 blocks, risk=3)

`GoCenter`, `Start`

### `Mokotan/First Floor/1floorRight/BookCase3.unity` (4 blocks, risk=7)

`BackSpace_Clicked`, `Select_No`, `Select_Yes`, `Start`

### `Mokotan/First Floor/1floorRight/BookCase4.unity` (4 blocks, risk=7)

`BackSpace_Clicked`, `Select_No`, `Select_Yes`, `Start`

### `Mokotan/First Floor/1floorRight/Hall_Right.unity` (6 blocks, risk=18)

`Front_clicked`, `Medal_Clicked`, `Showcase_Clicked`, `Start`, `selectNo`, `selectYes`

### `Mokotan/First Floor/1floorRight/Hall_Right2.unity` (5 blocks, risk=14)

`Go_Front`, `Medal_Clicked`, `Start`, `selectNo`, `selectYes`

### `Mokotan/First Floor/1floorRight/Hall_RightCross.unity` (5 blocks, risk=15)

`Left_Clicked`, `Right_Clicked`, `SelectPre`, `Start`, `selectNo`

### `Mokotan/First Floor/1floorRight/Hallway_Right.unity` (6 blocks, risk=16)

`Front_clicked`, `Medal_Clicked`, `Showcase_Clicked`, `Start`, `selectNo`, `selectYes`

### `Mokotan/First Floor/1floorRight/Hallway_Right2.unity` (6 blocks, risk=14)

`Front_Clicked`, `Medal_Clicked`, `Showcase_Clicked`, `Start`, `selectNo`, `selectYes`

### `Mokotan/First Floor/1floorRight/MaidEntrance.unity` (9 blocks, risk=27)

`EnterMaidRoom`, `EnterNo`, `EnterYes`, `GoMadeRoom_No`, `GoMaidRoom_Yes`, `MaidRoom_Clicked`, `Start`, `selectNo`, `selectYes`

### `Mokotan/First Floor/1floorRight/MaidRoom.unity` (14 blocks, risk=65)

`CookBook_Clicked`, `CookBook_SelectNo`, `CookBook_SelectYes`, `KeyShelf_Clicked`, `KeyShelf_SelectNo`, `KeyShelf_SelectYes`, `PanelBackspace`, `PuzzleBook_Clicked`, `PuzzleBook_SelectNo`, `PuzzleBook_SelectYes`, `Start`, `UnlockSuccess`, `drawer`, `food`

### `Mokotan/First Floor/1floorRight/Prison.unity` (8 blocks, risk=25)

`BackSpace_Clicked`, `Basementkey`, `Corpse_Clicked`, `Note_Clicked`, `PanelBackspace`, `Select_No`, `Start`, `StudyRoom`

### `Mokotan/First Floor/1floorRight/PrisonEntrance.unity` (13 blocks, risk=36)

`BackSpace_Clicked`, `EnterPrison`, `Enter_No`, `Enter_Yes`, `GoPrison_No`, `GoPrison_Yes`, `Hall_RightCross`, `Jocker`, `LockBackspace`, `Lock_Clicked`, `SelectNo`, `Start`, `StudyRoom`

### `Mokotan/First Floor/1floorRight/StudyEntrance.unity` (7 blocks, risk=23)

`EnterNo`, `EnterStudyRoom`, `EnterYes`, `GoStudyRoom_No`, `GoStudyRoom_Yes`, `Start`, `StudyRoom_Clicked`

### `Mokotan/First Floor/1floorRight/StudyRoom.unity` (17 blocks, risk=49)

`BibleBackspace`, `Bible_Clicked`, `BookCase1_Clicked`, `BookCase2_Clicked`, `BookCase3_Clicked`, `BookCase4_Clicked`, `CardStackBackspace`, `CardStack_Clicked`, `DeskBackspace`, `DiaryBackspace`, `Diary_Clicked`, `Hall_RightCross`, `SelectPre`, `Select_No`, `Start`, `StudyRoombackspace`, `UnlockSuccess`

### `Mokotan/First Floor/1floorRight/StudyRoomCutScene.unity` (1 blocks, risk=1)

`Start`

### `Mokotan/First Floor/1foorLeft/Hall_Left.unity` (13 blocks, risk=36)

`Bottle`, `Bottle_Clicked`, `Clicked_No`, `Clicked_Yes`, `Front_clicked`, `PannelBackSpace`, `Photo`, `Pot_Clicked`, `Start`, `Take`, `don't_TakeS`, `selectNo`, `selectYes`

### `Mokotan/First Floor/1foorLeft/Hall_Left2.unity` (4 blocks, risk=10)

`Clicked_No`, `Clicked_Yes`, `Door_Clicked`, `start`

### `Mokotan/First Floor/1foorLeft/Hallway_Left.unity` (14 blocks, risk=40)

`Bottle`, `Clicked_Bottle`, `Gate_Clicked`, `PannelBackSpace`, `Photo`, `Pot_Clicked`, `Start`, `Take`, `backNo`, `backYes`, `backspace`, `don't_TakeS`, `selectNo`, `selectYes`

### `Mokotan/First Floor/1foorLeft/Hallway_Left2.unity` (4 blocks, risk=9)

`Clicked_No`, `Clicked_Yes`, `Go_Front`, `Start`

### `Mokotan/First Floor/1foorLeft/Kitchen.unity` (30 blocks, risk=115)

`Bottle_Clicked`, `Bottle_Dragged`, `Door_Clicked`, `Door_toHall_Clicked`, `Enable`, `Faucet`, `FilledBottle`, `Food_Dragged`, `LookUp_Sink`, `Nothing`, `OffBurner_No`, `OffBurner_Yes`, `OnBurner_No`, `OnBurner_Yes`, `PannelBackspace`, `Sink`, `Start`, `TrashBox_Clicked`, `addKey`, `burner`, `dontGive`, `forActiveBurnerPannel`, `fripan`, `give`, `no`, `offFri`, `onFri`, `parret`, `refrigeratorClicked`, `yes`

### `Mokotan/First Floor/1foorLeft/UtilityRoom.unity` (8 blocks, risk=45)

`BackSpace`, `OffSwitch_Clicked`, `OnSwitch_Clicked`, `Rotom_Clicked`, `Start`, `electrical control panel_Clicekd`, `select_No`, `select_Yes`

### `Mokotan/First Floor/GoPrisonAnimation.unity` (1 blocks, risk=2)

`Start`

### `Mokotan/First Floor/Hall_animate.unity` (1 blocks, risk=2)

`Parret_Animated`

### `Mokotan/First Floor/Hall_playerble.unity` (18 blocks, risk=57)

`BasementDoor_Clicked`, `EnterBasement`, `EnterNo`, `EnterYes`, `IsPlayedAnimation`, `Left_Clicked`, `Map`, `New Block`, `No`, `PannelBacksapce`, `Parret_Pannel`, `Right_Clicked`, `Start`, `Yes`, `fade out/Effect`, `selectNo`, `selectYes`, `stair_Clicked`

### `Mokotan/First Floor/POAnimation.unity` (1 blocks, risk=2)

`Start`

### `Mokotan/Opening_Mention _open.unity` (3 blocks, risk=10)

`Bell_Clicked`, `Door`, `Start`

### `Mokotan/Opening_Mention.unity` (3 blocks, risk=15)

`Bell_Clicked`, `Fance_Clicked`, `Start`

### `Mokotan/Opening_Office.unity` (1 blocks, risk=3)

`Start`

### `Mokotan/Second Floor/2floorHallway_Left.unity` (10 blocks, risk=30)

`BackSpace_Clicked`, `Front_Clicked`, `Panel_Backspace`, `Photo_Clicked`, `Select_No`, `Select_Yes`, `Showcase1_Clicked`, `Showcase2_Clicked`, `Showcase3_Clicked`, `Start`

### `Mokotan/Second Floor/2floorHallway_Right.unity` (7 blocks, risk=20)

`BackSpace_Clicked`, `Front_Clicked`, `Photo1_Clicked`, `Photo2_Clicked`, `Select_No`, `Select_Yes`, `Start`

### `Mokotan/Second Floor/2floorLeft.unity` (10 blocks, risk=30)

`BackSpace_Clicked`, `Go_Front`, `Panel_Backspace`, `Photo_Clicked`, `Select_No`, `Select_Yes`, `Showcase1_Clicked`, `Showcase2_Clicked`, `Showcase3_Clicked`, `Start`

### `Mokotan/Second Floor/2floorLeftCross.unity` (6 blocks, risk=16)

`2floorHallway_Left`, `Left_Clicked`, `Right_Clicked`, `SelectPre`, `Start`, `selectNo`

### `Mokotan/Second Floor/2floorMainHall.unity` (7 blocks, risk=23)

`BackSpace_Clicked`, `Hall_playerble`, `Jesus_Clicked`, `Left_Clicked`, `Right_Clicked`, `Select_No`, `Start`

### `Mokotan/Second Floor/2floorRight.unity` (7 blocks, risk=20)

`BackSpace_Clicked`, `Go_Front`, `Photo1_Clicked`, `Photo2_Clicked`, `Select_No`, `Select_Yes`, `Start`

### `Mokotan/Second Floor/2floorRightCross.unity` (6 blocks, risk=16)

`2floorHallway_Right`, `Left_Clicked`, `Right_Clicked`, `SelectPre`, `Start`, `selectNo`

### `Mokotan/Second Floor/BedEntrance.unity` (10 blocks, risk=32)

`BackSpace_Clicked`, `BedRoom_Clicked`, `EnterBedRoom`, `EnterNo`, `EnterYes`, `Go_No`, `Go_Yes`, `Select_No`, `Select_Yes`, `Start`

### `Mokotan/Second Floor/BedRoom.unity` (14 blocks, risk=53)

`BackSpace_Clicked`, `BookPanel_Backspace`, `Book_Clicked`, `Book_No`, `Book_Yes`, `Bookcase_Clicked`, `Button_Clicked`, `Panel_Backspace`, `Parrot_Clicked`, `Safe_Clicked`, `Select_No`, `Select_Yes`, `Start`, `onUnlock`

### `Mokotan/Second Floor/ChildEntrance.unity` (10 blocks, risk=32)

`BackSpace_Clicked`, `ChildRoom_Clicked`, `EnterChildRoom`, `EnterNo`, `EnterYes`, `Go_No`, `Go_Yes`, `Select_No`, `Select_Yes`, `Start`

### `Mokotan/Second Floor/ChildRoom.unity` (18 blocks, risk=79)

`BackSpace_Clicked`, `BedDrawer_Backspace`, `Bedfloor_Clicked`, `Button_Clicked`, `Chest_Clicked`, `Drag_seal5`, `Drag_seal6`, `Drag_seal7`, `DrawerClose`, `DrawerOpen`, `Drawer_Clicked`, `Panel_Backspace`, `Parrot_Clicked`, `Select_No`, `Select_Yes`, `Start`, `Table_Clicked`, `allSealsComplete`

### `Mokotan/Second Floor/DressingRoom.unity` (10 blocks, risk=24)

`BackSpace_Clicked`, `Calendar_Backspace`, `Calendar_Clicked`, `ClothesRack1`, `ClothesRack2`, `Drawer_Clicked`, `Select_No`, `Select_Yes`, `Start`, `WifeDoor_Clicked`

### `Mokotan/Second Floor/TutorEntrance.unity` (10 blocks, risk=32)

`BackSpace_Clicked`, `EnterNo`, `EnterTutorRoom`, `EnterYes`, `Go_No`, `Go_Yes`, `Select_No`, `Select_Yes`, `Start`, `TutorRoom_Clicked`

### `Mokotan/Second Floor/TutorRoom.unity` (10 blocks, risk=42)

`BackSpace_Clicked`, `Bookcase_Clicked`, `Desk_Clicked`, `Panel_Backspace`, `Select_No`, `Select_Yes`, `Start`, `WhiteBoard_Clicked`, `Window_Clicked`, `active Key`

### `Mokotan/Second Floor/WifeEntrance.unity` (10 blocks, risk=32)

`BackSpace_Clicked`, `EnterNo`, `EnterWifeRoom`, `EnterYes`, `Go_No`, `Go_Yes`, `Select_No`, `Select_Yes`, `Start`, `WifeRoom_Clicked`

### `Mokotan/Second Floor/WifeRoom.unity` (14 blocks, risk=51)

`BackSpace_Clicked`, `DownDrawer_Clicked`, `Drawer_Backspace`, `Drawer_Clicked`, `DressDoor_Clicked`, `Dressingtable_Clicked`, `Lock_Backspace`, `Parrot_Clicked`, `Select_No`, `Select_Yes`, `Start`, `UnlockSuccess`, `Wallclock_Backspace`, `Wallclock_Clicked`

### `godlotto/BetaEnd.unity` (1 blocks, risk=1)

`start`

### `godlotto/IntroScene.unity` (1 blocks, risk=15)

`start`

### `godlotto/MainMenuScene.unity` (3 blocks, risk=2)

`ConfigButton`, `New Block`, `StartButton`

### 블록 없음 씬

`Mokotan/CreateEffect.unity`, `Mokotan/MainHall_sfx.unity`, `Mokotan/MiniGame.unity`, `godlotto/CardTestScene.unity`, `godlotto/SettingScene.unity`, `godlotto/UITestScene.unity`

---

## 2. `Flowchart.ExecuteBlock` 직접 호출 목록

| 씬 | 호출 경로 | 블록 이름 | 비고 |
|-----|----------|----------|------|
| `Mokotan/First Floor/1floorRight/BookCase1.unity` | UI Button OnClick | `AskGoPrison` | source: - |
| `Mokotan/First Floor/1floorRight/BookCase2.unity` | UI Button OnClick | `PrisonButton_Clicked` | source: - |
| `Mokotan/First Floor/1floorRight/BookCase2.unity` | UI Button OnClick | `BlueMidButton_Clicked` | source: - |
| `Mokotan/First Floor/1floorRight/MaidEntrance.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `EnterMaidRoom` | source: - |
| `Mokotan/First Floor/1floorRight/MaidRoom.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `UnlockSuccess` | source: - |
| `Mokotan/First Floor/1floorRight/PrisonEntrance.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `EnterPrison` | source: Jocker |
| `Mokotan/First Floor/1floorRight/StudyEntrance.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `EnterStudyRoom` | source: - |
| `Mokotan/First Floor/1floorRight/StudyRoom.unity` | UI Button OnClick | `CardStack_Clicked` | source: - |
| `Mokotan/First Floor/1floorRight/StudyRoom.unity` | UI Button OnClick | `Diary_Clicked` | source: - |
| `Mokotan/First Floor/1floorRight/StudyRoom.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `UnlockSuccess` | source: - |
| `Mokotan/First Floor/1foorLeft/Hall_Left.unity` | UI Button OnClick | `Bottle` | source: - |
| `Mokotan/First Floor/1foorLeft/Hallway_Left.unity` | UI Button OnClick | `Bottle` | source: - |
| `Mokotan/First Floor/1foorLeft/Kitchen.unity` | UI Button OnClick | `burner` | source: - |
| `Mokotan/First Floor/1foorLeft/Kitchen.unity` | UI Button OnClick | `Bottle_Clicked` | source: - |
| `Mokotan/First Floor/1foorLeft/Kitchen.unity` | UI Button OnClick | `Faucet` | source: - |
| `Mokotan/First Floor/1foorLeft/Kitchen.unity` | UI Button OnClick | `Faucet` | source: - |
| `Mokotan/First Floor/1foorLeft/Kitchen.unity` | UI Button OnClick | `FilledBottle` | source: - |
| `Mokotan/First Floor/1foorLeft/Kitchen.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `Bottle_Dragged` | source: SinkDropzone |
| `Mokotan/First Floor/1foorLeft/Kitchen.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `Food_Dragged` | source: BurnerDropzone |
| `Mokotan/First Floor/1foorLeft/UtilityRoom.unity` | UI Button OnClick | `OnSwitch_Clicked` | source: - |
| `Mokotan/First Floor/1foorLeft/UtilityRoom.unity` | UI Button OnClick | `OffSwitch_Clicked` | source: - |
| `Mokotan/First Floor/Hall_playerble.unity` | UI Button OnClick | `(empty)` | source: - |
| `Mokotan/First Floor/Hall_playerble.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `EnterBasement` | source: BasementDoor |
| `Mokotan/Opening_Mention _open.unity` | UI Button OnClick | `Bell_Clicked` | source: - |
| `Mokotan/Opening_Mention.unity` | UI Button OnClick | `Bell_Clicked` | source: - |
| `Mokotan/Second Floor/BedEntrance.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `EnterBedRoom` | source: Bed_Door |
| `Mokotan/Second Floor/BedRoom.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `onUnlock` | source: - |
| `Mokotan/Second Floor/ChildEntrance.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `EnterChildRoom` | source: Child_Door |
| `Mokotan/Second Floor/ChildRoom.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `Drag_seal7` | source: 7th seal Target |
| `Mokotan/Second Floor/ChildRoom.unity` | UI Button OnClick | `DrawerClose` | source: - |
| `Mokotan/Second Floor/ChildRoom.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `Drag_seal6` | source: 6th seal Target |
| `Mokotan/Second Floor/ChildRoom.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `Drag_seal5` | source: - |
| `Mokotan/Second Floor/ChildRoom.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `allSealsComplete` | source: - |
| `Mokotan/Second Floor/ChildRoom.unity` | UI Button OnClick | `DrawerOpen` | source: - |
| `Mokotan/Second Floor/DressingRoom.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `EnterChildRoom` | source: ClothesRack1 |
| `Mokotan/Second Floor/DressingRoom.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `EnterChildRoom` | source: ClothesRack2 |
| `Mokotan/Second Floor/DressingRoom.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `EnterChildRoom` | source: Drawer |
| `Mokotan/Second Floor/DressingRoom.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `EnterChildRoom` | source: Calendar |
| `Mokotan/Second Floor/TutorEntrance.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `EnterTutorRoom` | source: TutorRoom_Door |
| `Mokotan/Second Floor/WifeEntrance.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `EnterWifeRoom` | source: Wife_Door |
| `Mokotan/Second Floor/WifeRoom.unity` | C# UnityEvent (onUnlock/onUnlockSuccess/etc.) | `UnlockSuccess` | source: - |
| `godlotto/MainMenuScene.unity` | UI Button OnClick | `ConfigButton` | source: - |
| `godlotto/MainMenuScene.unity` | UI Button OnClick | `StartButton` | source: - |

### C# 코드에서 `ExecuteBlock` 호출 (씬 외)

| 스크립트 | 용도 |
|----------|------|
| `FungusClickTrigger` | OnMouseDown → 블록 실행 |
| `ItemPickup` | 아이템 획득 후 블록 실행 |
| `PanelBackspaceCloser` | 패널 닫기 시 블록 실행 |
| `SceneBookOverlayOpener` | 책 오버레이 후 블록 실행 |
| `FaucetKeyReleaseController` | 퍼즐 완료 후 블록 실행 |
| `AnimEndToFungus` | 애니 종료 후 블록 실행 |

---

## 3. Clickable2D / ObjectClicked 상호작용

### Clickable2D (또는 Guarded) 오브젝트

| 씬 | 오브젝트 |
|-----|---------|
| `Mokotan/Basement/BasementHallway.unity` | - |
| `Mokotan/Basement/BasementResearchRoom.unity` | - |
| `Mokotan/First Floor/1floorRight/BookCase2Back.unity` | GoCenter |
| `Mokotan/First Floor/1floorRight/Hall_Right.unity` | Medal, Front, Frame |
| `Mokotan/First Floor/1floorRight/Hall_Right2.unity` | Go_Front |
| `Mokotan/First Floor/1floorRight/Hall_RightCross.unity` | Go_Right, Go_Left |
| `Mokotan/First Floor/1floorRight/Hallway_Right.unity` | Front |
| `Mokotan/First Floor/1floorRight/Hallway_Right2.unity` | Front |
| `Mokotan/First Floor/1floorRight/MaidEntrance.unity` | MaidRoom_Door (Guarded) (Guarded: MaidRoom_Door) |
| `Mokotan/First Floor/1floorRight/MaidRoom.unity` | food, KeyShelf, drawer_Opened, CookBook, Desk, PuzzleBook, Chair,  (Guarded), Bed, drawer (Guarded: ) |
| `Mokotan/First Floor/1floorRight/Prison.unity` | - |
| `Mokotan/First Floor/1floorRight/PrisonEntrance.unity` | Lock |
| `Mokotan/First Floor/1floorRight/StudyEntrance.unity` | Study_Door (Guarded) (Guarded: Study_Door) |
| `Mokotan/First Floor/1floorRight/StudyRoom.unity` | BookCase1, BookCase3, BookCase2, Bible, BookCase4 |
| `Mokotan/First Floor/1foorLeft/Hall_Left.unity` | Photo, Pot, Front |
| `Mokotan/First Floor/1foorLeft/Hall_Left2.unity` | Door |
| `Mokotan/First Floor/1foorLeft/Hallway_Left.unity` | Photo, Gate, Pot |
| `Mokotan/First Floor/1foorLeft/Hallway_Left2.unity` | Go_Front |
| `Mokotan/First Floor/1foorLeft/Kitchen.unity` | Fripan, Parret, Burner |
| `Mokotan/First Floor/1foorLeft/UtilityRoom.unity` | washing machine, electrical control panel |
| `Mokotan/First Floor/Hall_playerble.unity` | Go_Left, Go_Right, Go_2floor |
| `Mokotan/Opening_Mention _open.unity` | - |
| `Mokotan/Opening_Mention.unity` | - |
| `Mokotan/Opening_Office.unity` | - |
| `Mokotan/Second Floor/2floorHallway_Left.unity` | Photo, Go_Front, Showcase1, Showcase3, Showcase2 |
| `Mokotan/Second Floor/2floorHallway_Right.unity` | Go_Front, Photo2, Photo1 |
| `Mokotan/Second Floor/2floorLeft.unity` | Showcase1, Go_Front, Showcase2, Showcase3, Photo |
| `Mokotan/Second Floor/2floorLeftCross.unity` | Go_Right, Go_Left |
| `Mokotan/Second Floor/2floorMainHall.unity` | Left_Door, Right_Door, Jesus |
| `Mokotan/Second Floor/2floorRight.unity` | Go_Front, Photo2, Photo1 |
| `Mokotan/Second Floor/2floorRightCross.unity` | Go_Right, Go_Left |
| `Mokotan/Second Floor/BedEntrance.unity` | - |
| `Mokotan/Second Floor/BedRoom.unity` | Bookcase, Safe |
| `Mokotan/Second Floor/ChildEntrance.unity` | - |
| `Mokotan/Second Floor/ChildRoom.unity` | Bedfloor, Drawer, Chest, Table |
| `Mokotan/Second Floor/DressingRoom.unity` | Wife_Door |
| `Mokotan/Second Floor/TutorEntrance.unity` | - |
| `Mokotan/Second Floor/TutorRoom.unity` | Nest,  (Guarded), Bookshelf, Key, Window, WhiteBoard (Guarded: ) |
| `Mokotan/Second Floor/WifeEntrance.unity` | - |
| `Mokotan/Second Floor/WifeRoom.unity` | Wallclock, Dress_Door, Drawer, Dressingtable |

### FungusClickTrigger (Clickable2D 우회 경로)

| 씬 | 오브젝트 | target block |
|-----|---------|--------------|
| `Mokotan/First Floor/1foorLeft/Kitchen.unity` |  | `Door_toHall_Clicked` |
| `Mokotan/First Floor/1foorLeft/Kitchen.unity` |  | `TrashBox_Clicked` |
| `Mokotan/First Floor/1foorLeft/Kitchen.unity` |  | `Door_Clicked` |
| `Mokotan/First Floor/1foorLeft/Kitchen.unity` |  | `refrigeratorClicked` |
| `Mokotan/First Floor/1foorLeft/Kitchen.unity` |  | `Sink` |

### ObjectClicked / GuardedObjectClicked 핸들러 수

| 씬 | ObjectClicked | GuardedObjectClicked |
|-----|---------------|----------------------|
| `Mokotan/Basement/BasementHallway.unity` | 5 | 0 |
| `Mokotan/Basement/BasementResearchRoom.unity` | 0 | 1 |
| `Mokotan/First Floor/1floorRight/BookCase1.unity` | 1 | 0 |
| `Mokotan/First Floor/1floorRight/BookCase2.unity` | 1 | 0 |
| `Mokotan/First Floor/1floorRight/BookCase2Back.unity` | 1 | 0 |
| `Mokotan/First Floor/1floorRight/BookCase3.unity` | 1 | 0 |
| `Mokotan/First Floor/1floorRight/BookCase4.unity` | 1 | 0 |
| `Mokotan/First Floor/1floorRight/Hall_Right.unity` | 3 | 0 |
| `Mokotan/First Floor/1floorRight/Hall_Right2.unity` | 2 | 0 |
| `Mokotan/First Floor/1floorRight/Hall_RightCross.unity` | 2 | 0 |
| `Mokotan/First Floor/1floorRight/Hallway_Right.unity` | 3 | 0 |
| `Mokotan/First Floor/1floorRight/Hallway_Right2.unity` | 3 | 0 |
| `Mokotan/First Floor/1floorRight/MaidEntrance.unity` | 0 | 1 |
| `Mokotan/First Floor/1floorRight/MaidRoom.unity` | 5 | 0 |
| `Mokotan/First Floor/1floorRight/Prison.unity` | 4 | 0 |
| `Mokotan/First Floor/1floorRight/PrisonEntrance.unity` | 3 | 0 |
| `Mokotan/First Floor/1floorRight/StudyEntrance.unity` | 0 | 1 |
| `Mokotan/First Floor/1floorRight/StudyRoom.unity` | 6 | 0 |
| `Mokotan/First Floor/1foorLeft/Hall_Left.unity` | 4 | 0 |
| `Mokotan/First Floor/1foorLeft/Hall_Left2.unity` | 1 | 0 |
| `Mokotan/First Floor/1foorLeft/Hallway_Left.unity` | 5 | 0 |
| `Mokotan/First Floor/1foorLeft/Hallway_Left2.unity` | 1 | 0 |
| `Mokotan/First Floor/1foorLeft/Kitchen.unity` | 2 | 0 |
| `Mokotan/First Floor/1foorLeft/UtilityRoom.unity` | 3 | 0 |
| `Mokotan/First Floor/Hall_playerble.unity` | 6 | 0 |
| `Mokotan/Opening_Mention _open.unity` | 1 | 0 |
| `Mokotan/Opening_Mention.unity` | 1 | 0 |
| `Mokotan/Second Floor/2floorHallway_Left.unity` | 6 | 0 |
| `Mokotan/Second Floor/2floorHallway_Right.unity` | 4 | 0 |
| `Mokotan/Second Floor/2floorLeft.unity` | 3 | 0 |
| `Mokotan/Second Floor/2floorLeftCross.unity` | 2 | 0 |
| `Mokotan/Second Floor/2floorMainHall.unity` | 4 | 0 |
| `Mokotan/Second Floor/2floorRight.unity` | 4 | 0 |
| `Mokotan/Second Floor/2floorRightCross.unity` | 2 | 0 |
| `Mokotan/Second Floor/BedEntrance.unity` | 2 | 0 |
| `Mokotan/Second Floor/BedRoom.unity` | 5 | 0 |
| `Mokotan/Second Floor/ChildEntrance.unity` | 2 | 0 |
| `Mokotan/Second Floor/ChildRoom.unity` | 6 | 0 |
| `Mokotan/Second Floor/DressingRoom.unity` | 6 | 0 |
| `Mokotan/Second Floor/TutorEntrance.unity` | 2 | 0 |
| `Mokotan/Second Floor/TutorRoom.unity` | 5 | 0 |
| `Mokotan/Second Floor/WifeEntrance.unity` | 2 | 0 |
| `Mokotan/Second Floor/WifeRoom.unity` | 6 | 0 |

> ObjectClicked 블록 이름은 Flowchart 에디터의 Event Handler 블록명과 1:1 대응. 씬 YAML에서는 handler GameObject 이름이 비어 있는 경우가 많아, 실제 매핑은 Unity 에디터에서 블록명 ↔ clickableObject 참조로 확인 필요.

---

## 4. 고위험 씬 (SetVariable / SetActive / If / Menu / LoadScene / InvokeMethod)

고위험 점수 = 6개 명령 GUID 출현 횟수 합산.

| 순위 | 씬 | 점수 | SetVar | SetActive | If | Menu | LoadScene | Invoke | Blocks | Say |
|------|-----|------|--------|-----------|-----|------|-----------|--------|--------|-----|
| 1 | `Mokotan/First Floor/1foorLeft/Kitchen.unity` | 115 | 25 | 38 | 29 | 14 | 2 | 7 | 30 | 33 |
| 2 | `Mokotan/Second Floor/ChildRoom.unity` | 79 | 13 | 43 | 19 | 2 | 1 | 1 | 18 | 1 |
| 3 | `Mokotan/First Floor/1floorRight/MaidRoom.unity` | 65 | 15 | 28 | 14 | 6 | 0 | 2 | 14 | 7 |
| 4 | `Mokotan/First Floor/Hall_playerble.unity` | 57 | 19 | 10 | 11 | 6 | 5 | 6 | 18 | 7 |
| 5 | `Mokotan/Second Floor/BedRoom.unity` | 53 | 7 | 29 | 11 | 4 | 1 | 1 | 14 | 2 |
| 6 | `Mokotan/Second Floor/WifeRoom.unity` | 51 | 18 | 12 | 12 | 2 | 2 | 5 | 14 | 3 |
| 7 | `Mokotan/First Floor/1floorRight/StudyRoom.unity` | 49 | 11 | 17 | 8 | 2 | 5 | 6 | 17 | 1 |
| 8 | `Mokotan/First Floor/1foorLeft/UtilityRoom.unity` | 45 | 12 | 17 | 7 | 2 | 0 | 7 | 8 | 2 |
| 9 | `Mokotan/Second Floor/TutorRoom.unity` | 42 | 14 | 6 | 11 | 2 | 1 | 8 | 10 | 3 |
| 10 | `Mokotan/First Floor/1foorLeft/Hallway_Left.unity` | 40 | 12 | 8 | 8 | 6 | 1 | 5 | 14 | 10 |
| 11 | `Mokotan/First Floor/1floorRight/PrisonEntrance.unity` | 36 | 13 | 6 | 3 | 7 | 4 | 3 | 13 | 7 |
| 12 | `Mokotan/First Floor/1foorLeft/Hall_Left.unity` | 36 | 10 | 8 | 7 | 4 | 1 | 6 | 13 | 8 |
| 13 | `Mokotan/Second Floor/BedEntrance.unity` | 32 | 14 | 0 | 5 | 6 | 2 | 5 | 10 | 7 |
| 14 | `Mokotan/Second Floor/ChildEntrance.unity` | 32 | 14 | 0 | 5 | 6 | 2 | 5 | 10 | 7 |
| 15 | `Mokotan/Second Floor/TutorEntrance.unity` | 32 | 14 | 0 | 5 | 6 | 2 | 5 | 10 | 7 |
| 16 | `Mokotan/Second Floor/WifeEntrance.unity` | 32 | 14 | 0 | 5 | 6 | 2 | 5 | 10 | 7 |
| 17 | `Mokotan/Second Floor/2floorHallway_Left.unity` | 30 | 10 | 6 | 7 | 2 | 1 | 4 | 10 | 2 |
| 18 | `Mokotan/Second Floor/2floorLeft.unity` | 30 | 10 | 6 | 7 | 2 | 1 | 4 | 10 | 2 |
| 19 | `Mokotan/First Floor/1floorRight/MaidEntrance.unity` | 27 | 12 | 0 | 4 | 4 | 2 | 5 | 9 | 6 |
| 20 | `Mokotan/First Floor/1floorRight/Prison.unity` | 25 | 9 | 5 | 5 | 2 | 1 | 3 | 8 | 2 |
| 21 | `Mokotan/Second Floor/DressingRoom.unity` | 24 | 12 | 2 | 6 | 2 | 1 | 1 | 10 | 4 |
| 22 | `Mokotan/First Floor/1floorRight/StudyEntrance.unity` | 23 | 9 | 0 | 4 | 4 | 2 | 4 | 7 | 6 |
| 23 | `Mokotan/Second Floor/2floorMainHall.unity` | 23 | 7 | 0 | 5 | 2 | 3 | 6 | 7 | 2 |
| 24 | `Mokotan/First Floor/1floorRight/BookCase1.unity` | 21 | 6 | 3 | 4 | 4 | 2 | 2 | 7 | 1 |
| 25 | `Mokotan/Second Floor/2floorHallway_Right.unity` | 20 | 8 | 0 | 5 | 2 | 1 | 4 | 7 | 3 |

### 고위험 유형별 해석

| 명령 | 이관 우선 대상 C# |
|------|------------------|
| `SetVariable` | `GameState` / `ProgressSnapshot` / ScriptableObject 플래그 |
| `SetActive` | `PanelController`, `SceneObjectRegistry` |
| `If` | 조건은 C#에서 평가 후 Fungus에는 분기된 대사 블록만 호출 |
| `Menu` | `ChoiceDialogController` (선택지 UI) |
| `LoadScene` | `SceneNavigator` / `BackNavigator` (이미 부분 존재) |
| `InvokeMethod` | 명시적 C# 서비스 호출로 대체 |

---

## 5. 추천 이관 순서

### Phase 0 — 공통 인프라 (씬 무관)
1. `GameState`/`InteractionSession` 인터페이스 정의 (Fungus 변수 미러 제거 방향)
2. `WorldClickRouter` 도입 → `FungusClickTrigger`·raw `OnMouseDown` 경로 흡수
3. `ClickInteractionCleanup` → UI 네비게이션 파이프라인에 공식 훅으로 등록

### Phase 1 — 저위험·템플릿 씬 (회귀 비용 낮음)
- `Basement/*` 단일 Start 블록 룸 (Brick/Extraction/Observation)
- `BookCase2Back`, `POAnimation`, `GoPrisonAnimation`, `StudyRoomCutScene`
- `godlotto/BetaEnd`, `SettingScene`, `UITestScene`, `CardTestScene`

### Phase 2 — 입구 씬 패턴 통합 (6씬 일괄 설계 후 개별 적용)
- `MaidEntrance`, `StudyEntrance`, `PrisonEntrance`
- `BedEntrance`, `ChildEntrance`, `TutorEntrance`, `WifeEntrance`
- 공통: Menu(Yes/No) + LoadScene + ItemGate(onUnlock→ExecuteBlock) → `RoomEntranceController` 하나로

### Phase 3 — 복도·교차로 (내비게이션 중심)
- `Hall_*`, `Hallway_*`, `2floor*Cross`, `2floorMainHall`
- LoadScene/InvokeMethod 위주 → `ScenePortal` C# 컴포넌트로 이관

### Phase 4 — 중간 복잡도 룸
- `UtilityRoom`, `DressingRoom`, `BedRoom`, `WifeRoom`, `TutorRoom`, `Prison`
- Say는 Fungus 유지, SetActive/SetVariable만 C#으로

### Phase 5 — 고복잡도 퍼즐 룸 (마지막)
- `Kitchen` (30 blocks, 115 risk, 이중 클릭 경로)
- `StudyRoom` (필터카드·책·다이어리 연동)
- `MaidRoom` (CombinationLock + 다중 상호작용)
- `ChildRoom` (SealManager + 드래그 + ExecuteBlock 6건)
- `Hall_playerble` (Variablemanager + 지하 입구 + 57 risk)

### Phase 6 — 오프닝·메타
- `Opening_Mention` / `Opening_Mention _open` (튜토리얼성, Bell UI→ExecuteBlock)
- `godlotto/MainMenuScene`, `IntroScene`
- `Opening_Office` (Variablemanager)

---

## 6. 절대 한 번에 건드리면 안 되는 씬

아래 씬은 **퍼즐·전역 상태·다중 진입 경로**가 얽혀 있어, 동시 리팩터 시 회귀 범위가 전체 게임 진행에 영향을 줌.

| 씬 | 금지 이유 |
|-----|----------|
| `Kitchen.unity` | 최고 risk(115), FungusClickTrigger 5개, UI+DropZone+Clickable2D 혼재, AI(Parrot) InvokeMethod |
| `Hall_playerble.unity` | Variablemanager 허브, 지하 입구, LoadScene 5, 오프닝 이후 허브 |
| `ChildRoom.unity` | SealManager 연동, ExecuteBlock 6건, SetActive 43 |
| `StudyRoom.unity` | 필터카드/책 패널 C# 연동, ExecuteBlock 3건, LoadScene 5 |
| `MaidRoom.unity` | CombinationLock, 11 clickables, 다중 책/서랍 분기 |
| `MainMenuScene.unity` | 게임 시작 진입점, Flowchart StartButton/ConfigButton |
| `Opening_Mention.unity` | 첫 플레이 시퀀스, Bell→ExecuteBlock |
| `2floorMainHall.unity` | 1층↔2층 허브, LoadScene+InvokeMethod 밀집 |
| `BasementHallway.unity` | 지하 4방향 문 네비 + ObjectClicked 5 |

### 동시 작업 금지 조합
- **Kitchen + UtilityRoom + Hall_Left** (1층 좌측 병/주방 퍼즐 체인)
- **StudyRoom + BookCase1~4 + PrisonEntrance** (서재·지하 서사 체인)
- **ChildRoom + ChildEntrance + SealManager** (봉인 퍼즐 체인)
- **모든 *Entrance.unity 6개 동시** (동일 패턴이지만 Variablemanager/아이템 게이트 공유)

---

## 부록: Fungus에 남길 것 vs C#으로 옮길 것

| Fungus에 유지 | C#으로 이관 |
|-------------|------------|
| `Say`, `Portrait`, `Writer` 대사 출력 | `SetVariable`, bool 플래그 |
| `PlayMusic`, `FadeScreen` 등 연출 (단기) | `SetActive` (패널/오브젝트) |
| 컷신용 일회성 시퀀스 블록 | `LoadScene`, `InvokeMethod` |
| | `Menu` 선택지 → C# Choice UI |
| | `ObjectClicked` / `Clickable2D` 월드 입력 |
| | `InteractionLock` 수명 관리 |

---

*본 리포트는 씬 YAML 정적 분석 기준. Prefab 인스턴스 오버라이드·런타임 `ExecuteBlock`은 Play Mode 검증으로 보완 권장.*