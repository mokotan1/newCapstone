# QA Autorun Execution Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to execute this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 2026-07-30 안에 현재 구현된 방과 부분 구현된 방의 QA 오토런을 안전하게 실행하고, 재현 가능한 증거와 결함 우선순위가 포함된 일일 판정을 만든다.

**Architecture:** 정적 계약 검사 → Unity 연결/컴파일 게이트 → 방별 smoke → happy/guard → 전이 회귀 순으로 범위를 넓힌다. 기존 작업트리 변경은 보호하며, 자동 수정은 별도 승인과 격리가 없으면 수행하지 않고 결함 보고까지만 진행한다.

**Tech Stack:** Unity 6.0.0.36f1, `scripts/unity-cli.cmd`, Python/pytest, Cursor QA role runner, JSON room scenario packs, Markdown/JSON evidence.

## Global Constraints

- 기존 수정 및 미추적 파일을 덮어쓰거나 정리하지 않는다.
- 한 번에 하나의 `qa-playtester`만 Unity를 조작한다.
- `NOT_IMPLEMENTED`는 실패가 아니라 coverage gap으로 기록한다.
- `PARTIAL`은 지원되는 capability 범위만 실행하며 완전한 gameplay PASS로 승격하지 않는다.
- PASS에는 snapshot, screenshot, Console delta, assertion 결과가 모두 있어야 한다.
- 동일 failure signature는 최대 3회까지만 재시도한다.
- force-solve 또는 직접 상태 변경을 happy-path PASS 근거로 사용하지 않는다.

---

### Task 1: 실행 전 기준선 고정 (10분)

**Files:**
- Read: `scripts/qa/run-cursor-qa.ps1`
- Read: `docs/qa/rooms/first-floor-acceptance.md`
- Read: `docs/qa/rooms/second-floor-acceptance.md`
- Produce: 당일 run 디렉터리의 `orchestration-manifest.json`

**Interfaces:**
- Consumes: 현재 Git 상태, Unity 연결 상태, Cursor CLI, room manifest
- Produces: 실행 가능 여부와 보호해야 할 dirty path 목록

- [ ] **Step 1: 현재 변경을 기록한다**

```powershell
git status --short --branch
git rev-parse HEAD
```

Expected: 현재 브랜치/커밋과 dirty path가 출력된다. 이 목록은 autorun 소유 파일로 간주하지 않는다.

- [ ] **Step 2: Unity와 Cursor CLI를 확인한다**

```powershell
.\scripts\unity-cli.cmd --project disputatio status
cursor-agent --version
cursor-agent --help
```

Expected: Unity `ready`, Cursor CLI가 `-p`, `--output-format`, `--workspace`, `--prompt`를 지원한다.

- [ ] **Step 3: 중단 기준을 적용한다**

Preflight가 실패하거나 Unity가 `ready`가 아니면 gameplay 실행은 `BLOCKED`로 종료한다. 정적 검사는 계속 실행할 수 있다. 기존 dirty path와 겹치는 자동 패치가 필요하면 패치하지 않고 `NEEDS_REVIEW`로 남긴다.

### Task 2: 정적 계약 및 오케스트레이터 회귀 (10분)

**Files:**
- Test: `scripts/qa/tests/`
- Read: `disputatio/Assets/Resources/QA/Scenarios/Rooms/`
- Produce: 당일 run 디렉터리의 `coverage.json`

**Interfaces:**
- Consumes: room catalog, manifests, scenarios, transition contracts
- Produces: runtime 진입 전에 신뢰할 수 있는 scenario 목록

- [ ] **Step 1: QA 오토런 단위 테스트를 실행한다**

```powershell
python -m pytest scripts/qa/tests -q
```

Expected baseline on 2026-07-30: `55 passed`.

- [ ] **Step 2: coverage audit를 실행한다**

```powershell
python -m scripts.qa.rooms.coverage_audit --report-only
```

Expected: first/second-floor 파일 누락은 없고 basement 미구현은 coverage gap으로 보고된다. `ok: false`만으로 gameplay failure를 만들지 않는다.

- [ ] **Step 3: 정적 실패를 분류한다**

Schema/undeclared capability/missing file은 `QA_INFRA_DEFECT`, 미구현 방은 `NOT_IMPLEMENTED`, 기획과 구현 충돌은 `SPEC_MISMATCH`로 기록한다.

### Task 3: Unity 컴파일 및 Smoke 게이트 (20~30분)

**Files:**
- Read: `disputatio/Assets/Resources/QA/Scenarios/*-autorun.json`
- Produce: `docs/qa/runs/<run-id>/<area>/<room>/smoke/`

**Interfaces:**
- Consumes: Task 2에서 검증된 scenario IDs
- Produces: happy/guard 실행 허용 방 목록

- [ ] **Step 1: Unity 콘솔/컴파일 오류가 없는지 확인한다**

```powershell
.\scripts\unity-cli.cmd --project disputatio status
```

Expected: `ready`. 컴파일 오류가 있으면 모든 runtime scenario를 `BLOCKED` 처리한다.

- [ ] **Step 2: active room smoke를 진행 순서대로 실행한다**

우선순위:

1. `room.hall.smoke`
2. `room.kitchen.smoke`
3. `room.maid-room.smoke`
4. `room.study-room.smoke`
5. `room.child-room.smoke`
6. `room.wife-room.smoke`
7. `room.bed-room.smoke`

각 smoke는 scene load, adapter resolution, stable target uniqueness, input gate release, snapshot/screenshot, 신규 Console error 부재를 검증한다.

- [ ] **Step 3: 실패 격리를 적용한다**

한 방의 smoke 실패는 그 방의 happy/guard만 막는다. 독립 preset이 있는 다른 방 smoke는 계속 진행한다.

### Task 4: Happy/Guard 오토런 (45~70분)

**Files:**
- Execute: `disputatio/Assets/Resources/QA/Scenarios/Rooms/**/{happy-path,guard-*.json}`
- Produce: `docs/qa/runs/<run-id>/<area>/<room>/{happy-path,guard-*}/`

**Interfaces:**
- Consumes: smoke PASS 방
- Produces: 방별 `PASS`, `FAIL`, `BLOCKED`, `PARTIAL` 판정

- [ ] **Step 1: 완전 구현 방을 먼저 실행한다**

`kitchen`의 happy path와 wrong-item/reentry guards를 실행한다. bottle → faucet → key → exit contract가 RealInput 근거로 통과해야 `PASS`다.

- [ ] **Step 2: 부분 구현 방을 실행한다**

`hall`, `maid-room`, `study-room`, `child-room`, `wife-room`, `bed-room` 순으로 지원되는 happy/guard를 실행한다. invoke-only 경로는 성공해도 room 상태를 `PARTIAL`로 유지한다.

- [ ] **Step 3: 재시도 예산을 적용한다**

환경성 일시 실패는 1회 재시도한다. 동일 normalized failure signature가 반복되면 최대 3회에서 중단하고 `FAIL` 또는 `BLOCKED`로 확정한다.

- [ ] **Step 4: 자동 수정 경계를 적용한다**

오늘 기본 모드는 진단/증거 수집이다. 제품 또는 QA capability 수정은 별도 격리 브랜치/작업트리와 사용자 승인 없이는 수행하지 않는다.

### Task 5: 전이 및 체크포인트 회귀 (30~45분)

**Files:**
- Execute: first/second-floor transition scenario JSON
- Produce: `docs/qa/runs/<run-id>/<area>/transitions/`

**Interfaces:**
- Consumes: source/destination smoke 결과와 exit contracts
- Produces: progression blocker 목록

- [ ] **Step 1: 1층 전이를 실행한다**

`hall → kitchen`, `kitchen → maid-room`, `maid-room → study-room` 순으로 lock, unlock, reward persistence, checkpoint, return 시 중복 보상 방지를 검증한다.

- [ ] **Step 2: 2층 전이를 실행한다**

`second-floor hall → child-room`, `child-room → wife-room`, `wife-room → bed-room` 순으로 실행한다. stub/partial 전이는 PASS로 승격하지 않는다.

- [ ] **Step 3: 진행 차단 결함을 P0/P1 후보로 표시한다**

문 열림 실패, 키/플래그 유실, checkpoint 복구 실패, 입력 잠금 미해제는 진행 차단 결함으로 분류한다.

### Task 6: 증거 검토와 일일 종료 판정 (20분)

**Files:**
- Produce: `docs/qa/runs/<run-id>/report.md`
- Produce: `docs/qa/runs/<run-id>/manifest.json`

**Interfaces:**
- Consumes: 모든 scenario evidence와 Console delta
- Produces: 오늘 공유 가능한 단일 QA 결과

- [ ] **Step 1: evidence reviewer를 실행한다**

```powershell
.\scripts\qa\run-cursor-qa.ps1 `
  -TaskId "qa-20260730-autorun" `
  -ScenarioIds @(
    "room.hall.smoke",
    "room.kitchen.happy-path",
    "room.maid-room.happy-path",
    "room.study-room.happy-path",
    "room.child-room.happy-path",
    "room.wife-room.happy-path",
    "room.bed-room.happy-path"
  )
```

Expected: `docs/qa/runs/<timestamp>-run-<id>/orchestration-manifest.json`과 역할별 로그가 생성된다.

- [ ] **Step 2: 최종 보고서를 작성한다**

보고서 첫 화면에 다음을 둔다: 실행 수, PASS/FAIL/BLOCKED/NOT_RUN, coverage gaps, 신규 결함, progression blockers, 재시도 횟수, evidence 링크.

- [ ] **Step 3: 오늘의 종료 기준을 적용한다**

`kitchen` full pack과 모든 active smoke가 통과하고 P0 진행 차단이 없으면 `DONE_WITH_CONCERNS`로 종료한다. 모든 구현 방/전이가 완전한 근거로 통과한 경우에만 `PLAYABLE PASS`를 사용한다.

## 권장 시간표 (KST)

| 구간 | 작업 |
|---|---|
| 시작~+20분 | Task 1~2: preflight, pytest, coverage |
| +20~+50분 | Task 3: Unity compile/smoke |
| +50~+120분 | Task 4: kitchen 우선, 나머지 active rooms |
| +120~+165분 | Task 5: transitions/checkpoints |
| +165~+185분 | Task 6: evidence review/report |

## Self-Review

- Spec coverage: static audit, smoke, happy, guard, transition, checkpoint, evidence, retry, verdict를 모두 포함한다.
- Placeholder scan: 실행 명령, scenario 순서, 판정 기준을 명시했다.
- Type/ID consistency: room IDs와 verdict는 현재 manifests 및 approved design의 명칭을 사용한다.
