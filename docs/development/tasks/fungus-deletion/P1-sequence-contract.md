# P1-sequence-contract / FlagStore 타입 정책과 Sequence 사전 검증

- state: verified
- phase: P1 (R1). 공유 영속 상태 연동은 이 패킷에서 하지 않음 (P2)
- prerequisites: G0 문서. EditMode 실행은 `P0-infra-unity-cli-listener` ready 이후
- baseRevision: e1de9505 + S1 Sequence 워킹트리 파일
- actualModel / runner / editorOwner: Codex 부모 / Unity PID 40088
- 행동 AC (FD02, FD03):
  - 동일 키에 bool/int/string을 섞어 등록·쓰면 실패한다. 잘못된 읽기를 false/0으로 숨기지 않는다
  - 공백 키는 실패한다
  - Sequence 문서는 실행 전 전체 검증: schemaVersion, 중복 blockId, 허용 명령, 필수 필드, 참조, 순환, 최대 깊이/명령 수
  - 뒤쪽 명령이 잘못된 문서에서 앞쪽 `FlagStore` 값을 바꾸지 않는다
  - 유효 문서의 기존 S1 `set_*` / `if_bool` 동작은 유지한다
- allowedFiles:
  - 소유: `disputatio/Assets/godlotto/Script/Sequence/**`
  - 테스트: `disputatio/Assets/Editor/Tests/EditMode/Sequence/**`
  - 문서: `docs/development/tasks/fungus-deletion/index.md`, 본 패킷, `docs/architecture.md` Sequence 절만
- excluded: 씬/프리팹, Variablemanager 이중 기록, Kitchen Flowchart 제거, 비동기 handle, 저장, 새 글로벌 Manager, `using Fungus`, 커밋
- oldOwner → newOwner: 없음. 실험 DSL `set_*`/`if_bool`은 유지하되 게임 규칙 DSL로 확대하지 않음
- save/input/scene 영향: 없음
- verification:
  - `.\scripts\unity-cli.cmd --project disputatio editor refresh --compile`
  - `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter FlagStoreTests`
  - `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter SequencePlayerTests`
  - 기대 실행 수: 각 필터 1, passed > 0. 매칭 0은 실패
- rollback: Sequence 폴더와 테스트만 되돌림
- ScenarioScript: 최종 실행기는 SequencePlayer. 이 패킷에서 ScenarioScript를 삭제하지 않고 제거 시점을 index에 `P3`로 적는다

## 실행

- [x] 관련 코드/참조/에셋 조사 및 AC 확정
- [x] 실패하는 테스트 먼저 (TDD)
- [x] AC 범위 내 최소 구현
- [x] compile + 두 EditMode 필터
- [x] independentReview: false (같은 세션)
- [x] 원장·index·인계 갱신

## 증거

- command / exitCode / executed / passed / failed / skipped:
  - `status` → exit 0, Unity ready, PID 40088
  - RED `FlagStoreTests` → exit 1, 8 executed / 5 passed / 3 failed (타입 충돌·공백 키 계약)
  - RED `SequencePlayerTests` → exit 1, 15 executed / 8 passed / 7 failed (사전 검증·상태 비변경 계약)
  - RED `SequencePlayerTests` → exit 1, 16 executed / 15 passed / 1 failed (`bool_value` 문자열 타입 거부 계약)
  - `editor refresh --compile` → exit 0, compilation complete
  - `FlagStoreTests` → exit 0, 8 executed / 8 passed / 0 failed / 0 skipped
  - `SequencePlayerTests` → exit 0, 18 executed / 18 passed / 0 failed / 0 skipped
- runRoot / screenshots / consoleDelta: Play/QA 없음. console은 기존 경고만 반환했고 새 관련 error 없음.
- before/after behavior and state: 동일 키의 타입 혼용과 잘못된 타입 읽기를 거부한다. JSON 필수 필드와 값 타입을 역직렬화 전에 검증하며, `Play`는 schema, 블록, 명령, 참조, 순환, 깊이, 명령 수를 전체 검증한 뒤에만 FlagStore를 바꾼다.
- reviewSession / findings / resolution: 별도 read-only 세션 `p1_review`가 문서 내 키 타입 충돌과 공유 꼬리 깊이 제한 우회 두 건을 발견했다. 두 회귀 테스트를 RED로 확인하고 수정 후 재리뷰에서 Critical/Important 0건 판정. `independentReview: true`.

## 중단 또는 완료 인계

- completedACs: 타입 충돌 실패, 공백 키 실패, 사전 검증, 잘못된 문서의 FlagStore 비변경, 기존 set_*/if_bool 유지
- incompleteACs: P2 이상의 상태 저장·씬 이전은 이 패킷 범위 밖.
- changedFiles / diffIdentity: `FlagStore.cs`, `SequenceDocument.cs`, `SequencePlayer.cs`, `FlagStoreTests.cs`, `SequencePlayerTests.cs` 및 기존 S1 워킹트리 파일. 커밋 없음.
- lastVerifiedCommand / result: 2026-09-21 `FlagStoreTests` 8/8, `SequencePlayerTests` 18/18; compilation complete, console error 0
- currentEditorProject / pid / mode / dirty / lease / activeOperation: `D:/Capstone/newCapstone/disputatio`, PID 40088, ready; QA lease 미확인, Play/QA 미실행
- unfinishedProcess / runId / uncertainMutation: 없음
- blocker: 없음 (P1 패킷 범위)
- lastObservedFailure: 2026-09-21 리뷰가 찾은 문서 내 타입 충돌·공유 꼬리 깊이 우회. 둘 다 회귀 테스트와 재리뷰로 해소.
- nextAction: P2 상태·Checkpoint 계약을 별도 패킷으로 설계
- nextCommand: P2 범위를 확정한 뒤 관련 저장 fixture와 검증 경로를 조사
- doNotRepeat: 씬 일괄 변경, FlagStore와 Variablemanager 동시 쓰기, 벤더 패치
