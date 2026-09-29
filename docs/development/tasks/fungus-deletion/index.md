# fungus-deletion

## 현재 실행 기준

- 최종 범위: `BetaEnd`를 제외한 게임 씬·공용 자산의 Fungus 제거. Kitchen은 후보일 뿐.
- 브랜치: `feature/fungus-deletion-framework` (base `develop`)
- 계획: [MASTER-PLAN.md](./MASTER-PLAN.md), [master plan](../../../superpowers/plans/2026-09-17-fungus-deletion-framework-master-plan.md)
- 진행 지표: [PROGRESS.md](./PROGRESS.md). `BetaEnd` 제외 55씬 중 12씬의 Flowchart 제거가 현재 브랜치에 반영되어 있다.
- 이 합침: 이어하기는 `Checkpoint.Latest.v1`. 새 게임은 Fungus `DoSaveReset()`을 부르지 않는다. 제품 씬 Save Point 명령은 제거했다. `Opening_Office` Flowchart 제거는 원격 R1을 유지한다.
- 스크립트 구역: [script-zones.md](../../script-zones.md). 체크포인트는 `Script/Progress/Checkpoint`.
- 현재: [R1 지하 복도 Sequence 적용](R1-basement-hallway-next.md)(Start fade·Flowchart 제거 포함), [R1 지하 연구실 Flowchart 제거](R1-basement-research-room.md), [MainMenu 새 게임 전환](R1-main-menu-start.md), [IntroScene 오프닝 연출](R1-intro-opening-sequence.md)이 현재 브랜치에 포함됐다. `BetaEnd` 제외 범위 기준 Flowchart 완전 제거는 **12/55씬(21.8%)**이다. Hallway live `qa_run`은 Passed; 독립 리뷰 전이므로 `verified`가 아니다. 다음 핵심 경로는 `Opening_Office`의 남은 대사 Flowchart와 공용 Say/Menu 의존성이다.

## 동결

새 Fungus 블록, `Assets/Fungus/` 패치, `*SceneMigrator`, `FungusDialogueQaAdapter` 확장 금지. FlagStore와 Variablemanager 이중 기록 금지.

## 인계

| 필드 | 내용 |
|------|------|
| 합침 | 로컬 세이브·폴더 정리와 원격 R1 진행을 merge |
| independentReview | 세이브·Save Point 제거는 false |
| stash | `preserve-qa-tool-integration-wip-2026-09-17` 는 pop 하지 말 것 |
| 다음 | async Sequence presentation 완료(EditMode). 다음: Opening_Office 대사 이전 (pre-c5fa8b98 Start) |

## 로컬에서 확인한 EditMode (merge 전)

- P1: `FlagStoreTests` 8/8, `SequencePlayerTests` 18/18. independentReview true (그 시점의 계약).
- P3 route: `SceneRouteStateTests` 2/2, `BackNavigatorTests` 10/10. independentReview true.
- P2 저장 거부: `CheckpointRepositoryTests` 7/7, `CheckpointServiceTests` 6/6. independentReview false.
- Save Point 제거: `pytest tools/tests/test_remove_fungus_save_points.py` 4 passed. 제품 씬 42개. 콘솔 `[]`.

## 원격에서 확인한 검증

- P4: `SequenceCatalog` / `SequenceRouter`. NUnit 44 pass. Unity EditMode는 그 checkout에서 미실행.
- `scripts/FungusDeletionSequenceTests` 와 scanner pytest는 원격 TODO 기준.

## 문서

- 세이브 키: [P2-save-key-map.md](P2-save-key-map.md)
- 저장 거부: [P2-checkpoint-save-guard.md](P2-checkpoint-save-guard.md)
- route: [P3-route-state.md](P3-route-state.md)
