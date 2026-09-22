# 스크립트 구역 지도

작성: 2026-09-22. 씬에 붙은 스크립트 GUID는 `.meta`와 함께 옮긴다. 한 번에 한 구역만 옮긴다.

집계는 `disputatio/Assets`의 `.cs`이다. Fungus 패키지, FungusExamples, TextMesh Pro, Plugins, Sprite Shaders Ultimate는 게임 구역이 아니므로 빼었다. Editor 테스트 197개와 Editor 도구는 게임 구역 밖에 두고, 테스트는 대응 구역을 따라간다.

흩어진 게임 스크립트는 구역 폴더로 옮겼다. `Script` 루트에 남은 `.cs`는 `AssemblyInfo.EditorTests.cs`뿐이다. 이미 이름 있는 폴더(`Sequence`, `FungusCommands`, `DialogueLog`, `Quest`, `DropZone`, `Sound`, `KTH`, `AI`, `QA`, `Editor`, `Core`, `Config`, `Input`, `Constants`)는 그 폴더가 구역이다.

## 구역

| 구역 | 하는 일 | 지금 있는 곳 | 파일 수 |
|---|---|---|---|
| 진행/세이브 | 이어하기 JSON, 인벤토리 내용, 획득 플래그, 자물쇠·달력·다이얼·책 페이지 | `Script/Progress` (`Checkpoint`는 그 하위) | 아래 목록 |
| 씬 이동 | 씬 전환, 뒤로가기, 현재 씬 이름 | `Script/SceneFlow` | 6 |
| 방 퍼즐 | 방 클릭, 문, 퍼즐 판정 | `Script/Interaction`, `DropZone`, `Quest` | 방 컨트롤러 + 루트에서 옮긴 퍼즐 |
| 대사 | 시퀀스, 스탠딩 대사, 메뉴, 대사 로그 | `Script/Dialogue`, `Sequence`, `FungusCommands`, `DialogueLog`, `mokotan/.../Scenario`, `StandingDialogue`, `mokotan/.../script/Dialogue` | 기존 폴더 + 루트 3개 + `DialogueData` |
| 설정 | 소리, 화면, 설정 창 | `Script/Setting`, `Sound` | 기존 Setting에 루트 4개를 합침 |
| 체셔 | AI 대화, 로컬 모델, 언어 | `mokotan/.../script/AI` | 30 + Heuristics 9 + Localization 4 + Quizzes 2 |
| 연출 | 타이틀, 점프스케어, 조명, 미니게임, 드래그·화면 | `Script/Title`, `Script/Stage`, `Script/Minigame`, `godlotto/KTH`, `mokotan/.../script/Stage` | 기존 폴더 + 루트에서 옮긴 파일 |
| QA | 플레이 검증. 게임 규칙을 소유하지 않음 | `mokotan/.../script/QA` | 약 66 |
| 개발 도구 | 개발자 모드, 에디터 창 | `script/DevMode`, `Script/Editor`, `script/Editor` | 12 + 28 + 6 |

`Script/Core`(3), `Script/Config`(1), `Script/Input`(4), `Script/Constants`(3), `MapSP`(5), `Graphic or Effect`(5), `Util`(3)은 여러 구역이 같이 쓴다. 진행/세이브 이동에 포함하지 않는다.

## 진행/세이브 — `Script/Progress`로 이동함

체크포인트 8개와 폴더 GUID는 `Script/Progress/Checkpoint`에 있다. `CheckpointRepository`, `CheckpointSaveData`, `CheckpointLoadCoordinator`, `ProgressSnapshotCollector`, `ProgressSnapshotApplier`, `ProgressSnapshotPolicy`, `RoomUnlockCheckpointService`, `RoomCheckpointDefinition`.

`Script/Progress` 루트:

- 저장 키: `PlayDataPrefsCleaner`, `CalendarController`, `UISafeLockController`, `UIDialRotator`, `BookPanelController`
- 아이템 내용: `InventoryManager`, `InventoryAccessState`, `Item`, `ItemLookup`, `ItemRegistry`, `ItemGrantRules`, `ItemAcquisitionTracker`, `ItemAcquisitionBootstrap`
- 이름 상수: `FungusVariableKeys.cs`

한 파일에 구역이 섞인 것. 첫 이동에서 파일을 쪼개지 않는다.

- `InventoryManager` — 아이템 목록은 진행, 슬롯 UI는 인벤토리 화면
- `MainMenu` — 새 게임·이어하기는 진행, 버튼 화면은 타이틀
- `ItemPickup` — 획득 기록은 진행, 클릭은 방 퍼즐

## 씬 이동 — `Script/SceneFlow`

- `SceneTransitionService.cs`
- `SceneRouteState.cs`
- `SceneInteractionController.cs`
- `BackNavigator.cs`
- `SceneTracker.cs`
- `SceneNameSetter.cs` — `SceneName`만 기록한다. `SavePointKey`는 쓰지 않는다.

## 방 퍼즐

`Script/Interaction`에 방 컨트롤러와 아래 파일을 둔다. `ItemPickup`은 쪼개지 않고 여기 있다.

`SealManager`, `CombinationLock`, `ElectricSwitchUiSync`, `PuzzleBookLoader`, `PuzzleBookPageItemGate`, `OpeningMentionController`, `FilterCardBoundedDrag`, `FilterCardRotator`, `BibleSpreadUI`, `BibleSpreadLayoutMarker`, `BibleCommentaryPanelHintButton`, `BookOverlayPagedReader`, `SceneBookOverlayOpener`.

`Script/DropZone`, `Script/Quest`는 그대로 둔다.

## 대사

- `Script/Sequence` 4개. Fungus 없이 플래그와 분기를 실행한다.
- `Script/FungusCommands` 12개. 인벤토리 추가, 퀘스트 스텝, 글래스 메뉴, 효과.
- `Script/DialogueLog` 31개.
- `mokotan/mokotan/script/Scenario` 4개, `StandingDialogue` 5개.
- `Script/Dialogue`: `FungusDialogueBridge`, `FlowchartLocator`, `VariablemanagerSingleton`
- `mokotan/mokotan/script/Dialogue`: `DialogueData`

## 설정

`Script/Setting`에 `ResolutionAudioSettings`, `InGameSettingsPanel`, `SettingPanelWorldInputBlocker`, `AutoAudioListenerFixer`를 합쳤다. `Script/Sound`는 그대로다.

소리·화면 키 4개(`BGMVolume`, `SFXVolume`, `Fullscreen`, `ResolutionIndex`)는 새 게임이 유지한다. 진행/세이브로 옮기지 않는다.

## 체셔

`mokotan/mokotan/script/AI` 전체. `script/Stage`의 `KitchenPostExposureController`, `PostExposureController`, `PotPanelBottleBackgroundSync`는 주방 연출이라 체셔가 아니다.

## 연출과 그 외 루트 스크립트

- 타이틀: `Script/Title`. `MainMenu`, `MainMenuConfigPanel`, `RewardFadePresenter`는 여기 있다. `MainMenu`는 쪼개지 않았다.
- 점프스케어·조명: `godlotto/KTH`
- 미니게임: `Script/Minigame` (`ClickedBubble`, `ControllExit`, `EyeBlinkController`, `MiniGameEnemy`, `MiniGameManager`, `MiniGamePlayer`, `SwingMotion`)
- 드래그, 클릭 정리, 씬 유지, 입력 닫기, 엔딩, 소개 카메라, Fungus bool 그림, 인벤토리 화면: `Script/Stage`

`mokotan/mokotan/script/Stage`: `CompassController`, `GameTimer`, `KitchenPostExposureController`, `PostExposureController`, `PotPanelBottleBackgroundSync`.

## 하지 않는 것

- `.cs`와 `.meta`만 옮겼다. 씬·프리팹 YAML은 그대로다.
- 섞인 파일은 쪼개지 않았다. `InventoryManager`는 `Progress`, `MainMenu`는 `Title`, `ItemPickup`은 `Interaction`.
- Fungus `Assets/Fungus`, QA, Editor, 공용 폴더(`Core`, `Config`, `Input`, `Constants`)는 옮기지 않았다.
- 패키지 삭제와 대사는 이 폴더 정리 다음이다. 제품 씬의 Save Point 명령은 제거했다.
