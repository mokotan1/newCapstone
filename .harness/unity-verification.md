# Unity 검증 계약

완료 판정은 실행 도구와 분리한다. Cursor, 로컬 래퍼, CI가 같은 변경에 같은 필수 검증을 고른다. backend 사용법은 `.harness/unity-toolchain.json`과 `.cursor/skills/newcapstone-unity-automation/SKILL.md`를 본다.

## 작업 상태와 검증 상태

작업 상태: `planned` / `running` / `review` / `changes-requested` / `verified` / `blocked`.

검증 상태(`verificationStatus`): `passed` / `failed` / `blocked` / `not-applicable` / `waived`.

실행 상태(`executionStatus`): `succeeded` / `failed` / `timed-out` / `cancelled` / `blocked`.

CLI exit code 0만으로 `verificationStatus`를 `passed`로 바꾸지 않는다. 필수 검증이 모두 `passed` 또는 근거 있는 `not-applicable`이고, 해당 위험도 리뷰가 충족돼야 작업이 `verified`다. 사용자 요청으로 생략한 검사는 `waived`이며 전체 `verified`가 아니다.

R0 문서/주석 변경은 Unity 실행 없이 검증을 종료할 수 있다. R2/R3는 필수 독립 리뷰가 빠지면 `verified`가 될 수 없다.

## 변경별 필수 증거

| 변경 | 필수 증거 | 추가 조건 |
|---|---|---|
| C# 동작 | 실제 Unity 컴파일, 관련 테스트 결과, 새 관련 Console 오류 유무 | 수명 주기·입력 동작이면 PlayMode 추가 |
| 씬·프리팹·Inspector | 대상 에셋 로드, 누락 스크립트/참조 검사, 의도한 값·연결 확인 | 직접 직렬화 수정 시 대상만 재직렬화; 게임 흐름 변경은 플레이 QA |
| UI 배치·표시 | 지정 해상도·상태의 화면 증거, 잘림·겹침 확인 | 클릭/포커스 변경은 입력 검증; 의미 없는 EditMode 테스트 추가 금지 |
| 저장·씬 전환 | 새 시작·재진입·로드·설정 보존 시나리오 | 정상 플레이어 저장을 쓰지 않고 QA 격리 프로파일 사용 |
| 공유 Interaction | 영향받는 컨트롤러 회귀 및 전환·입력 잠금 확인 | 기존 8개 스위트를 초기 기준으로 보존하고 의존성 증거로 조정 |
| 패키지·CLI | 버전·연결·컴파일·테스트·QA 명령·취소·복구 | 프로젝트 재열기 또는 domain reload 후 재연결 확인 |
| Player 전용 조건부 코드·빌드 설정 | 대상 Player 빌드 결과 | Editor 통과로 Player 성공을 대신하지 않음 |

테스트 필터는 설치된 backend 버전의 의미를 확인한다. 매칭된 테스트 수가 0이면 검증 성공이 아니다. Console은 시작 시점 대비 새 관련 오류를 구분하고 원본 로그를 보존한다. 테스트 기대값을 낮추거나 재시도로 실패를 숨기지 않는다.

기존 실패는 baseline에 기록한다. 필수 대상 테스트가 기존부터 실패한 경우 해당 검증은 fail/blocked로 남기고 전체 verified를 주장하지 않는다. 무관한 기존 실패는 별도 제한으로 보고한다.

## 증거 위치

- 게임 QA: `docs/qa/runs/`
- 일반 하네스: `.harness/runs/<run-id>/` (원본 로그는 기본 커밋 제외)

증거에는 프로젝트 절대 경로, base revision, 실제 변경 파일과 diff 식별값, 도구 버전, 명령·exit code, 시작/종료 시각, 테스트 수, 실패·생략 사유를 남긴다. revision이 같아도 미커밋 diff가 달라지면 관련 증거를 재평가한다.

## 공통 결과 필드

`schemaVersion`, `operation`, `backend`, `toolVersion`, `connectorVersion`, `projectPath`, `editorPid`, `startedAt`, `finishedAt`, `executionStatus`, `verificationStatus`, `nativeExitCode`, `artifacts`, `errorCategory`.

`errorCategory`: `none`, `tool-missing`, `version-mismatch`, `editor-unavailable`, `compilation`, `test-failure`, `ownership`, `timeout`, `invalid-input`.

테스트 결과에는 `matched`, `executed`, `passed`, `failed`, `skipped`를 추가한다. backend가 매칭 수를 제공하지 않으면 unknown으로 남기고 실제 실행 수가 양수인지 검증한다.

컴파일 실패, 테스트 실제 실패, 테스트 0개 실행, 연결 timeout은 각각 다른 결과로 기록한다.

## 현재 backend 명령 (legacy)

활성 backend가 `legacy-unity-cli`일 때의 호출 예는 `.harness/unity-cli-postflight.md`와 스킬을 따른다. 완료 기준은 이 문서가 우선한다.
