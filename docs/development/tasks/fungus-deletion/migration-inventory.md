# Fungus 삭제 P0 — 전체 씬/자산 inventory

- 상태: **G0 문서 게이트 충족, 플레이 검증은 미검증**
- 작성: 2026-09-17T07:48:54Z
- 브랜치: `feature/fungus-deletion-framework`
- baseRevision: `e1de9505` (`origin/develop`, Merge PR #336)
- 스캐너: `tools/scan_inventory.py` (읽기 전용, 에셋 수정 없음)
- 원장: `migration-inventory.json`, `migration-inventory.csv`
- 실제 모델: Cursor Grok 4.6 (부모). Composer 2 slug는 추측하지 않음.
- independentReview: false

Kitchen 통과나 Sequence 테스트 개수는 전체 진행률이 아니다. 아래 네 수치만 쓴다.

## 진행률 (P0 종료 시점)

| # | 수치 | 값 |
|---|---|---|
| 1 | 등록 자산 분류 | 씬 129/129 등록. 명령 2911/2911, 블록 585/585 분류. 미분류 0 |
| 2 | 유지 씬 검증 | 제품 씬 56: 전부 `planned`. verified 0. 플레이 미검증이므로 이전 완료 금지 |
| 3 | 씬 전환 edge | Fungus/BlockOutcome 67 + C# 리터럴 일부. BackNavigator 고정표는 코드에서 별도. 검증 완료 0 |
| 4 | 남은 Fungus 참조 | 벤더 GUID 1293개. 보존 자산에서 GUID 참조 파일 241. C# `using Fungus`/심볼 149파일. asmdef 7(전부 벤더) |

폐기 수량(근거 있음, 아직 삭제하지 않음):

- `Assets/FungusExamples/**` 씬 52: `retire-pending` (P7 패키지와 함께 삭제)
- 서드파티 데모 씬 6: 게임 콘텐츠 아님, Fungus 이전 대상 아님(`keep` as vendor-demo)
- 프로젝트 테스트/에디터 씬 15: 사용자 콘텐츠로 단정하지 않음. 기본 `keep`, 패킷에서 근거 확인 후만 retire

## 1. 등록 범위

- `.unity` 전체: **129**
- Build Settings 활성: **56**, 파일 누락 0
- 제품 플레이 씬: **56** (Build Settings와 1:1)
- 제품 Flowchart: **57** (Hall_playerble·Opening_Office는 씬당 2)
- 제품 블록: **397**, 제품 명령: **2063**, 제품 변수 키(고유): **75** (`migration-inventory.json`의 예제 제외 고유 key 재계산 기준)
- Addressables: 프로젝트에 없음. 동적 씬은 `SceneManager.LoadScene` / `LoadSceneSafely` / Fungus `LoadScene` / Checkpoint resume
- 공용 Fungus 시각 프리팹: SayDialog* 5, GlassMenuDialog, Parret. StandingDialogueCanvas는 Fungus 컴포넌트 0

제품 씬 행은 CSV와 아래 표를 같이 본다. 같은 프리팹을 씬 수만큼 완료로 세지 않는다.

| scene | flowcharts | blocks | cmds | input | exits |
|---|---:|---:|---:|---|---|
| `godlotto/BetaEnd.unity` | 1 | 1 | 2 | ui-eventsystem | - |
| `godlotto/IntroScene.unity` | 1 | 1 | 28 | physics2d-collider,ui-button,ui-eventsystem | Opening_Office |
| `godlotto/MainMenuScene.unity` | 1 | 3 | 1 | ui-eventsystem | IntroScene |
| `godlotto/SettingScene.unity` | 0 | 0 | 0 | ui-button,ui-eventsystem | - |
| `Mokotan/Basement/BasementBrickRoom.unity` | 1 | 1 | 1 | - | - |
| `Mokotan/Basement/BasementExtractionRoom.unity` | 1 | 1 | 1 | ui-eventsystem | - |
| `Mokotan/Basement/BasementHallway.unity` | 1 | 6 | 11 | legacy-clickable2d,physics2d-collider | Basement, BasementBrickRoom, BasementExtractionRoom, BasementObservationRoom, BasementResearchRoom |
| `Mokotan/Basement/BasementObservationRoom.unity` | 1 | 1 | 1 | - | - |
| `Mokotan/Basement/BasementResearchRoom.unity` | 1 | 3 | 3 | legacy-clickable2d,physics2d-collider,ui-eventsystem | - |
| `Mokotan/Basement.unity` | 1 | 1 | 1 | - | - |
| `Mokotan/First Floor/1floorRight/BookCase1.unity` | 1 | 7 | 30 | ui-eventsystem | GoPrisonAnimation, PrisonEntrance |
| `Mokotan/First Floor/1floorRight/BookCase2.unity` | 1 | 6 | 27 | ui-eventsystem | StudyRoomCutScene |
| `Mokotan/First Floor/1floorRight/BookCase2Back.unity` | 1 | 2 | 7 | legacy-clickable2d,physics2d-collider,ui-eventsystem | StudyRoom |
| `Mokotan/First Floor/1floorRight/BookCase3.unity` | 1 | 4 | 12 | ui-eventsystem | - |
| `Mokotan/First Floor/1floorRight/BookCase4.unity` | 1 | 4 | 12 | ui-eventsystem | - |
| `Mokotan/First Floor/1floorRight/Hall_Right.unity` | 1 | 6 | 31 | legacy-clickable2d,physics2d-collider,ui-eventsystem | Hall_Right2 |
| `Mokotan/First Floor/1floorRight/Hall_Right2.unity` | 1 | 5 | 24 | legacy-clickable2d,physics2d-collider,ui-eventsystem | Hall_Right2, Hall_RightCross |
| `Mokotan/First Floor/1floorRight/Hall_RightCross.unity` | 1 | 5 | 25 | legacy-clickable2d,physics2d-collider,ui-eventsystem | Hallway_Right, MaidEntrance, StudyEntrance |
| `Mokotan/First Floor/1floorRight/Hallway_Right.unity` | 1 | 6 | 29 | legacy-clickable2d,physics2d-collider,ui-eventsystem | Hallway_Right2 |
| `Mokotan/First Floor/1floorRight/Hallway_Right2.unity` | 1 | 6 | 27 | legacy-clickable2d,physics2d-collider,ui-eventsystem | Hall_playerble |
| `Mokotan/First Floor/1floorRight/MaidEntrance.unity` | 1 | 9 | 47 | legacy-clickable2d,physics2d-collider,ui-eventsystem | MaidRoom |
| `Mokotan/First Floor/1floorRight/MaidRoom.unity` | 1 | 14 | 78 | drag-drop,legacy-clickable2d,physics2d-collider,ui-eventsystem | - |
| `Mokotan/First Floor/1floorRight/Prison.unity` | 1 | 8 | 31 | legacy-clickable2d,physics2d-collider,ui-eventsystem | StudyRoom |
| `Mokotan/First Floor/1floorRight/PrisonEntrance.unity` | 1 | 13 | 56 | legacy-clickable2d,physics2d-collider,ui-eventsystem | Hall_RightCross, Prison, StudyRoom |
| `Mokotan/First Floor/1floorRight/StudyEntrance.unity` | 1 | 7 | 40 | legacy-clickable2d,physics2d-collider,ui-eventsystem | StudyRoom |
| `Mokotan/First Floor/1floorRight/StudyRoom.unity` | 1 | 17 | 67 | drag-drop,legacy-clickable2d,physics2d-collider,ui-eventsystem | BookCase1–4, Hallway_Right |
| `Mokotan/First Floor/1floorRight/StudyRoomCutScene.unity` | 1 | 1 | 4 | ui-eventsystem | BookCase2Back |
| `Mokotan/First Floor/1foorLeft/Hall_Left.unity` | 1 | 13 | 67 | legacy-clickable2d,physics2d-collider,ui-eventsystem | Hall_Left2 |
| `Mokotan/First Floor/1foorLeft/Hall_Left2.unity` | 1 | 4 | 19 | legacy-clickable2d,physics2d-collider,ui-eventsystem | Kitchen |
| `Mokotan/First Floor/1foorLeft/Hallway_Left.unity` | 1 | 14 | 72 | legacy-clickable2d,physics2d-collider,ui-eventsystem | Hallway_Left2 |
| `Mokotan/First Floor/1foorLeft/Hallway_Left2.unity` | 1 | 4 | 18 | legacy-clickable2d,physics2d-collider,ui-eventsystem | Hall_playerble |
| `Mokotan/First Floor/1foorLeft/Kitchen.unity` | 1 | 29 | 245 | legacy-clickable2d,physics2d-collider,ui-eventsystem | Hallway_Left, UtilityRoom |
| `Mokotan/First Floor/1foorLeft/UtilityRoom.unity` | 1 | 8 | 56 | legacy-clickable2d,physics2d-collider,ui-eventsystem | - |
| `Mokotan/First Floor/GoPrisonAnimation.unity` | 1 | 1 | 5 | ui-eventsystem | PrisonEntrance |
| `Mokotan/First Floor/Hall_animate.unity` | 1 | 1 | 7 | ui-eventsystem | Hall_playerble |
| `Mokotan/First Floor/Hall_playerble.unity` | 2 | 18 | 98 | legacy-clickable2d,physics2d-collider,ui-eventsystem | 2floorMainHall, BetaEnd, Hall_Left, Hall_Right, Hall_animate |
| `Mokotan/First Floor/POAnimation.unity` | 1 | 1 | 5 | ui-eventsystem | BookCase2Back |
| `Mokotan/Opening_Mention _open.unity` | 1 | 3 | 18 | legacy-clickable2d,physics2d-collider,ui-eventsystem | Hall_animate |
| `Mokotan/Opening_Mention.unity` | 1 | 3 | 28 | legacy-clickable2d,physics2d-collider,ui-eventsystem | Opening_Mention _open |
| `Mokotan/Opening_Office.unity` | 2 | 1 | 36 | legacy-clickable2d,ui-eventsystem | Opening_Mention |
| `Mokotan/Second Floor/2floorHallway_Left.unity` | 1 | 10 | 41 | legacy-clickable2d,physics2d-collider,ui-eventsystem | 2floorMainHall |
| `Mokotan/Second Floor/2floorHallway_Right.unity` | 1 | 7 | 33 | legacy-clickable2d,physics2d-collider,ui-eventsystem | 2floorMainHall |
| `Mokotan/Second Floor/2floorLeft.unity` | 1 | 10 | 41 | legacy-clickable2d,physics2d-collider,ui-eventsystem | 2floorLeftCross |
| `Mokotan/Second Floor/2floorLeftCross.unity` | 1 | 6 | 29 | legacy-clickable2d,physics2d-collider,ui-eventsystem | 2floorHallway_Left, ChildEntrance, TutorEntrance |
| `Mokotan/Second Floor/2floorMainHall.unity` | 1 | 7 | 35 | legacy-clickable2d,physics2d-collider,ui-eventsystem | 2floorLeft, 2floorRight, Hall_playerble |
| `Mokotan/Second Floor/2floorRight.unity` | 1 | 7 | 33 | legacy-clickable2d,physics2d-collider,ui-eventsystem | 2floorRightCross |
| `Mokotan/Second Floor/2floorRightCross.unity` | 1 | 6 | 29 | legacy-clickable2d,physics2d-collider,ui-eventsystem | 2floorHallway_Right, BedEntrance, WifeEntrance |
| `Mokotan/Second Floor/BedEntrance.unity` | 1 | 10 | 52 | legacy-clickable2d,physics2d-collider,ui-eventsystem | BedRoom |
| `Mokotan/Second Floor/BedRoom.unity` | 1 | 14 | 72 | legacy-clickable2d,physics2d-collider,ui-button,ui-eventsystem | 2floorRightCross |
| `Mokotan/Second Floor/ChildEntrance.unity` | 1 | 10 | 52 | legacy-clickable2d,physics2d-collider,ui-eventsystem | ChildRoom |
| `Mokotan/Second Floor/ChildRoom.unity` | 1 | 18 | 103 | drag-drop,legacy-clickable2d,physics2d-collider,ui-eventsystem | 2floorLeftCross |
| `Mokotan/Second Floor/DressingRoom.unity` | 1 | 10 | 38 | legacy-clickable2d,physics2d-collider,ui-eventsystem | WifeRoom |
| `Mokotan/Second Floor/TutorEntrance.unity` | 1 | 10 | 52 | legacy-clickable2d,physics2d-collider,ui-eventsystem | TutorRoom |
| `Mokotan/Second Floor/TutorRoom.unity` | 1 | 10 | 62 | legacy-clickable2d,physics2d-collider,ui-eventsystem | 2floorLeftCross |
| `Mokotan/Second Floor/WifeEntrance.unity` | 1 | 10 | 52 | legacy-clickable2d,physics2d-collider,ui-eventsystem | WifeRoom |
| `Mokotan/Second Floor/WifeRoom.unity` | 1 | 14 | 69 | legacy-clickable2d,physics2d-collider,ui-eventsystem | 2floorRightCross, DressingRoom |

입력은 씬 YAML에 실제로 나타난 컴포넌트다. 복도/방 다수는 **legacy Clickable2D + Collider2D + EventSystem**이 공존한다. 드래그는 MaidRoom·StudyRoom·ChildRoom에서만 스캔됨. Kitchen 병 드래그는 코드(`Draggable*`)가 담당하므로 씬 입력 모드에 drag-drop이 없을 수 있다 — 플레이 검증 때 코드 경로를 별도 확인.

## 2. 연결 그래프 (시작·재진입·엔딩)

시작:

```
MainMenuScene → (Fungus) IntroScene → Opening_Office → Opening_Mention
  → Opening_Mention _open → Hall_animate → Hall_playerble
```

이어하기: `CheckpointLoadCoordinator.LoadLatestOrFallback("MainScene")`. **`MainScene.unity`는 저장소에 없다.** 체크포인트가 없으면 잘못된 씬 이름을 로드한다. 기존 결함이며 이전 성공의 기대값으로 고정하지 않는다.

허브: `Hall_playerble` → Hall_Left / Hall_Right / 2floorMainHall / BetaEnd / (연출) Hall_animate.

1F 좌: `Hall_Left → Hall_Left2 → Kitchen ⇄ Hallway_Left → Hallway_Left2 → Hall_playerble`. Kitchen → UtilityRoom.

1F 우: `Hall_Right → Hall_Right2 → Hall_RightCross → MaidEntrance/StudyEntrance/Hallway_Right`.

2F: `2floorMainHall → Left/Right 복도 교차 → 방 입구 → 방`. BackNavigator 고정표: Study/Maid→Hallway_Right, Bed/Wife→2floorHallway_Right, Tutor/Child→2floorHallway_Left, 2floorMainHall→Hall_playerble.

엔딩: Hall_playerble → BetaEnd. 설정/엔딩 UI는 C# `LoadScene(MainMenuScene)`.

맵 UI (`WhenClikcedButton`, `ForkHallReturnNav`)와 `OpeningSkipService`도 LoadScene을 직접 친다. 전부 `existing-service` → 최종 소유자는 `SceneTransitionService`.

## 3. 명령/변수 분류 규칙

네 갈래만 쓴다. 인스턴스는 명령 타입으로 상속하고, 혼합 블록은 `csharp-rule` + “분리 대상” 주석.

| 갈래 | 대표 타입 | 소유자 |
|---|---|---|
| csharp-rule | SetVariable, If/ElseIf/Else/End, While, AddItemToInventory, CompleteTutorialQuestStep | 방 상태/`FlagStore`로 통합 예정. 지금은 Variablemanager와 이중 기록 금지 계획만 |
| sequence-presentation | Say, Menu, GlassMenu, TalkStandingCommand, Wait, Fade*, Portrait, SetActive, SFX, LeanTween | Sequence + 기존 Say/Glass/Standing UI |
| existing-service | LoadScene, SavePoint, SetLanguage, InvokeMethod, Call, CallMethod, SendMessage, Clickable2D | SceneTransitionService, Checkpoint, locale, 좁은 C# 메서드 |
| retire-with-evidence | Comment, DebugLog, ExecuteLua, Collection*, Math 예제 명령 | 벤더 예제 또는 미사용. 제품 씬 사용처는 패킷에서 재확인 |

전체 명령 분류 합(벤더 예제 포함): presentation 1534 / csharp 863 / service 436 / retire 78.

제품 변수 키 75개. `FungusVariableKeys`에 있는 것과 전역 `isClicked`/`PrevScene`/`PreviousSceneName`은 기존 서비스. Kitchen 싱크/병/체셔 키는 `KitchenPuzzleState`. 나머지(`seal*`, `HaveWood`, `safe*_ok` 등)는 해당 방 C# 퍼즐이 소유해야 하며 P2에서 세이브 대응표를 만든다.

InvokeMethod (제품, YAML `methodName` 검색): `GoBack`, `PlayFootstep`, Kitchen `TriggerAIResponseByFlag` / 패널 Open·Close. 범용 문자열 버스로 확장하지 않고 좁은 actionId로만 옮긴다.

`ScenarioScript` (`mokotan/.../Scenario/ScenarioScript.cs`)는 Standing 대사 JSON/CSV 경로다. 최종 실행기는 **SequencePlayer 하나**. ScenarioScript는 데이터 공급/폐기 시점을 P1에서 정한다. 이중 실행기 금지.

## 4. 공용 선행 의존성 (P1–P3 우선)

1. 타입 있는 상태 저장소 — `FlagStore`를 Variablemanager와 통합하는 계약 (P1/P2)
2. Sequence 문서 사전 검증·실행 결과 Completed/Cancelled/Failed (P1/P3)
3. `SceneTransitionService` + BackNavigator LoadScene 우회 제거 (P3)
4. `InteractionInputGate` 토큰 (P3)
5. SayDialog / GlassMenu / StandingDialogue 시각 자산, Fungus 타입 제거 (P3)
6. `CheshireLocaleResolver` — SetLanguage와 이중 동기화 금지 (P3)
7. Checkpoint schemaVersion·변환 (P2)
8. QA: FungusDialogueQaAdapter 제거, 공개 Sequence/입력 API (P3/P6)

## 5. P4 대표 집합 (Kitchen은 후보일 뿐)

| 특성 | 대표 | 이유 |
|---|---|---|
| 단순 진입·퇴장·이동 | `Hall_Left2` | 블록 4, Kitchen 한 칸, Clickable2D |
| 퍼즐·아이템·재진입 | `Kitchen` | 블록 29/명령 245, 병·수도·인벤, 재진입 플래그 |
| 대사·선택·AI·locale | `Kitchen` Parret + `TutorRoom` | InvokeMethod AI, TalkStanding, 퀴즈 locale |
| 레거시 physics/드래그 | `StudyRoom` 또는 `ChildRoom` | 씬에 drag-drop 표시. Kitchen은 코드 드래그 |
| 공용 프리팹·전역 상태 | `Hall_playerble` | 허브, DDOL Variablemanager, 다수 출구 |

한 씬이 여러 특성을 만족한다. 이 집합이 통과해도 전체 씬 완료가 아니다.

## 6. 기존 실패 vs 정상 기대값

이전 성공으로 고정하지 **않는** 기존 결함:

| 항목 | 관찰 | 취급 |
|---|---|---|
| `SceneNames.MainScene` | 씬 파일 없음. 새 게임 이어하기 폴백이 깨질 수 있음 | 기존 결함. P2에서 폴백을 실존 씬으로 바꿀지는 사용자 결정 |
| Fungus Fade/Wait/InvokeMethod `Continue()` 누락 | 블록 영구 정지 가능 | **벤더 패치 금지.** Sequence 비동기가 대체 |
| QA Unity listener | 2026-09-17 compile/stop이 health timeout 122s | 라이브 QA blocked. 인프라 패킷 |
| 하이브리드 이중 주인 | C# Interaction + Flowchart 동시 | 구 경로 제거 전까지 정상으로 보지 않음 |

정상으로 유지할 행동(이전 후 비교 기준, 플레이 미검증):

- 메인메뉴 새 게임 → 오프닝 → Hall_playerble
- 좌/우 복도 클릭으로 방 진입, BackNavigator 고정 복귀
- Kitchen 싱크/병/열쇠/체셔 패널 (기존 Kitchen AC)
- 설정 BGM/SFX/해상도는 진행 리셋과 분리
- Cheshire locale ko/ja/en

## 7. QA 격리·timeout·cancel 가능 여부

코드 존재 (모의 테스트로 대체하지 않고 **구현 여부만** 확인):

- 세이브 격리: `scripts/qa/tool/isolation.py` (`qa.` prefix만 변경 허용)
- cancel: `scripts/qa/tool/coordinator.py`
- listener health: `scripts/qa/tool/live.py` / `status_watch.py` (HTTP 비2xx면 console 금지)
- 증거: `scripts/qa/tool/report.py`, `docs/qa/runs/`

실제 실행:

- 이번 세션에서 라이브 hops/PlayMode를 돌리지 않음
- 같은 checkout의 직전 unity-cli compile/stop이 **listener health timeout 122s**
- 따라서 격리·cancel·증거의 **라이브 가능은 blocked**
- 해소 패킷: `P0-infra-unity-cli-listener.md`
- Fungus QA 어댑터 확장으로 리스너를 고치지 않음

## 8. G0 판정

| 조건 | 결과 |
|---|---|
| 미등록 씬 0 | 충족 (129) |
| 미분류 dependency 0 | 충족 (명령/블록 타입 분류). InvokeMethod 타깃은 서비스로 분류, 메서드 목록은 §3 |
| 공용 선행 의존성 식별 | 충족 (§4) |
| 검증 차단과 해소 패킷 | 충족: Unity listener down → 인프라 패킷. 전체 씬 플레이 미검증 → 해당 이전 완료 금지 |
| 전체 verified | **아님** |

## 9. 다음 패킷

1. `P0-infra-unity-cli-listener.md` — Editor health/`unity-cli status` ready. 게임 코드 변경 없음
2. `P1-sequence-contract.md` — FlagStore 타입 충돌 실패, Sequence 문서 전체 검증, 잘못된 문서가 앞쪽 상태를 바꾸지 않음. 씬 일괄 변경 없음
