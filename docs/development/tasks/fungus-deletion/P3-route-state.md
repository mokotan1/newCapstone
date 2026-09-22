# P3-route-state / C# 뒤로가기 route 상태 (PrevScene 제거)

- state: verified
- phase: P3 첫 실제 교체 (R3). 대사·선택·비동기 UI는 이 패킷 범위 밖
- prerequisites: G0 문서, P1 Sequence 계약, Unity CLI ready (`P0-infra-unity-cli-listener`)
- baseRevision: `7c2557a3` (`feat: establish Fungus-free sequence foundations`)
- actualModel / runner / editorOwner: Cursor implementer / Unity PID는 실행 시 `status`로 재확인
- 행동 AC:
  - `SceneTracker`가 씬 unload 시 이전 씬 이름을 `SceneRouteState`에 기록한다. 공백 이름은 기존 값을 덮어쓰지 않는다
  - `BackNavigator.GoBack`이 고정 복귀 경로·모달 입력 차단·fallback을 유지하면서, 동적 복귀는 Fungus `PrevScene`이 아니라 `SceneRouteState`만 읽는다
  - route 상태는 세션 전용이며 체크포인트/영속 저장에 쓰이지 않는다
  - `SceneName`·`SavePointKey` 등 Fungus SavePoint가 아직 읽는 값은 이 패킷에서 바꾸지 않는다
- allowedFiles (이미 커밋됨):
  - `disputatio/Assets/godlotto/Script/SceneFlow/SceneRouteState.cs`
  - `disputatio/Assets/godlotto/Script/SceneFlow/SceneTracker.cs`
  - `disputatio/Assets/godlotto/Script/SceneFlow/BackNavigator.cs`
  - `disputatio/Assets/Editor/Tests/EditMode/Interaction/SceneRouteStateTests.cs`
  - `disputatio/Assets/Editor/Tests/EditMode/UI/BackNavigatorTests.cs` (기존 고정 복귀 테스트 유지)
  - 문서: 본 패킷, `index.md`
- excluded: 씬·프리팹 YAML에서 `PrevScene` 직렬화 필드 제거, Flowchart 블록 삭제, Checkpoint/Variablemanager 이중 기록, 커밋
- oldOwner → newOwner: Fungus `PrevScene` 변수(런타임 읽기) → `SceneRouteState` (세션 static)
- save/input/scene 영향: 저장 없음. 씬 에셋 미변경. Play/QA 미실행
- verification:
  - `.\scripts\unity-cli.cmd --project disputatio status`
  - `.\scripts\unity-cli.cmd --project disputatio editor refresh --compile`
  - `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter SceneRouteStateTests`
  - `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter BackNavigatorTests`
  - 회귀: `FlagStoreTests`, `SequencePlayerTests` (P1)
- rollback: 위 소스·테스트만 `7c2557a3` 이전으로 되돌림 (씬 미포함)

## 실행

- [x] 관련 코드/참조/에셋 조사 및 AC 확정
- [x] 실패하는 테스트 먼저 (TDD)
- [x] AC 범위 내 최소 구현
- [x] compile + EditMode 필터
- [x] independentReview: true (별도 read-only 세션, Critical/Important 0건 — index 기록)
- [x] index·인계 갱신

## 증거

- command / exitCode / executed / passed / failed / skipped:
  - 2026-09-22 `status` → exit 0, Unity ready, PID 48440
  - `editor refresh --compile` → exit 0, compilation complete
  - `console --type error,warning --lines 40` → `[]`
  - `SceneRouteStateTests` → 2/2 passed
  - `BackNavigatorTests` → 10/10 passed
  - `FlagStoreTests` → 8/8 passed (회귀)
  - `SequencePlayerTests` → 18/18 passed (회귀)
- runRoot / screenshots / consoleDelta: Play/QA 없음
- before/after behavior and state: 런타임 뒤로가기 동적 경로는 C# route 스택만 사용. 씬·프리팹에 남은 `PrevScene` 직렬화는 정리 패킷에서 제거 예정
- reviewSession / findings / resolution: index §P3 — 타입/깊이 이슈 없음, route 교체 ready

## 중단 또는 완료 인계

- completedACs: Fungus PrevScene 런타임 읽기 제거, SceneRouteState 기록·복귀, 고정 복귀·모달·fallback 유지
- incompleteACs: 씬/프리팹 PrevScene 필드 정리, 대사·선택 P3 다음 슬라이스, P2 영속 상태
- changedFiles / diffIdentity: `7c2557a3`에 포함. 워킹트리 게임 코드 추가 변경 없음
- lastVerifiedCommand / result: 2026-09-22 EditMode 38/38 (route 12 + P1 26), compile OK
- currentEditorProject / pid / mode: `D:/Capstone/newCapstone/disputatio`, PID 48440, ready
- blocker: 없음 (이 패킷)
- nextAction: P2 Checkpoint·영속 상태 계약 패킷 설계, 또는 P3 대사/선택 최소 슬라이스 범위 확정
- nextCommand: P2 범위 확정 후 `migration-inventory`의 저장·Fungus 변수 대응 조사
- doNotRepeat: 씬 일괄 PrevScene 삭제를 route 패킷에 섞기, FlagStore와 Variablemanager 동시 쓰기
