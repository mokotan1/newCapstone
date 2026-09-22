# P0-infra-unity-cli-listener / Unity CLI 리스너 복구

- state: planned
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

- [ ] 관련 코드/참조/에셋 조사 및 AC 확정
- [ ] 의미 있는 실패/회귀 테스트 또는 baseline 확보 (status 실패가 baseline)
- [ ] AC 범위 내 최소 구현 + 구 경로 제거 (구현 없음, Editor 연결만)
- [ ] 관련 컴파일/테스트/씬 재로드/플레이 검증 (status only)
- [ ] 위험도에 맞는 독립 리뷰 및 지적 수정 (R3이지만 이번 패킷은 연결 확인. independentReview: false)
- [ ] 원장·index·인계 갱신

## 증거

- command / exitCode / executed / passed / failed / skipped: 미실행
- runRoot / screenshots / consoleDelta:
- before/after behavior and state: 직전 세션 compile/stop timeout 122s
- reviewSession / findings / resolution:

## 중단 또는 완료 인계

- completedACs: 없음
- incompleteACs: status ready
- changedFiles / diffIdentity: 없음
- lastVerifiedCommand / result: 없음
- currentEditorProject / pid / mode / dirty / lease / activeOperation: 미확인 (리스너 다운)
- unfinishedProcess / runId / uncertainMutation: 이전 compile timeout — 재전송 금지
- blocker: Unity health endpoint unreachable
- lastObservedFailure: `timed out waiting for Unity listener`
- nextAction: 사용자가 Editor를 연 뒤 status 한 번만
- nextCommand: `.\scripts\unity-cli.cmd --project disputatio status`
- doNotRepeat: health timeout 후 editor refresh/compile 재전송, Fungus Continue 패치
