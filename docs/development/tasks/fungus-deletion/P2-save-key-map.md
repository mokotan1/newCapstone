# P2-save-key-map / 세이브·로드 키 대응표

- state: documented
- phase: P2 조사 (R0). 스키마 변환·저장 코드 변경은 이 패킷에서 하지 않음
- prerequisites: G0 inventory, P2-checkpoint-save-guard (저장 거부는 이미 EditMode)
- baseRevision: `7c2557a3` + 워킹트리의 save-guard
- 행동 AC: 플레이어 진행이 어디에 저장되는지 키·파일·신호 단위로 적고, 새 게임에서 지워지는지와 이어하기가 무엇을 읽는지 구분한다
- allowedFiles: 본 문서, `index.md`, `docs/architecture.md` §8 한 행, `TODO.md`
- excluded: 씬·프리팹, `CheckpointRepository` 추가 변경, Fungus `SaveManager` 삭제, 키 문자열 변경
- verification: `migration-inventory.csv`에서 Build Settings 활성 56씬의 `variableKeys` 재집계. Unity 테스트·플레이 없음

## 플레이어가 보는 세이브/로드

| 동작 | 지금 하는 일 |
|---|---|
| 이어하기 | `MainMenu.OnLoadButton` → `CheckpointLoadCoordinator.LoadLatestOrFallback("MainScene")`. Fungus Save Menu를 타지 않음 |
| 새 게임 | `PlayDataPrefsCleaner`가 PlayerPrefs를 비운 뒤 오디오·화면 4키만 되돌림. 인벤토리는 `InventoryManager.ClearItemsForNewGame()`. `DoSaveReset()`은 호출하지 않음 |
| 방 해금 저장 | `RoomUnlockCheckpointService`가 Fungus 변수를 읽어 `Checkpoint.Latest.v1` JSON에 넣음 |
| Fungus 파일 세이브 | `persistentDataPath/FungusSaves/{saveDataKey}.json`. 메뉴 기본 키는 `save_data`. 제품 씬의 Save Point 명령은 없다. Fungus 예제 씬만 남아 있다 |

`MainScene.unity`는 없다. 이어하기 fallback 씬 이름은 이 표에서 바꾸지 않는다.

이어하기는 C# JSON이다. `InventoryManager`는 Fungus 세이브 로드로 아이템을 다시 채우지 않는다. 새 게임 리셋 신호 `OnSaveReset`만 구독한다. 제품 씬에는 Save Point 명령이 없다.

## 1. 설정 — 진행 세이브가 아님

| 키 | 타입 | 새 게임 |
|---|---|---|
| `BGMVolume`, `SFXVolume`, `Fullscreen`, `ResolutionIndex` | float/int | 유지. `PlayDataPrefsCleaner`가 되돌리는 전부 |
| `CheshireAnswerTextScale` | int | 지워짐. 설정 화면 값이지만 cleaner 스냅샷 밖 |
| `LocalAi.ChatDisabled` | int | 지워짐. 같은 이유 |

언어(`ko`/`ja`/`en`)는 PlayerPrefs 키가 없다. `CheshireLocaleResolver`가 Fungus `SetLanguage` 런타임 값을 읽는다. 세이브 필드가 아니다.

## 2. 이어하기 JSON — `Checkpoint.Latest.v1`

`Checkpoint.LatestId.v1`는 마지막 `checkpointId` 사본이다.

JSON 필드: `version`, `checkpointId`, `checkpointType`, `unlockedRoomKey`, `resumeSceneName`, `resumeSpawnId`, `createdAtUtc`, `itemIds`, `fungusBooleans`, `fungusIntegers`, `fungusStrings`.

저장 시 `Variablemanager` Flowchart의 bool/int/string을 복사한다. 빼는 키는 `ProgressSnapshotPolicy` 기준 `isClicked`, `WindowClicked`, `BGMVolume`, `SFXVolume`, `Fullscreen`, `ResolutionIndex`, 접두 `Dial_`, `SnapState_`.

코드가 이름까지 고정해 복사하는 키:

| 타입 | 키 | 의미 |
|---|---|---|
| bool | `ElectricOn` | 주방 해금. checkpointId `unlock_kitchen` |
| bool | `UsedStudyKey`, `UsedMaidKey`, `UsedTutorKey`, `UsedChildKey`, `UsedWifeKey`, `UsedBedKey` | 방 해금. `RoomCheckpointDefinition` |
| int | `CorrectAnswerCount` | 튜터 정답 수 |
| int | `AcquiredItemsMask` | 아이템 영구 획득 비트. 인벤토리에서 빼도 유지 |
| string | `InventoryItemIds` | 인벤토리 id 문자열. 씬 변수 scan에는 없고 코드 상수 |

`itemIds`는 `InventoryManager` 슬롯 id 배열이다. Fungus 키와 별도 필드다.

## 3. 빌드 씬 Fungus 변수 76개

`migration-inventory.csv`에서 `buildEnabled=True` 56행을 다시 세었다. P0 문서의 75와 1개 차이 난다. 아래가 이번 재집계다.

체크포인트가 Flowchart를 찾으면 2절의 제외 키를 뺀 나머지를 JSON에 넣는다. 방에만 있는 변수는 그 Flowchart가 저장 시점에 있어야 복사된다.

**세션. 체크포인트에 넣지 않음:** `isClicked`, `WindowClicked`.

**뒤로가기. 런타임 읽기는 이미 `SceneRouteState`:** `PrevScene`, `PreviousSceneName`. 씬 YAML에는 남아 있다. 세이브 필드가 아니다.

**방 해금·열쇠:** `ElectricOn`, `UsedStudyKey`, `UsedMaidKey`, `UsedBedKey`, `UsedWifeKey`, `UsedTutorKey`, `UsedChildKey`, `UsedBasementKey`, `UsedPrisonKey`, `HaveStudyKey`, `HaveMaidKey`, `HaveBedKey`, `HaveWifeKey`, `HaveTutorKey`, `HaveChildKey`, `HaveBasementKey`, `HavePrisonKey`, `HaveHolyGrail`, `HaveWood`. `UsedBasementKey`와 `UsedPrisonKey`는 `RoomCheckpointDefinition`에 없다.

**아이템·음식:** `AcquiredItemsMask`, `GetBottle`, `GetFood`, `GetBookmarkMirror`, `GetItem1`, `GetItem2`, `GetItem3`, `getFood`, `giveFood`, `friFood`, `FoodCooked`, `FoodDragged`. 코드의 `GetFilterCard`, `GetBibleCommentary`, `HasBible`은 이 76개에 없다. 마스크 비트로만 남을 수 있다.

**주방·체셔:** `BottleClicked`, `BottleDragged`, `FaucetClicked`, `ComeParret`. 퀘스트 완료는 별도 저장이 없고 `ElectricOn`, `GetBottle`을 읽는다.

**퍼즐:** `seal1`–`seal7`, `Haveseal5`–`Haveseal7`, `allSealsComplete`, `safeL_ok`, `safeM_ok`, `safeR_ok`, `safe_all_ok`, `DrawerOpen`, `DownDrawerOpen`, `UpDrawerOpen`, `isDrawerOpened`, `DiarySolved`, `solved`, `is2floorsolved`, `PrisonOpen`, `CardOn`, `call_burner`, `hasPannel`.

**그 외 진행 플래그:** `CorrectAnswerCount`, `PlayerAnswer`, `Animation`, `POAnimation`, `Asked`, `Saveunlock`, `pressTab`, `MapClicked`, `ButtonClicked`, `isCall`, `isTalking`. `pressTab`은 인벤토리 열림 미러다.

## 4. 체크포인트 JSON 밖의 PlayerPrefs 진행

새 게임 `DeleteAll`로 지워진다. 이어하기 JSON에는 들어가지 않는다.

| 키 | 쓰는 곳 |
|---|---|
| `SafeLock_Unlocked` | `UISafeLockController` |
| `InventoryAccess.UnlockedAfterHallPlayableRetry` | 인벤토리 해금 |
| `InventoryGuide.InventoryOpened` | 인벤토리 가이드 1회 |
| `LastCalendarMonth` | 달력 |
| `No40_MansionHubVisited`, `No40_FirstEntryPlayed`, `No40_FirstDeathLinePlayed`, `No40_BloodPathLinePlayed` | 1회 독백. QA 진행 키 목록에는 없음 |
| `Dial_{오브젝트이름}_Value` | 다이얼 숫자. 이름이 씬 오브젝트에 붙음 |
| `SnapState_*` | 드래그 스냅. 정책상 Fungus 스냅샷에서 제외 |
| `LastBookPage_{오브젝트이름}` | 책 페이지 |
| `PlayLogRecorder.SessionId` | 플레이 로그. 진행 세이브가 아님 |

## 5. 이번 표에서 옮기지 않는 것

| 키 | 이유 |
|---|---|
| `LocalAi.SessionId`, `LocalAi.ParentStart`, `LocalAi.ManualStop`, `LocalAi.AutoStart` | 로컬 AI 프로세스 |
| `ChatHttpClient.AnonymousUserId` | 채팅 익명 id |
| 개발 GUI 폰트 PlayerPrefs | 개발 도구 |

Fungus 예제 씬 변수(`MyBool`, `Score` 등)는 제품 76개 밖이다.

## 다음 구현에 넘기는 것

- 이어하기가 읽는 값은 `Checkpoint.Latest.v1` 하나다. 제품 씬의 Save Point 명령은 제거됐다. `FungusSaves` 파일 경로는 새 게임이 에디터에서 지울 수 있지만, 이어하기는 그 파일을 읽지 않는다.
- JSON 밖 진행 키(자물쇠, 인벤토리 해금, 달력, No40, 다이얼, 스냅, 책)를 그 JSON으로 합칠지 유지할지는 이 문서가 결정하지 않는다. 합치기 전에 기존 값 변환이 필요하다.
- `CheshireAnswerTextScale`과 `LocalAi.ChatDisabled`가 새 게임에서 지워지는 것은 설정 보존 구멍이다. 키 이름을 바꾸지 않는다.
- 손상된 JSON과 세이브 없음의 구분은 아직 `TryLoad`가 둘 다 false다.

## 증거

- 2026-09-22 `migration-inventory.csv` 재집계: build 56행, 변수 키 76개.
- 코드: `MainMenu.OnLoadButton`, `MainMenu.OnStartButton`, `CheckpointRepository`, `ProgressSnapshotCollector`, `ProgressSnapshotPolicy`, `RoomCheckpointDefinition`, `QaProfileService.KnownGameplayKeys`, `SceneNameSetter`, `ItemAcquisitionTracker`, `No40ConditionalDialogueRunner.PrefsKeys`.
- 플레이·EditMode: 이 패킷에서 실행하지 않음.
