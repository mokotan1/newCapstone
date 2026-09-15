# qa-tool-integration

- Feature: QA 도구 통합 (스펙 `docs/superpowers/specs/2026-09-15-qa-tool-integration-design.md`)
- 한 줄 목표: 홀→주방 수직 실행에서 명령 성공과 게임플레이 성공을 분리하고, 증거·복원·판정을 같은 계약으로 고정한다.
- 위험도: R3 (하네스·저장 격리·씬 전환)
- Cursor 도구: 부모 (분할 조건 미달 — pytest와 Unity/PlayMode를 한 명령으로 검증할 수 없고 Gateway·architecture는 공유 자원)
- independentReview: false (별도 세션 리뷰 전 verified 금지)
- 상태: 구현 착수 / Hall assert-route 슬라이스 (계약 커밋 `61316a26`, coordinator `4f8b9d4f`)

## AC (1단계 필수, 스펙 §7)

관찰 가능한 완료조건은 스펙 AC01–AC23. 이번 세션의 첫 슬라이스는 **계약·반례 판정**만이다.

| 슬라이스 | AC | 검증 |
|---|---|---|
| 계약·반례 | AC01, AC09, AC10, AC17, AC18, AC21 | `python -m pytest scripts/qa/tests/test_tool_plan.py scripts/qa/tests/test_tool_verdicts.py scripts/qa/tests/test_tool_evidence.py scripts/qa/tests/test_tool_report.py scripts/qa/tests/test_tool_normalize.py -q` + 기존 `scripts/qa/tests` 회귀 |
| Gateway·preflight | AC02, AC03, AC04 | pytest + 실제 Editor 미연결/lease fixture |
| 복원/취소 | AC05, AC12–AC15, AC16, AC22 | pytest fault-injection 후 실제 recover |
| 홀 경로 | AC06–AC08, AC11, AC19, AC20, AC23 | Unity PlayMode/플레이 증거. fixture만으로 AC19 완료 금지 |

## 제외

- 게임 퍼즐·저장 정책·AI 제품 요구 변경
- 공식 Unity CLI backend 전환
- QA 실행 중 제품 코드 자동 수정, 커밋·push
- 2·3단계 방 회귀·AI·Player (AC24–AC32)
- 새 글로벌 MonoBehaviour Manager

## 파일 소유 (AC → 심볼 → 파일)

검색 기준: 스펙 G01–G08, `HallQaAdapter`, `QaRunManifest`, `result_contract`, `preflight.missing_required_capabilities`.

| 바구니 | 파일 | AC |
|---|---|---|
| 소유 | `scripts/qa/tool/plan.py` | AC01 |
| 소유 | `scripts/qa/tool/verdict.py` | AC09, AC17 |
| 소유 | `scripts/qa/tool/evidence.py` | AC10 |
| 소유 | `scripts/qa/tool/report.py` | AC18 |
| 소유 | `scripts/qa/tool/normalize.py` | AC21 |
| 소유 | `scripts/qa/tool/preflight.py` | AC02–AC04 |
| 소유 | `scripts/qa/tool/coordinator.py` | AC12–AC15 |
| 소유 | `scripts/qa/tool/hall_route.py` | AC06, AC08 |
| 테스트 쌍 | `scripts/qa/tests/test_tool_*.py` | 위와 동일 |
| 공유·후속 | `scripts/unity_harness/result_contract.py` | AC21 래핑. 기존 0-count 의미는 유지하고 unknown은 도구 adapter에서만 보존 |
| 공유·후속 | `scripts/qa/rooms/preflight.py`, `scripts/qa/autorun/*`, `QaRunManifest.cs`, `docs/architecture.md` | AC02–AC06, AC08, AC12–AC16, AC19, AC22 |
| 소유 | `HallQaAdapter.cs`, `HallQaRouteAssertion.cs` | AC07 |

허용 목록 = 이번 슬라이스 소유 + 테스트 쌍 + 스펙/계획/본 index. 공유 파일은 부모가 후속 슬라이스에서만 연다.

## 부모 구현 증거

- 브랜치: `feature/qa-tool-integration` ← `origin/develop` (`959dc01a`)
- 스펙 조사 revision: `2047d55` (설계 문서에 기록). 구현 기준은 최신 develop
- 검증: `python -m pytest scripts/qa/tests -q` → 93 passed. Unity EditMode `HallQaRouteAssertionTests` 4 passed, `HallQaCapabilityTests` 4 passed (`unity-cli` compile 완료, 신규 console error 없음). 라이브 Kitchen 도착 0회
- 홀 경로: 씬 YAML 기준 hop은 `Hall_playerble` → `Hall_Left` → `Hall_Left2` → `Kitchen`. 직접 Kitchen hop은 spec-mismatch. `HallQaAdapter` assert-route는 `HallQaRouteAssertion`(Kitchen + 전환 종료 + 게이트 해제). 라이브 도착은 NOT_RUN
- 계약 슬라이스: AC01/AC09/AC10/AC17/AC18/AC21 pytest 통과. AC02–AC04는 상태 스냅샷 판정만 (실제 Editor 미연결/lease는 미실행)
- coordinator 슬라이스: AC12–AC15 RecordingGateway 반례 pytest 통과. 실제 Editor 취소·도메인 리로드는 미실행
- independentReview: false

## 다음 단계

1. 실제 Editor 수직 실행 (AC19) — Editor 소유권 인계. AC07 라이브 Kitchen 도착 증거 포함
2. 별도 세션 명세·품질 리뷰 (AC23)
