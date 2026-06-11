# 퀘스트 트래커 — 수동 검증 절차

순수 매핑·상태 기계는 EditMode 테스트(`TutorialQuestProgressAdapterTests`, `QuestTrackerStateTests`, `QuestTrackerHudTests`)로 검증합니다.  
아래는 **씬·Fungus·상호작용 연결**을 플레이 모드에서 확인하는 체크리스트입니다.

## 사전 조건

- Unity에서 `disputatio` 프로젝트 열림
- `Resources/TutorialQuestCatalog.asset` 존재 (없으면 메뉴 `Disputatio/Quest/Build Tutorial Quest Catalog`)
- `QuestTrackerHudBootstrap`이 `RuntimeInitializeOnLoadMethod`로 DDOL 시스템·HUD 부착을 처리함 (씬에 수동 배치 불필요)

## 1. 씬 시작 — 첫 퀘스트 표시

1. 메인 메뉴가 아닌 **첫 플레이 씬**(예: `Opening_Office`)에서 새 게임 시작
2. 우측 상단에 **「저택에 불을 밝혀라」** HUD가 슬라이드·페이드 인(약 0.35s)으로 나타나는지 확인
3. 활성 단계는 **「주방으로 이동한다」** 하나만 강조(펄스 점)되는지 확인

## 2. 퀘스트 1 — 단계별 완료

| 단계 | 플레이어 행동 | 기대 HUD |
|------|----------------|----------|
| `go_kitchen` | `Kitchen` 씬 진입 | 첫 줄 완료(✓), 다음 단계 활성 |
| `raise_breaker` | `UtilityRoom`에서 전기 스위치 ON (`ElectricOn` true) | 두 번째 줄 완료 |
| `inspect_hall` | 전기 ON 상태에서 `Hall_playerble` / `Hall_Right` / `Hall_animate` 진입 | 세 번째 줄 완료 → **완료 배너** |

## 3. 퀘스트 교체 (1.5초)

1. 퀘스트 1 마지막 단계 완료 직후 완료 배너·체크 표시 확인
2. **약 1.5초** 후 HUD가 페이드아웃 → **「병 속 열쇠를 꺼내라」** 로 교체·다시 인트로되는지 확인

## 4. 퀘스트 2 — 단계별 완료

| 단계 | 플레이어 행동 | 기대 HUD |
|------|----------------|----------|
| `find_bottle` | 병 습득 (`GetBottle` Fungus bool, `ItemPickup` 등) | 첫 줄 완료 |
| `fill_bottle` | 주방 싱크 `Faucet` 블록 완료 | 두 번째 줄 완료 |
| `take_key` | 병 드래그 `Bottle_Dragged` 블록 완료 | 세 번째 줄 완료 → 완료 배너 (다음 퀘스트 없음) |

## 5. UI 클릭 방해 없음

1. 플레이 씬에서 HUD가 보이는 상태에서 **인벤토리(Tab)·설정·월드 클릭·Fungus 대화 진행**이 정상인지 확인
2. HUD 영역(우측 상단)을 클릭해도 뒤쪽 UI/월드 상호작용이 막히지 않아야 함 (`raycastTarget=false`, `blocksRaycasts=false`)

## 6. 회귀 — 기존 동작 유지

- 주방 싱크/병 퍼즐: 대사·패널·인벤토리 제거가 이전과 동일한지
- 전기 스위치: ON/OFF 스프라이트·Fungus 블록 실행이 이전과 동일한지
- 복도 입장: `CorridorEntranceController` 씬 전환이 이전과 동일한지

## 7. (선택) Fungus 커맨드

Flowchart에 **Quest → Complete Tutorial Quest Step** 커맨드를 넣어 `TutorialQuestIds.*` 상수와 동일한 step id로 수동 완료가 되는지 확인합니다.

## 8. HUD 부착·씬 전환 (Bootstrap)

1. **Canvas 우선**: 플레이 씬에 기존 `Canvas`가 있으면 그 아래에 `QuestTrackerHud`가 붙는지 Hierarchy에서 확인
2. **Canvas 없음**: `QuestTrackerCanvas`(Screen Space Overlay)가 생성되고 그 아래에 HUD가 붙는지 확인
3. **중복 없음**: 같은 씬에 `QuestTrackerHud`가 2개 이상 생기지 않는지 확인 (씬 재진입·방 이동 반복)
4. **씬 전환 후 상태 유지**: `Kitchen` → `UtilityRoom` → 복도 이동 후에도 퀘스트 제목·완료된 단계(✓)가 유지되는지 확인
5. **메인 메뉴**: `MainMenuScene`에서는 HUD가 보이지 않거나 생성되지 않음
6. **메인 메뉴 복귀 후 재플레이**: 다시 플레이 씬 진입 시 HUD가 새 Canvas 아래에 1개만 생성되는지 확인

## 9. 이어하기 / 중간 진입

1. `ElectricOn`만 켜진 체크포인트에서 로드
2. HUD가 `light_the_manor`에서 이미 만족한 단계를 건너뛰고 **현재 맞는 활성 단계**만 표시하는지 확인
3. `GetBottle` 이후 로드 시 `bottle_key` 퀘스트가 바로 잡히는지 확인

## 문제 발생 시

- Console에서 `[KitchenPuzzleState]`, `[CompleteTutorialQuestStep]`, Fungus 블록 이름 확인
- `unity-cli --project disputatio console --type error,warning --lines 80`
- EditMode: `unity-cli --project disputatio test --mode EditMode --filter QuestTrackerHudHostTests`
- EditMode: `unity-cli --project disputatio test --mode EditMode --filter QuestTrackerHudBootstrapTests`
- EditMode: `unity-cli --project disputatio test --mode EditMode --filter TutorialQuestProgressAdapterTests`
