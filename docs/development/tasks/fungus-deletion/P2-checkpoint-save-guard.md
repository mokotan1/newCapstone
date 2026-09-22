# P2-checkpoint-save-guard / 저장 전 검증과 이전 체크포인트 보존

- state: implemented (EditMode green, independentReview false)
- phase: P2 첫 슬라이스 (R3). 스키마 변환·인벤토리·퀘스트·locale 대응표는 이 패킷 범위 밖
- prerequisites: G0 문서, P1 Sequence 계약 (EditMode). G1 체크리스트의 ScenarioScript 단일 실행기 지정은 미완이며 이 패킷의 선행이 아님
- baseRevision: `7c2557a3`
- actualModel / runner / editorOwner: Cursor 부모 / Unity PID는 실행 시 `status`로 재확인
- 행동 AC:
  - `CheckpointRepository.Save`는 `resumeSceneName`이 공백이거나, Fungus 스냅샷 키가 공백이거나, 같은 키가 bool/int/string 중 둘 이상(또는 한 배열 안 중복)이면 `ArgumentException`을 던진다
  - 그 예외가 나기 전에는 `Checkpoint.Latest.v1` / `Checkpoint.LatestId.v1`를 쓰지 않는다. 이미 있는 체크포인트 JSON은 그대로 `TryLoad` 된다
  - 설정 키(`SettingPlayerPrefsKeys`)는 이 패킷에서 지우거나 다시 쓰지 않는다
- allowedFiles:
  - 소유: `disputatio/Assets/godlotto/Script/Progress/Checkpoint/CheckpointRepository.cs`
  - 테스트: `disputatio/Assets/Editor/Tests/EditMode/Checkpoint/CheckpointRepositoryTests.cs`
  - 문서: 본 패킷, `docs/development/tasks/fungus-deletion/index.md`, `docs/architecture.md` §4 체크포인트 문장, `TODO.md`
- excluded: 씬·프리팹, Fungus SavePoint/`SaveManager` 삭제, `PlayDataPrefsCleaner`의 Fungus 디스크 삭제, 스냅샷 필드 제거, 구버전→신버전 변환, `CheckpointLoadCoordinator` 동작 변경, 커밋
- oldOwner → newOwner: 저장 직전 검증 없음 → `CheckpointRepository.Save`가 쓰기 전에 거부
- save/input/scene 영향: PlayerPrefs 체크포인트 키만. 씬 에셋 미변경. Play/QA 미실행
- verification:
  - `.\scripts\unity-cli.cmd --project disputatio status`
  - `.\scripts\unity-cli.cmd --project disputatio editor refresh --compile`
  - `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter CheckpointRepositoryTests`
  - 회귀: `CheckpointServiceTests`
- rollback: 위 소스·테스트·문서만 `7c2557a3` 기준으로 되돌림

## 실행

- [x] 관련 코드/참조 조사 및 AC 확정
- [x] 실패하는 테스트 먼저 (TDD) — RED: 4건 `Expected ArgumentException but was null`
- [x] AC 범위 내 최소 구현
- [x] compile + EditMode 필터
- [ ] independentReview: false (같은 세션). R3 verified는 별도 세션 리뷰 후
- [x] index·인계 갱신

## 증거

- command / exitCode / executed / passed / failed / skipped:
  - 2026-09-22 `status` → exit 0, ready, PID 48440
  - RED `CheckpointRepositoryTests` → 4 failed / 3 passed (`ArgumentException` 없음)
  - `editor refresh --compile` → exit 0, console error/warning `[]`
  - GREEN `CheckpointRepositoryTests` → 7/7
  - 회귀 `CheckpointServiceTests` → 6/6
- runRoot / screenshots / consoleDelta: Play/QA 없음
- before/after behavior and state: 잘못된 저장은 PlayerPrefs 쓰기 전에 예외. 이전 `checkpointId`가 유지됨
- reviewSession / findings / resolution: 같은 세션. independentReview false

## 중단 또는 완료 인계

- completedACs: 공백 씬 이름·공백 Fungus 키·중복/타입 충돌 저장 거부, 이전 체크포인트 유지
- incompleteACs: 저장 키 대응표, 손상 JSON과 없음의 구분, 변환, G2 독립 리뷰
- changedFiles / diffIdentity: `CheckpointRepository.cs`, `CheckpointRepositoryTests.cs`, 본 패킷, `index.md`, `docs/architecture.md` §4 한 문장. HEAD는 여전히 `7c2557a3` (미커밋)
- lastVerifiedCommand / result: `CheckpointRepositoryTests` 7/7, `CheckpointServiceTests` 6/6, console `[]`
- currentEditorProject / pid / mode: `D:/Capstone/newCapstone/disputatio`, PID 48440, ready
- blocker: 없음
- nextAction: 별도 세션 리뷰 또는 저장 키 대응표. 커밋은 사용자 요청 시
- nextCommand: `.\scripts\unity-cli.cmd --project disputatio test --mode EditMode --filter CheckpointRepositoryTests`
- doNotRepeat: 손상 JSON을 조용히 지우고 새 게임으로 초기화, Fungus 변수와 FlagStore 동시 쓰기
