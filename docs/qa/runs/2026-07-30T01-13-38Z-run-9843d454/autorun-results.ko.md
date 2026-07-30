# 2026-07-30 QA 오토런 결과 요약 (한글)

**런 ID:** `2026-07-30T01-13-38Z-run-9843d454`  
**계획:** `docs/superpowers/plans/2026-07-30-qa-autorun-execution.md`  
**전체 판정 (1차 런타임):** **fail**  
**종료 기준 관점:** 인프라 블로커로 인해 `DONE_WITH_CONCERNS` / `PLAYABLE PASS` 미도달

---

## 한줄 요약

정적 검사(pytest 55통과)는 통과했으나, `qa_run`이 **씬 로드·Play Mode 진입을 하지 않아** 레거시 시나리오 5건이 실패했고, `room.*` 팩은 스키마 불일치로 **실행 자체가 막혔다**. 유일하게 `tutorroom.cheshire-quiz`만 증거 검토까지 **PASS**였으나, 에디터가 이미 TutorRoom이었던 **환경 의존** Pass다.

---

## Task 1 — 실행 전 기준선

| 항목 | 결과 |
|---|---|
| 브랜치 | `feature/tutorroom-quiz-input-qa-seam` @ `b836b519` |
| Unity | `ready` (6000.0.36f1) |
| Cursor CLI | 사용 가능 (`-p`, `--output-format`, `--workspace`, `--prompt`) |
| 기존 dirty 경로 | 보호(덮어쓰지 않음) |
| 게임플레이 게이트 | OPEN |

---

## Task 2 — 정적 계약

| 검사 | 결과 | 분류 |
|---|---|---|
| `pytest scripts/qa/tests` | **55 passed** | — |
| coverage `ok` | `false` | 그 자체로 게임플레이 실패는 아님 |
| basement 매니페스트 6개 누락 | coverage gap | `NOT_IMPLEMENTED` |
| `kitchen:guard-wrong-input.json` 누락 보고 | 실제 파일명은 `guard-wrong-item.json` | `SPEC_MISMATCH` |
| `qa_list`에 `room.*` id **0개** | room 팩이 DeveloperQa 스키마인데 게이트웨이는 classic 스키마 검증 | `QA_INFRA_DEFECT` |

---

## Task 3~4 — 스모크 / Happy (1차)

### 실행 가능했던 레거시 대체 시나리오

| 시나리오 | 역할 | 시도 | 상태 | 비고 |
|---|---|---:|---|---|
| `hall.kitchen-quest` | hall 스모크 대체 | 2 | **실패** | Hall_playerble이 Play Mode가 아님 |
| `kitchen.faucet-key` | kitchen 스모크/해피 대체 | 2 | **실패** | Kitchen 프리셋에 Play Mode 필요 |
| `maidroom.food-effect` | maid 스모크/해피 대체 | 2 | **실패** | MaidRoom 컨트롤러 없음 |
| `kitchen.cheshire-repeat` | 추가 유효 시나리오 | 2 | **실패** | Kitchen Play Mode 동일 문제 |
| `mainmenu.new-game-reset` | 추가 유효 시나리오 | 2 | **실패** | MainMenuScene이 Play Mode가 아님 |
| `tutorroom.cheshire-quiz` | 추가 유효 시나리오 | 1 | **PASS*** | assertion 3 + 스크린샷 2; 증거 검토 통과 |

\* 환경 의존: 실행 전 에디터 상태가 Edit Mode + `TutorRoom`.

### 레거시 대체가 없는 방

| 방 | 상태 | 이유 |
|---|---|---|
| study / child / wife / bed (smoke + happy) | **차단(BLOCKED)** | `room.*` 팩이 `qa_run`으로 로드 불가 (`QA_INFRA_DEFECT`) |

### 집계

| 판정 | 수 |
|---|---:|
| PASS (인증) | 1 (환경 의존) |
| FAIL | 5 |
| BLOCKED | 8 (study/child/wife/bed smoke+happy) |

---

## 시스템성 결함 (P0)

1. **Play Mode 부트스트랩 부재**  
   `QaScenarioRunner`가 `scenario.scene`을 열거나 Play Mode에 들어가지 않은 채 프리셋/상호작용을 실행한다.  
   실패 시그니처: `missing-playmode-scene:<Scene>/<Controller>`  
   → 분류: **QA_INFRA_DEFECT** (제품 버그로 올리지 않음)

2. **room 팩 스키마/경로 불일치**  
   `Rooms/**` JSON은 `{family, name, targetId}`인데 게이트웨이는 `{command, timeoutMs, scene}`을 기대.  
   잘못된 항목은 JSON `id`가 아니라 파일명(`smoke`)으로 목록화됨.  
   → 분류: **QA_INFRA_DEFECT**

3. **전이(transition) JSON**  
   계약 문서 형태라 `qa_run` 대상이 아님 → 오늘은 전이 회귀 **미실행/차단**.

---

## 증거 검토 (`tutorroom.cheshire-quiz`)

| 기준 | 결과 |
|---|---|
| 재현 경로 | 통과 (`click-quiz-input` → Success) |
| 상태 assertion | 통과 3 / 실패 0 |
| 필수 스크린샷 | 통과 (파일 실재 + 이벤트 참조) |
| 콘솔 신규 예외 | 통과 (`ConsoleErrorCount=0`) |
| API vs RealInput | API만 (RealInput N/A) |
| 최종 검토 판정 | **pass** (환경 의존 명시) |

---

## 다음 단계 (진행 중)

- room 팩 ↔ 게이트웨이 브리지
- `qa_run` 전 Play Mode + 씬 로드 부트스트랩
- coverage 파일명 `guard-wrong-item` 정합
- 수정 후 smoke/happy **재실행** → 일일 최종 `report.md`

증거 루트: `docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/`
