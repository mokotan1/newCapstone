# QA Scenario Readiness Confirmation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to execute this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 현재 저장소에 준비된 QA 시나리오의 실제 범위를 확정하고, 다음 오토런에서 실행할 항목과 제외할 항목을 사용자 승인 기준으로 고정한다.

**Architecture:** room-pack 시나리오, legacy 단일 시나리오, transition 계약을 서로 다른 계층으로 구분한다. 파일 존재만으로 실행 가능하다고 판단하지 않고 `IMPLEMENTED`, `PARTIAL`, `NOT_IMPLEMENTED`, `MISSING` 상태를 최종 실행 범위에 반영한다.

**Tech Stack:** Unity 6 DeveloperQa, JSON room packs, legacy `qa_run` scenarios, Unity CLI, pytest.

## Global Constraints

- `PARTIAL` 방의 성공을 완전한 게임플레이 PASS로 표현하지 않는다.
- 빈 smoke stub과 transition contract는 실행 가능한 시나리오로 집계하지 않는다.
- 지하 구역은 manifest와 scenario가 모두 준비되기 전까지 전체 오토런 범위에서 제외한다.
- 제품 결함과 `QA_INFRA_DEFECT`를 별도로 집계한다.
- 수정된 씬 bootstrap을 실제 `qa_run`으로 재검증하기 전에는 오토런 안정화를 완료 처리하지 않는다.

---

## 1. 현재 준비 수준 요약

| 분류 | 수량 | 의미 |
|---|---:|---|
| `IMPLEMENTED` full room-pack | 1개 | Kitchen: smoke, happy, guard 2종과 capability 구현 |
| `PARTIAL` full room-pack | 6개 | 파일은 완비됐지만 특정 퍼즐 seam만 검증 |
| `NOT_IMPLEMENTED` smoke stub | 7개 | manifest와 빈 smoke만 존재 |
| `MISSING` | 6개 | 지하 구역: manifest/scenario 없음 |
| Transition contract stub | 6개 | 전이 계약은 있으나 실행 steps 없음 |
| Legacy 단일 시나리오 | 14개 | 기존 `qa_run` 형식의 독립 시나리오 |

## 2. Room-pack 준비 현황

### 2.1 다음 오토런 실행 후보

| 승인 | 방 | 상태 | 준비된 파일 | 검증 가능한 범위 |
|---|---|---|---|---|
| [ ] | `kitchen` | `IMPLEMENTED` | smoke, happy, wrong-item, reentry | 병 채우기, 수도꼭지, 열쇠, exit contract |
| [ ] | `hall` | `PARTIAL` | smoke, happy, wrong-item, reentry | 주방 입구 클릭, 경로 probe/assert |
| [ ] | `maid-room` | `PARTIAL` | smoke, happy, wrong-item, reentry | 음식 쟁반 클릭과 효과 |
| [ ] | `study-room` | `PARTIAL` | smoke, happy, wrong-item, reentry | 거울 책갈피 preset/place/probe |
| [ ] | `child-room` | `PARTIAL` | smoke, happy, wrong-item, reentry | seal5 클릭과 컨트롤러 상태 |
| [ ] | `wife-room` | `PARTIAL` | smoke, happy, wrong-item, reentry | 벽시계 클릭과 컨트롤러 상태 |
| [ ] | `bed-room` | `PARTIAL` | smoke, happy, wrong-item, reentry | 책 클릭과 컨트롤러 상태 |

### 2.2 이번 실행에서 제외할 stub

| 제외 확인 | 방 | 현재 상태 | 부족한 항목 |
|---|---|---|---|
| [ ] | `hall.left` | `NOT_IMPLEMENTED` | 실행 steps, capabilities, happy/guards |
| [ ] | `hall.right` | `NOT_IMPLEMENTED` | 실행 steps, capabilities, happy/guards |
| [ ] | `utility-room` | `NOT_IMPLEMENTED` | 실행 steps, capabilities, happy/guards |
| [ ] | `prison` | `NOT_IMPLEMENTED` | 실행 steps, capabilities, happy/guards |
| [ ] | `study-bookcases` | `NOT_IMPLEMENTED` | 실행 steps, capabilities, happy/guards |
| [ ] | `second-floor.hall` | `NOT_IMPLEMENTED` | 실행 steps, capabilities, happy/guards |
| [ ] | `tutor-room` room-pack | `NOT_IMPLEMENTED` | full room-pack; legacy quiz만 별도 존재 |

### 2.3 아직 만들어지지 않은 지하 범위

| 제외 확인 | 구역 | 현재 상태 |
|---|---|---|
| [ ] | `basement.entry` | manifest/scenario 없음 |
| [ ] | `basement.hall` | manifest/scenario 없음 |
| [ ] | `basement.extraction` | manifest/scenario 없음 |
| [ ] | `basement.observation` | manifest/scenario 없음 |
| [ ] | `basement.brick` | manifest/scenario 없음 |
| [ ] | `basement.research` | manifest/scenario 없음 |

## 3. Legacy 단일 시나리오

### 3.1 대표 실행 후보

| 승인 | ID | Scene | Steps | 현재 판정 |
|---|---|---|---:|---|
| [ ] | `hall.kitchen-quest` | `Hall_playerble` | 2 | bootstrap 수정 후 재검증 필요 |
| [ ] | `kitchen.faucet-key` | `Kitchen` | 4 | bootstrap 수정 후 재검증 필요 |
| [ ] | `maidroom.food-effect` | `MaidRoom` | 2 | bootstrap 수정 후 재검증 필요 |
| [ ] | `mainmenu.new-game-reset` | `MainMenuScene` | 4 | bootstrap 수정 후 재검증 필요 |
| [ ] | `tutorroom.cheshire-quiz` | `TutorRoom` | 5 | 조건부 인증 PASS; 임의 씬 시작 재검증 필요 |

### 3.2 추가 legacy 시나리오

| 승인 | ID | Scene | Steps |
|---|---|---|---:|
| [ ] | `bedroom-book-autorun` | `BedRoom` | 5 |
| [ ] | `childroom-seals-autorun` | `ChildRoom` | 5 |
| [ ] | `hall-nav-autorun` | `Hall_playerble` | 5 |
| [ ] | `kitchen-faucet-autorun` | `Kitchen` | 6 |
| [ ] | `maidroom-food-autorun` | `MaidRoom` | 5 |
| [ ] | `mainmenu-start-autorun` | `MainMenuScene` | 5 |
| [ ] | `studyroom-mirror-diary` | `StudyRoom` | 11 |
| [ ] | `wiferoom-wallclock-autorun` | `WifeRoom` | 5 |
| [ ] | `kitchen.cheshire-repeat` | `Kitchen` | 7 |

## 4. Transition 준비 현황

다음 파일은 계약 데이터만 있으며 실행 단계가 없다. 다음 오토런에서는 coverage 정보로만 표시하고 PASS/FAIL 대상으로 실행하지 않는다.

| 제외 확인 | Transition | 현재 상태 |
|---|---|---|
| [ ] | Hall → Kitchen | `steps: 0` |
| [ ] | Kitchen → MaidRoom | `steps: 0` |
| [ ] | MaidRoom → StudyRoom | `steps: 0` |
| [ ] | 2층 Hall → ChildRoom | `steps: 0` |
| [ ] | ChildRoom → WifeRoom | `steps: 0` |
| [ ] | WifeRoom → BedRoom | `steps: 0` |

## 5. 권장 다음 오토런 범위

### Phase A: Bootstrap 재검증

- [ ] `hall.kitchen-quest`
- [ ] `kitchen.faucet-key`
- [ ] `maidroom.food-effect`
- [ ] `mainmenu.new-game-reset`
- [ ] `tutorroom.cheshire-quiz`

각 실행은 서로 다른 임의 씬 또는 Edit Mode에서 시작해 대상 씬 자동 로드, Play Mode 진입, controller readiness, 실행 후 복구를 검증한다.

### Phase B: Room-pack Smoke

- [ ] `room.hall.smoke`
- [ ] `room.kitchen.smoke`
- [ ] `room.maid-room.smoke`
- [ ] `room.study-room.smoke`
- [ ] `room.child-room.smoke`
- [ ] `room.wife-room.smoke`
- [ ] `room.bed-room.smoke`

### Phase C: 지원 범위 Happy/Guard

- [ ] Kitchen full pack을 완전 PASS 후보로 실행한다.
- [ ] 나머지 6개 `PARTIAL` 방은 지원 capability 범위만 실행한다.
- [ ] stub, transition, basement는 실행하지 않고 coverage gap으로 기록한다.

## 6. 사용자 확인 항목

- [ ] 다음 오토런의 최우선 목표를 “씬 bootstrap 안정성 검증”으로 확정한다.
- [ ] Kitchen만 완전 게임플레이 PASS 후보로 인정한다.
- [ ] Hall/Maid/Study/Child/Wife/Bed 결과는 `PARTIAL PASS` 또는 `PARTIAL FAIL`로 표시한다.
- [ ] 7개 smoke stub은 실행 대상에서 제외한다.
- [ ] 지하 6개 구역은 실행 대상에서 제외한다.
- [ ] transition 6개는 실행 시나리오 구현 전까지 coverage-only로 둔다.
- [ ] 수정 후 오토런 결과가 나오기 전에는 전체 게임 QA 완료를 선언하지 않는다.

## 7. 소스 위치

- Room packs: `disputatio/Assets/Resources/QA/Scenarios/Rooms/`
- Legacy scenarios: `disputatio/Assets/Resources/QA/Scenarios/`
- July legacy scenarios: `disputatio/Assets/Resources/QA/Scenarios/2026-07/`
- Latest QA evidence: `docs/qa/runs/2026-07-30T01-13-38Z-run-9843d454/`
- Scenario coverage tests: `scripts/qa/tests/`

## Self-Review

- 14개 room manifest를 모두 `IMPLEMENTED`, `PARTIAL`, `NOT_IMPLEMENTED`로 분류했다.
- 지하 6개 누락 구역을 별도 표시했다.
- 14개 legacy 시나리오를 모두 기록했다.
- transition 6개가 실행 가능한 시나리오가 아닌 계약 stub임을 명시했다.
- 현재 결과와 수정 후 재검증 대상을 분리했다.
