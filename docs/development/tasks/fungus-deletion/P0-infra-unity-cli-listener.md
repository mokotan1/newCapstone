# P0-infra-unity-cli-listener / Unity CLI 리스너 복구

- state: verified
- phase: P0 인프라 (R3 하네스, 게임 코드 변경 없음)
- prerequisites: G0 문서. 라이브 QA·P1 EditMode 검증이 listener health에 의존
- baseRevision: e1de9505 + P0 inventory 워킹트리
- actualModel / runner / editorOwner: Cursor 부모. Editor PID는 실행 시 status로 재확인
- 행동 AC:
  - 입력: `.\scripts\unity-cli-open-status-cmd.cmd`가 이미 떠 있으면 재실행하지 않음. `.\scripts\unity-cli.cmd --project disputatio status`
  - 전제: disputatio 프로젝트가 Editor에 열려 있고 QA playtester lease가 없음
  - 관찰: status exit 0, ready, health 2xx. timeout을 실패 확정으로만 기록하고 동일 mutation을 재전송하지 않음 (FD14)
- allowedFiles: 없음 (게임/씬/프리팹 금지). 증거만 `docs/development/tasks/fungus-deletion/index.md` 인계 필드
- excluded: Fungus 패치, 리스너 복구용 벤더 수정, Kitchen 확장, 패키지 삭제, 커밋
- oldOwner → newOwner: 해당 없음
- save/input/scene 영향: 없음. Play Mode를 켜지 않음
- verification: `status` 1회. 기대 실행 수 1. Play 시나리오 없음
- rollback: 변경 없음

## 실행

- [x] 관련 코드/참조/에셋 조사 및 AC 확정
- [x] baseline: 2026-09-17 health timeout 기록
- [x] AC 범위 내 최소 구현 (Editor 연결만)
- [x] status + P1/P3 EditMode 재검증 (2026-09-21, 2026-09-22)
- [x] independentReview: false (연결 확인 패킷)
- [x] index·인계 갱신

## 증거

- command / exitCode / executed / passed / failed / skipped:
  - 2026-09-22 `status` → exit 0, ready, PID 48440
- runRoot / screenshots / consoleDelta: 없음
- before/after behavior and state: listener reachable; compile + EditMode filters 실행 가능
- reviewSession / findings / resolution: 해당 없음

## 중단 또는 완료 인계

- completedACs: status ready, health 2xx
- incompleteACs: Play/QA lease 확인은 플레이 작업 전에 재수행
- changedFiles / diffIdentity: 없음
- lastVerifiedCommand / result: 2026-09-22 `status` exit 0
- currentEditorProject / pid / mode: `D:/Capstone/newCapstone/disputatio`, PID 48440, ready
- unfinishedProcess / runId / uncertainMutation: 없음
- blocker: 없음
- lastObservedFailure: 2026-09-17 `timed out waiting for Unity listener` (해소)
- nextAction: P2 패킷 설계 또는 P3 대사 슬라이스
- nextCommand: 작업 패킷의 EditMode `--filter` 실행
- doNotRepeat: health timeout 후 동일 mutation 무한 재전송, Fungus Continue 패치
