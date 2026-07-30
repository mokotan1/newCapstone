# 인증 결과 (부분) — 한글

| 시나리오 | 플레이테스터 | 증거 검토 | 인증 가능? |
|---|---|---|---|
| `tutorroom.cheshire-quiz` | 게이트웨이 Pass | **pass** ([검토](evidence-review-tutorroom.md)) | 가능, **단서 있음**: 환경 의존 (에디터가 이미 TutorRoom; Play Mode 부트스트랩 없음) |
| `hall.kitchen-quest` | fail | — | 불가 — `QA_INFRA_DEFECT` (Play Mode 부트스트랩) |
| `kitchen.faucet-key` | fail | — | 불가 — 동일 |
| `maidroom.food-effect` | fail | — | 불가 — 동일 |
| `kitchen.cheshire-repeat` | fail | — | 불가 — 동일 |
| `mainmenu.new-game-reset` | fail | — | 불가 — 동일 |
| study / child / wife / bed의 `room.*` | blocked | — | 불가 — room 팩 스키마 + 부트스트랩 |

## 일일 보고서 정책

- `tutorroom.cheshire-quiz`는 **PASS (환경 의존)** 로만 집계한다. 오토런이 임의 씬을 스스로 부트스트랩한다는 증거가 아니다.
- 실패 5건은 부트스트랩 수정·재실행 전에는 **제품 결함(PRODUCT_DEFECT)** 으로 올리지 않는다.
- 재플레이테스트는 room 브리지 + Play Mode 부트스트랩 인프라 수정 완료 후에만 진행한다.
