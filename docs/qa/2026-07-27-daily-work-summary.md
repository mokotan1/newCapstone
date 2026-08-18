# 2026-07-27 작업 정리 — Self-Extending Multi-Room QA Autorun

**날짜:** 2026-07-27  
**브랜치:** `feature/self-extending-qa-autorun`  
**Worktree:** `.worktrees/qa-autorun-dev-mode`  
**PR:** [#318](https://github.com/mokotan1/newCapstone/pull/318) → `develop` (**MERGED** `c0e38334`, 2026-07-27)  
**목표:** Developer Mode QA를 StudyRoom 단일 슬라이스에서 **방별 매니페스트·능력(capability)·오토런**으로 확장하고, Kitchen을 첫 **PlayMode IMPLEMENTED** 방으로 완성한다.

---

## 1. 한줄 요약

`DeveloperQaService` + capability registry + 방별 room pack을 깔고, Kitchen은 bottle → faucet(Say 펌프) → maid key → exit까지 PlayMode 오토런을 통과시켰다. 그 외 1·2층 어댑터 방은 **PARTIAL**(EditMode·팩 검증), 스텁·지하실은 **NOT_IMPLEMENTED**. 맨션 전체 PlayMode 연쇄는 아직 아님.

---

## 2. 당일 진행 흐름

| 단계 | 내용 |
|------|------|
| A. 설계·플랜 | Self-extending QA autorun, multi-room thin wrap, room-by-room scenario 설계/플랜 |
| B. Core | `IDeveloperQaService`, registry, MissingCapability, session isolation, CLI 브리지 |
| C. Wave 1 | Kitchen faucet + MainMenu start capabilities, factory 등록, autorun 시나리오 |
| D. Wave 2 | MaidRoom food, Hall nav thin wraps |
| E. Wave 3 | ChildRoom seals, WifeRoom wallclock, BedRoom book |
| F. Room packs | 1층·2층 manifest/smoke/happy/guard, catalog·coverage audit·preflight |
| G. Kitchen exit | bottle-fill / key / exit.assert + RealInput(`interaction.pointer`) |
| H. PlayMode 블로커 해결 | Fungus Say 데드락 → async dispatch + Say pump + QA 즉시 키 스폰 |
| I. 검증·PR | 타 방 EditMode·pytest 재검증 후 PR #318 생성·머지 |

---

## 3. Kitchen PlayMode — 핵심 이슈와 해결

### 원인

Kitchen Fungus 블록 `Faucet`에 `Say(waitForClick: 1)`(+ 짧은 `Wait`)가 있다.  
`FaucetClicked`는 블록 종료 시 `OnBlockEnd` → `KitchenPuzzleState.ApplyBlockCompletion("Faucet")`로만 true가 된다.  
동기 QA/`GetResult()` + `Task.Yield` 조합이 Unity 메인 스레드를 막아 **unity-cli health timeout**이 났다.

### 해결 (CLI sync-safe)

1. Faucet 클릭 후 `DeveloperQaFungusSayPump`로 Say 진행 → stuck Faucet 블록 정지 → `ApplyBlockCompletion("Faucet")` (플레이어 경로와 동일; `HaveMaidKey` 위장 없음).
2. `FaucetKeyReleaseController.TriggerImmediateKeySpawnForQa()`로 1초 Update 지연 우회; inactive 부모 활성화로 `MaidRoomKey`가 hierarchy에 보이게.
3. `KitchenSinkInteractionGate.PlayerHasBottle`이 인벤토리 Bottle도 인정.
4. 격리 Kitchen: Play Mode에서 Inventory/Variablemanager 부트스트랩.
5. Capability ID: `kitchen.sink.fill-bottle` (오타 `fills-bottle` 아님).
6. Async capability handler (`DeveloperQaAsyncCapabilityHandler` / `RegisterAsync`)로 긴 대기 없이 디스패치.

### PlayMode 증거 (통과)

`before-bottle-fill` → `fill-bottle` → `faucet.click` → `key.click` → `exit.assert` 전부 **Ok**  
(`haveMaidKey=True`, inventory에 maid-room-key).

Kitchen을 `IMPLEMENTED`로 기록:

- `disputatio/Assets/Resources/QA/Scenarios/Rooms/first-floor/kitchen/manifest.json`
- `docs/qa/rooms/first-floor-acceptance.md`
- `scripts/qa/tests/test_first_floor_kitchen_pack.py`

---

## 4. 방별 상태 (acceptance 기준)

### 1층 (`docs/qa/rooms/first-floor-acceptance.md`)

| roomId | status | 비고 |
|--------|--------|------|
| kitchen | **IMPLEMENTED** | PlayMode exit 계약 통과 |
| hall | PARTIAL | `hall.nav.*` |
| maid-room | PARTIAL | `maidroom.food.*` |
| study-room | PARTIAL | `studyroom.mirror.*` |
| hall.left / hall.right / utility-room / study-bookcases / prison | NOT_IMPLEMENTED | smoke stub만 |

### 2층 (`docs/qa/rooms/second-floor-acceptance.md`)

| roomId | status | 비고 |
|--------|--------|------|
| child-room | PARTIAL | `childroom.seals.*`, invoke-only |
| wife-room | PARTIAL | `wiferoom.wallclock.*`, invoke-only |
| bed-room | PARTIAL | `bedroom.book.*`, invoke-only |
| second-floor.hall / tutor-room | NOT_IMPLEMENTED | smoke stub |

### 커버리지 감사

`python -m scripts.qa.rooms.coverage_audit` → `ok: false`  
남은 갭 예: basement.* 매니페스트 부재, `kitchen:guard-wrong-input.json` 네이밍 옵션 갭.  
area 연쇄(`scripts/qa/rooms/orchestrate_area.py`)는 **phase stub** (연쇄 traversal 미구현).

---

## 5. 검증 결과 (PR 직전 재실행)

| 검증 | 결과 |
|------|------|
| `python -m pytest scripts/qa/tests -q` | **55 passed** |
| EditMode Hall / Maid / Wife / Child / Bed / MainMenu / Kitchen(+Exit) | **0 failed** |
| EditMode `MultiRoomAutorunScenarioTests` | **7/7** |
| EditMode `DeveloperQaServiceFactoryMultiRoomTests` | **3/3** |
| EditMode `StudyRoomQaAdapterTests` | **13/13** |
| Kitchen PlayMode exit | 세션 내 Ok (위 §3) |

**주장하지 않는 것:** 전 방 PlayMode happy-path, basement pack, 맨션 전체 연쇄 오토런.

---

## 6. 주요 산출물 / 파일

### 인프라·런타임

- `disputatio/Assets/mokotan/mokotan/script/QA/Developer/*` — Service, Registry, Async handler, ScenarioRunner, Factory
- `DeveloperQaFungusSayPump.cs` (+ auto-advance host)
- Kitchen: `KitchenQaAdapter.cs`, `FaucetKeyReleaseController.cs`, `KitchenSinkInteractionGate.cs`

### Room packs

- `disputatio/Assets/Resources/QA/Scenarios/Rooms/first-floor/**`
- `disputatio/Assets/Resources/QA/Scenarios/Rooms/second-floor/**`

### 스크립트

- `scripts/qa/rooms/` — catalog, schema, coverage_audit, progression, preflight, orchestrate_area stub
- `scripts/qa/autorun/` — orchestrator skeleton, classify, checkpoint, git_isolation
- `scripts/qa/tests/` — pack·schema·orchestrator 단위 테스트

### 문서

- `docs/superpowers/specs/2026-07-27-*-design.md` (self-extending / multi-room / room-by-room)
- `docs/qa/rooms/first-floor-acceptance.md`, `second-floor-acceptance.md`, `coverage-baseline.md`
- `docs/qa/wave-{1,2,3}-completion.md`

### 대표 커밋 (당일 tip)

- `d789b14a` — kitchen PlayMode exit + async capability dispatch
- 그 외 Wave·room-pack·catalog·coverage·capability 커밋 다수 (브랜치 `origin/develop..feature/...` 전량)

---

## 7. 의도적으로 제외한 로컬 잡음

커밋/PR에 넣지 않음:

- FungusEditorResources, DOTweenSettings, EditorBuildSettings, ShaderGraphSettings
- KTH png.meta, `scripts/CSharpSyntaxChecker/bin/*`
- 일부 로컬 implementer report 미정리분

---

## 8. Follow-up (다음 작업 후보)

1. PARTIAL 방(hall/maid/study/child/wife/bed) **PlayMode smoke**로 IMPLEMENTED 승격 검토  
2. Basement region 매니페스트·시나리오 팩  
3. `orchestrate_area` 실제 방 연쇄 traversal  
4. Coverage audit `ok: true`까지 갭 해소 (`guard-wrong-input` 네이밍 포함)  
5. unity-cli exec에서 `GetResult` + 긴 `Task.Yield` 메인스레드 대기 **재도입 금지**

---

## 9. 관련 링크

- PR: https://github.com/mokotan1/newCapstone/pull/318  
- 설계: `docs/superpowers/specs/2026-07-27-self-extending-qa-autorun-developer-mode-design.md`  
- Room-by-room: `docs/superpowers/specs/2026-07-27-room-by-room-qa-autorun-scenarios-design.md`  
- Multi-room: `docs/superpowers/specs/2026-07-27-multi-room-qa-autorun-design.md`
