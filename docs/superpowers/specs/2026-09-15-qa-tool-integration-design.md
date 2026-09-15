# QA 도구 통합 설계 및 상세 완료조건

- 작성일: 2026-09-15
- 상태: 1단계 구현 진행 중 / 라이브 Editor 미실행 / `independentReview: false`
- 조사 기준 revision: `2047d55feeddc9e18184005ebaa42cff49bb1b81`
- 구현 브랜치: `feature/qa-tool-integration` (계약 커밋 `61316a26`; coordinator는 미커밋 가능)
- 요청: 기존 아키텍처와 QA 기반의 공백을 보완하고 관찰 가능한 작업 완료조건을 정의한다.
- 계약 본문(§1–§11)은 완료조건을 유지한다. 실행 상태는 **§12 AC 대응표**만 갱신한다.
- 이 문서의 체크박스나 pytest 통과만으로 1단계 verified를 선언하지 않는다.

## 1. 목표와 범위

QA 담당자가 검사 대상을 선택하면 기존 Gateway로 실행하고, 재현 경로와 증거를 수집하여 동일한 기준으로 결과를 판정한다. 첫 수직 구현 범위는 홀에서 주방까지의 이동이다. 이후 방 회귀, 연속 진행, AI·설정·Player 검사를 같은 계약에 연결한다.

### 1.1 핵심 성공 기준

1. 명령 성공과 게임플레이 성공을 혼동하지 않는다.
2. 필요한 검사가 빠졌거나 증거가 부족하면 PASS 또는 verified를 선언하지 않는다.
3. 정상 종료·실패·취소 후 QA 저장과 Editor 소유권을 안전하게 정리한다.
4. 다른 담당자가 기록된 기준·환경·초기 상태로 재실행할 수 있다.
5. 지원하지 않는 구역과 환경도 미실행/차단 항목으로 드러낸다.

### 1.2 이번 설계에서 제외하는 것

- 게임 퍼즐·저장 정책·AI 제품 요구사항 자체의 변경.
- 공식 Unity CLI로 기본 backend 전환, Unity 버전 변경.
- QA 실행 중 제품 코드 자동 수정, 자동 커밋·push·배포.
- 웹 서비스·계정·클라우드 DB 구축. 초기 인터페이스는 CLI와 로컬 보고서다.
- 모든 언어·해상도·GPU 조합을 첫 단계에서 전수 검사하는 것.
- EventSystem 주입 성공을 OS 입력 또는 출시 Player 검증으로 인정하는 것.

## 2. 근거와 확인된 공백

경로는 저장소 루트 기준이다. 문서와 코드에서 확인한 내용이며, 실행 검증 여부는 별도로 표시한다.

| ID | 근거 | 확인 내용 | 설계 대응 |
|---|---|---|---|
| G01 | `scripts/qa/rooms/orchestrate_area.py` | 구역 순서를 출력하는 stub, chained traversal 미구현 | 실제 순차 실행·취소·결과 연결 |
| G02 | `QA/SceneAdapters/HallQaAdapter.cs` | 클릭은 `OnInteraction("left")`, assert-route는 controller 존재 검사 | 상태 전후·목적지·입력 복구 assertion |
| G03 | `QA/Evidence/QaRunManifest.cs` | 통과 assertion과 ScreenshotAttached 이벤트로 Pass 가능 | 최종 판정기에서 필수 증거와 파일 내용 검증 |
| G04 | `scripts/qa/autorun/checkpoint.py` | git commit은 있으나 diff 식별값 없음 | 시나리오·변경·환경 fingerprint |
| G05 | `scripts/qa/autorun/orchestrator.py` | 수정·커밋 상태가 있는 repair skeleton | 실행과 수정 작업 인계 분리 |
| G06 | `QA/Input/QaEventSystemInputDriver.cs` | Raycast 후 ExecuteEvents로 이벤트 주입 | 입력 계층을 결과 계약에 명시 |
| G07 | `scripts/qa/rooms/preflight.py` | live capability 누락 비교 helper | 소유권·저장·환경까지 사전 점검 연결 |
| G08 | `python -m scripts.qa.rooms.coverage_audit` | 이번 조사에서 exit 1, 지하 manifest 6개 누락 | 커버리지 공백 유지 및 후속 팩 작성 |

`QA/`는 `disputatio/Assets/mokotan/mokotan/script/QA/`를 뜻한다.

G08 실행 결과: buildSceneCount=56, catalogRegionCount=20, excludedSceneCount=7. 누락은 basement.brick, basement.entry, basement.extraction, basement.hall, basement.observation, basement.research. 다른 gap 배열은 비어 있다. 파일 존재 검사가 통과해도 게임플레이 커버리지 통과는 아니다.

우선 적용 계약:

- `.harness/unity-policy.md`, `.harness/unity-verification.md`
- `docs/architecture.md` §3, §4, §6, §8
- `docs/development/feature-workflow.md`
- `.cursor/rules/qa-subagent-orchestration.mdc`
- `.cursor/agents/qa-playtester.md`, `.cursor/agents/qa-evidence-reviewer.md`

기존 미커밋 `docs/qa/2026-09-15-develop-qa-plan.md`는 참고 자료로만 사용한다. 그 문서의 별도 develop revision을 이번 checkout 실행 결과와 합치지 않는다.

## 3. 구조와 책임

```text
CLI / 후속 개발자 패널
  → 실행 계획: 요구사항, 시나리오, 환경, 필수 증거 고정
  → 실행 총괄: preflight → lease/profile → 순차 실행 → cleanup
      → Unity adapter → 기존 QA Gateway → 기존 runner/scene adapters
      → 후속 Python test/eval adapter
      → 후속 Player·수동 증거 adapter
  → evidence validator → 결과 집계 → report.json / report.md
```

| 구성요소 | 책임 | 소유하지 않는 것 |
|---|---|---|
| Plan builder | 선택 범위, 요구사항 대응, timeout, 실행 순서 고정 | Unity 상태 변경 |
| Run coordinator | 상태 전이, attempt 기록, 취소, cleanup 지휘 | 별도 Unity 잠금 구현, 제품 수정 |
| Unity adapter | backend 응답 정규화, 명령·상태 대응 | exit 0을 PASS로 변환 |
| 기존 Gateway | 단일 Unity 조작 진입, lease/profile/runner 연결 | 전체 요구사항 verified 판정 |
| Evidence validator | 파일·식별값·필수 assertion·콘솔 검사 | 누락 증거 생성, 원본 결과 수정 |
| Report builder | 동일 정규화 결과로 JSON/Markdown 생성 | 독립적인 판정 로직 |

기존 scenario runner 두 종류와 capability registry를 즉시 통폐합하지 않는다. adapter가 형식을 구분해 호출하고 결과를 공통 계약으로 변환한다. 새 글로벌 MonoBehaviour Manager는 추가하지 않는다.

## 4. 데이터 계약

아래는 신규 계약이다. 기존 파일이 이미 이 필드를 제공한다고 가정하지 않는다.

### 4.1 실행 계획

`planId`, `schemaVersion`, `requirementIds`, `scenarioIds`, `scenarioHashes`, `target`, `requiredChecks`, `timeouts`, `exclusions`를 가진다.

- 각 요구사항은 근거 문서와 절, 기대 결과, 필수 시나리오·입력 계층을 참조한다.
- 각 단계는 안정적 stepId와 사전조건, 동작, 관측 조건, 제한 시간, 필수 artifact 종류를 가진다.
- 대상 씬은 실행 중 관측값에서 정답을 생성하지 않는다. 사전에 고정된 기대 씬과 대조한다.
- 기획과 씬 연결이 충돌하면 `spec-mismatch`로 실행을 차단하고 차이를 남긴다.
- 고정 sleep으로 성공을 추정하지 않고 조건을 polling한다. deadline은 단조 시계, 기록 시각은 UTC다.
- 기본 제한 시간 제안: preflight 60초, 일반 단계 30초, 시나리오 300초, cleanup 60초. AI/긴 연출은 근거와 함께 계획에서 명시적으로 변경한다. 실행 중 자동 연장하지 않는다.
- heartbeat 주기는 기존 lease TTL의 1/3 이하로 설정하며 실제 TTL과 주기를 기록한다. TTL을 별도 상수로 중복 정의하지 않는다.

### 4.2 입력 계층

| inputLayer | 인정하는 증명 범위 |
|---|---|
| api | 기능 API·capability의 상태 변화 |
| event-system | Unity raycast·이벤트 전달·입력 차단 경로 |
| player-input | 실제 Player 창 입력과 관측 결과 |

기존 `RealInput` enum은 호환성을 위해 유지할 수 있지만 정규화 결과에는 `event-system`으로 표시한다. API와 EventSystem 검사는 각각 동일한 초기 상태로 reset 후 실행한다. 한쪽 실패를 다른 쪽 성공으로 상쇄하지 않는다. 첫 단계는 api와 event-system을 필수로 하고 player-input은 후속 단계에 둔다.

### 4.3 실행 결과와 식별값

공통 하네스 필드 `schemaVersion`, `operation`, `backend`, `toolVersion`, `connectorVersion`, `projectPath`, `editorPid`, `startedAt`, `finishedAt`, `executionStatus`, `verificationStatus`, `nativeExitCode`, `artifacts`, `errorCategory`를 보존한다.

QA 확장 필드:

- `runId`, `attemptId`, `planId`, `scenarioId`, `stepId`, `inputLayer`.
- `baseRevision`, `diffHash`, `scenarioHash`, `planHash`, `environmentFingerprint`.
- `scenarioVerdict`, `reasonCodes`, `assertions`, `cleanupStatus`, `review`.
- assertion은 ID, 기대값, 관측값, 판정, 시각, 근거 artifact ID를 가진다.
- artifact는 상대 경로, 종류, byte 크기, SHA-256, 생성 단계·시각을 가진다.
- 테스트 adapter는 matched/executed/passed/failed/skipped를 기록한다. unknown은 0으로 바꾸지 않는다.

diffHash는 staged/unstaged 변경과 실행 영향 범위의 untracked 소스를 포함한다. 생성된 QA 증거 자체는 제외한다. 대상 파일 목록과 hash 산식을 기록하며, 판단 불가한 변경은 영향 범위에 포함한다. 환경은 OS, Unity, backend, 언어, 해상도, 실행 대상과 해당 시 모델·런타임 hash 및 장치 정보를 포함한다. Player 검사는 실행 패키지 hash가 필수다.

### 4.4 증거 디렉터리

```text
docs/qa/runs/<UTC>-run-<id>/
  plan.json                 # 실행 시작 시 고정된 계획
  context.json              # revision/diff/환경/도구
  events.jsonl              # append-only 상태·명령·입력 이벤트
  attempts/<attempt-id>/    # assertion, 화면, 원본 응답·로그
  cleanup.json              # 저장·프로파일·lease 복구 증거
  report.json               # 최종 정규화 결과
  report.md                 # 같은 결과의 사람이 읽는 표현
  review.json               # 별도 리뷰 결과, 있을 때만 생성
```

기존 Gateway의 manifest는 원본 artifact로 유지한다. 신규 report가 최종 집계이며 구버전 manifest의 Pass만으로 승격하지 않는다. 증거 경로는 run root 안으로 정규화하고 경로 탈출·외부 링크 대상은 거부한다. 비밀 값과 인증 헤더는 수집 단계에서 제외/마스킹한다.

## 5. 상태·실패·복원 계약

```text
planned → preflight → acquiring → running → collecting → restoring → finalized
                       모든 부분 실패·취소 ───────────────→ restoring
restoring 실패 → recovery-required (새 실행 차단)
```

- preflight에서 아무 상태도 변경하지 않았다면 cleanup은 근거 있는 not-applicable이다.
- 획득 또는 profile 전환 중 부분 실패도 실제 획득한 자원을 조사해 정리한다.
- coordinator는 기존 Gateway lease를 사용한다. 파일 잠금만으로 Unity 소유권을 선언하지 않는다.
- mutation 응답 timeout은 `executionStatus=timed-out`로 기록한다. run/command 식별값으로 상태를 조회하고 같은 mutation을 맹목적으로 재전송하지 않는다.
- Gateway가 요청 식별 조회를 지원하지 않으면 `recovery-required`로 남긴다. 재시작 성공을 추정하지 않는다.
- 재실행은 새 attempt이며 초기 상태에서 시작한다. 첫 단계에서는 중간 단계 resume를 지원하지 않는다.
- 프로세스 중단 후 finally 실행을 가정하지 않는다. 다음 시작 시 미완료 journal과 live 상태를 대조하고 기존 recover 경로로 복원한 뒤 새 실행을 허용한다.
- 일반 저장의 파일·키 존재 여부와 값/hash를 전후 비교한다. QA가 변경할 수 있는 키와 파일 범위는 사전 목록으로 고정한다.
- 사용자 설정·저장 차이를 발견하면 원본을 덮어쓰며 자동 해결하지 않는다. 복구 필요 상태와 백업 위치를 남긴다.
- 제품 결함과 QA 결함은 분류 후 재현 패킷으로 인계한다. 수정 후에는 새 revision/diff 기준의 새 run을 생성한다.

## 6. 최종 판정 규칙

### 6.1 시나리오 판정 우선순위

1. 유효한 관측으로 필수 기대 결과 위반이 확인되면 FAIL. 동시에 증거 누락이나 cleanup 실패가 있어도 실패 사실은 보존한다.
2. 실패가 확인되지 않았지만 필수 실행·증거·환경·복원이 부족하면 BLOCKED.
3. 아직 실행 대상으로 시작하지 않은 케이스는 NOT_RUN. 사전 점검에서 시도했으나 실행 불가한 케이스는 BLOCKED.
4. 모든 필수 단계·입력 계층·assertion·화면·콘솔·cleanup이 충족됐을 때만 PASS.

관측 조건의 deadline 초과는 환경과 관측기가 정상임이 확인되면 FAIL, 연결/관측 불가로 결과를 알 수 없으면 BLOCKED다. 관련 신규 Console 예외는 FAIL, 관련성 미분류는 BLOCKED다. 기존 예외는 fingerprint와 판단 근거를 baseline에 남기며 일괄 무시하지 않는다.

스크린샷 첨부 이벤트만으로 충분하지 않다. 파일 존재·비어 있지 않음·디코딩 가능·hash 일치·해당 단계 연결을 검사한다. 화면의 의미·잘림·겹침은 지정된 시각 검토에서 확인한다. 자동 파일 검증을 시각 검토로 표시하지 않는다.

### 6.2 전체 집계

- 필수 케이스 중 FAIL이 있으면 전체 FAIL.
- FAIL이 없고 BLOCKED/NOT_RUN이 있으면 전체 BLOCKED. 케이스별 NOT_RUN은 유지한다.
- 모든 필수 케이스 PASS일 때 실행 묶음 PASS.
- 제외 케이스는 이유와 범위를 표시하며 통과율 분모를 조용히 줄이지 않는다. 전체 요구 수·실행 수·통과 수·차단 수·미실행 수·명시적 제외 수를 함께 표시한다.
- `waived`는 공통 verificationStatus에 보존하며 전체 기능 verified를 막는다.
- 기능 verified는 공통 검증 계약과 위험도별 명세·품질 독립 리뷰까지 충족해야 한다. 실행 묶음 PASS만으로 verified가 되지 않는다.
- 리뷰에서 PASS를 기각하면 원본을 수정하지 않고 reviewed 판정과 사유를 추가한다. 미해결 기각이 있으면 verified 금지.

## 7. 상세 인수조건: 1단계 필수

아래 모든 AC는 구현 완료 시 증거 링크와 실제 결과를 요구한다. 현재 실행 상태는 §12에 있다. 이 표의 완료조건 문구는 바꾸지 않는다.

| AC | 시험 조건·행동 | 관찰 가능한 완료조건 | 필수 증거 |
|---|---|---|---|
| AC01 | 홀→주방 계획 생성 | 요구사항·시나리오·입력 계층·기대 씬·timeout·hash가 고정되고 빈 필수값/중복 ID를 거부 | 유효 계획 및 잘못된 계획 거부 결과 |
| AC02 | Editor 미연결, 다른 프로젝트, 컴파일 중, dirty scene, lease 충돌을 각각 준비 | 이유를 구분해 BLOCKED, 임의 저장·씬 전환·Play 진입 0회 | 각 preflight 결과와 mutation 호출 기록 |
| AC03 | required capability 또는 대상 매핑 누락 | 누락 ID를 보고하고 실행 전 차단, 가능한 API 모드로 조용히 대체하지 않음 | live registry와 누락 목록 |
| AC04 | 두 실행자가 같은 Editor에 시작 요청 | 한 owner만 획득, 다른 실행은 ownership 차단; 첫 owner heartbeat 유지 | owner/lease ID·시각·거부 응답 |
| AC05 | 정상 플레이어 저장·설정을 가진 상태에서 QA 시작 | 격리 프로파일 사용; 종료 후 일반 저장·설정의 존재/값/hash 동일 | 허용 변경 목록과 전후 비교 |
| AC06 | 홀 이동 api 실행 후 reset, event-system 실행 | 두 attempt가 구분되고 각각 초기 상태 검증; 실제 사용한 driver 기록 | 입력 이벤트·초기 상태 snapshot |
| AC07 | 정상 홀→주방 경로 실행 | 사전 고정한 경로를 거쳐 Kitchen 도착, 전환 종료, 입력 게이트 해제; controller 존재만으로 통과 불가 | 씬 전후·전환·입력 assertion과 화면 |
| AC08 | 홀 경로 기획·씬 배선 검토 | 중간 복도/대화가 있으면 계획에 명시; 정확한 클릭 대상/목적지 연결을 실행 전 확정; 불일치는 spec-mismatch | 배선 조사 및 요구사항 대응 기록 |
| AC09 | 필수 assertion 실패 또는 미실행 단계 삽입 | 실패는 FAIL, 누락은 BLOCKED; 일부 성공으로 전체 PASS 불가 | 판정기 반례 테스트 |
| AC10 | 화면 파일 삭제·0 byte·손상·hash 불일치·경로 탈출을 각각 주입 | 모든 사례에서 PASS 거부, artifact ID와 사유 표시 | 독립 evidence validator 테스트 |
| AC11 | baseline 예외, 신규 관련 예외, 미분류 예외를 준비 | baseline 구별, 신규 관련은 FAIL, 미분류는 BLOCKED, 콘솔 미수집도 BLOCKED | 원본 console delta·분류 결과 |
| AC12 | 실행 중 취소 | 새 gameplay 명령을 중단하고 유한 시간 안에 cleanup, cancelled 기록, 시나리오 PASS 금지 | 취소 시각·마지막 명령·cleanup |
| AC13 | mutation 응답 유실 | 동일 mutation 중복 전송 0회; 상태 조회로 조정하거나 recovery-required | fault-injection 호출 순서 |
| AC14 | 실행 실패, profile 전환 부분 실패, cleanup 실패를 각각 주입 | 모든 경우 정리 시도; 정리 불확실 시 다음 실행 차단 | 실패별 journal·복원 결과 |
| AC15 | coordinator 강제 중단 후 재시작 | 미완료 run 감지, live 상태 대조, 복원 전 새 실행 차단; 자동 Editor 재시작 없음 | 중단 전후 journal·recover 증거 |
| AC16 | 동일 commit에서 관련 uncommitted 파일 변경 | diffHash 변경; 이전 PASS를 현재 결과로 재사용하지 않음 | 전후 context·재평가 결과 |
| AC17 | 과거 schema manifest만 전달 | 원본 열람은 가능, 필수 신규 필드 부재를 BLOCKED로 표시 | 호환성 fixture 테스트 |
| AC18 | 결과 JSON/Markdown 생성 | 케이스별 판정·원인·증거 링크·미실행·제외·복원·리뷰 상태 일치 | 두 산출물 대조 |
| AC19 | 실제 backend로 첫 수직 실행 | 실제 호출·대기·수집·복원 수행; stub 출력 또는 fixture만으로 완료 인정 금지 | 실제 Unity run 원본 응답·보고서 |
| AC20 | QA 실행 중 제품 결함 발견 | 제품 코드/커밋 변경 0건, 요구사항·환경·재현·기대/실제·증거를 결함 패킷으로 생성 | 전후 소스 상태와 패킷 |
| AC21 | 관련 테스트 0개 실행 또는 backend exit 0 + 실패 결과 | 검증 passed 금지; 테스트 0개와 실제 실패를 별도 분류 | 결과 정규화 반례 테스트 |
| AC22 | domain reload 또는 재연결 후 동일 수직 실행 | stale PID/lease를 성공으로 간주하지 않고 재연결 확인 후 정상 실행·복원 | reload 전후 연결·실행 기록 |
| AC23 | 필수 독립 리뷰 없음 또는 리뷰 기각 | 실행 PASS와 별개로 feature verified 금지 | review 필드 및 보고서 |

AC07은 목적지 도착 요구이며, 현재 홀 왼쪽 클릭 한 번이 즉시 Kitchen으로 전환한다는 주장이 아니다. AC08에서 필요한 전체 경로를 확정하고 그 경로를 실행한다. 경로 확정이 불가능하면 1단계는 BLOCKED다.

## 8. 후속 단계 완료조건

### 2단계: 방 회귀와 연속 진행

| AC | 범위 | 완료조건 |
|---|---|---|
| AC24 | 주방·가정부방·서재 | 각 방의 정상 경로, 잘못된 아이템/입력, 이탈·재진입, 해결 후 재조작을 기획 근거와 연결하고 실행 증거 확보 |
| AC25 | 연속 진행 | 방마다 reset하지 않는 하나의 진행 세션에서 입장·퇴장 계약과 아이템·플래그 보존 검증; 순서만 출력하면 실패 |
| AC26 | 저장·이어하기 | 격리 프로파일에서 저장→종료→재시작→이어하기 후 위치·아이템·진행·설정 대조; 저장 정책 충돌은 spec-mismatch |
| AC27 | 커버리지 | 요구사항 단위로 자동/API/입력/수동/미지원 표시; 파일 존재율과 실행 통과율 분리 |

2단계 범위는 위 세 방과 그 연결 경로다. 다른 방까지 완료했다고 확대하지 않는다. 정답·보상·아이템 소비 정책은 해당 방 명세를 사용하며 현재 코드값을 그대로 기대값으로 만들지 않는다.

### 3단계: AI·설정·Player 및 지하

| AC | 범위 | 완료조건 |
|---|---|---|
| AC28 | 로컬 AI | 자동 시작→Ready→대화, 종료 정리, 초기화 실패·복구를 제품 AC와 연결; 실제 장치·모델 hash·시간·소유 프로세스 기록 |
| AC29 | 설정 UI | CPU/GPU/AUTO 재진입 조작과 요청/실제 장치 구분, ko/ja/en × 1280×720/1920×1080의 표시·입력 증거 확보 |
| AC30 | Player | 패키지 hash가 고정된 실제 실행본에서 player-input 증거 수집; Editor 결과로 대체 금지 |
| AC31 | 오프라인 설치 환경 | 시스템 Python 없는 분리 환경과 제품 요구에 따른 외부 통신 차단 상태에서 실행; 환경/패키지 미비는 BLOCKED |
| AC32 | 지하 6개 구역 | 각 manifest·요구사항·시나리오·입장/퇴장 조건 작성; 정적 audit 재실행; gameplay 상태는 실제 증거에 따라 별도 판정 |

3단계는 후속 하위 범위별로 별도 작업을 산정한다. AI 성능 임계값은 승인된 제품 명세를 가져오고 이 QA 도구 문서에서 임의로 정하지 않는다. 1단계 verified는 2·3단계 완료를 의미하지 않는다.

## 9. 허용 수정 범위와 통합 순서

구현 허용 파일은 `docs/development/tasks/qa-tool-integration/index.md`에 고정한다. 아래는 모듈 후보이며 전체 디렉터리 수정 허가가 아니다.

| 범위 | 후보 위치 | 연결 AC |
|---|---|---|
| 계획·실행·판정·보고 | `scripts/qa/`의 신규 통합 모듈, 기존 rooms/autorun 중 필요한 파일 | AC01–04, AC09–23 |
| 하네스 결과 정규화 | `scripts/unity_harness/result_contract.py`와 테스트 | AC21 |
| Gateway·증거·입력·profile | 기존 QA 하위 해당 정의 파일과 Unity 테스트 | AC04–17, AC19, AC22 |
| 홀 경로 | HallQaAdapter 및 해당 scenario/manifest와 테스트 | AC06–08 |
| 아키텍처 설명 | `docs/architecture.md` 관련 절 | 구현 반영 후 |

씬·프리팹·ProjectSettings 변경은 AC08 등에서 필요성이 입증될 때 부모 소유로 추가한다. 정상 플레이 동작을 QA 통과에 맞춰 바꾸지 않는다. 같은 checkout 쓰기는 순차, Unity 조작자는 한 명이다.

통합 순서: 계약·반례 판정 → Gateway adapter·preflight → 복원/취소 → 홀 경로 → 실제 실행 → 독립 리뷰 → 회귀. 구현 중 QA 실행이 필요하면 쓰기를 멈추고 Editor 소유권을 인계한다.

## 10. 검증 전략과 완료 게이트

### 10.1 필요한 검증

- Python: 계획/판정/경로/식별값/timeout/중단 복구의 반례 테스트. 기존 `python -m pytest scripts/qa/tests -q` 회귀.
- 하네스 변경 시 `python -m pytest scripts/unity-harness/tests -q` 및 관련 결과 계약 테스트.
- C# 변경: 실제 Unity 컴파일, 영향받는 EditMode 테스트, 테스트 실행 수 양수, 신규 관련 Console 오류 검사.
- 입력/수명주기/저장/전환: 실제 PlayMode 또는 플레이 증거. Python fixture 통과로 대체하지 않는다.
- 패키지/CLI 연결 영향: domain reload 후 재연결, 취소·복구 및 동일 수직 실행.
- 시각 검토: 지정 해상도·상태의 화면을 열어 확인하고 검토자를 기록한다.
- 명세 리뷰 후 별도 품질 리뷰: 필수 AC 누락, 중복 상태 관리, 범위 초과, 임시 우회, 불필요 Manager를 점검한다.

실제 명령 문법은 `.harness/unity-toolchain.json`과 backend 문서를 따른다. 이 문서가 backend 명령의 새 원본이 되지 않는다. 테스트 필터는 구현 파일 확정 후 대응표에 기록한다.

### 10.2 1단계 작업 완료 체크리스트

- [ ] AC01–AC23 각각에 구현 파일, 검증 명령/행동, 실제 결과, 증거 경로가 있다.
- [ ] 실제 Editor에서 홀→주방 api/event-system 실행과 복원이 확인됐다.
- [ ] 취소·응답 유실·부분 실패·중단 후 복구 반례가 통과했다.
- [ ] 일반 저장·설정 보존과 lease 독점이 확인됐다.
- [ ] 미커밋 변경을 포함한 실행 식별값이 기록됐다.
- [ ] 필수 검증은 passed 또는 근거 있는 not-applicable이고 waived/blocked/실패가 없다.
- [ ] 명세·품질 리뷰가 별도 세션에서 수행되고 기각 사항이 해결됐다.
- [ ] 리뷰 이후 변경이 있다면 영향받는 검사와 리뷰를 재평가했다.
- [ ] 허용 파일 밖 변경, 실행 중 제품 수정, 요청 없는 커밋이 없다.
- [ ] JSON/Markdown 보고서에 1단계 범위와 후속 미완료 범위가 명시돼 있다.

하나라도 충족하지 못하면 `1단계 구현 반영, 검증 미완` 또는 구체적인 failed/blocked로 보고한다. 사용할 수 없는 리뷰 도구를 실행했다고 기록하지 않는다. R3 구현은 독립 리뷰가 없으면 verified가 아니다.

### 10.3 구현 시 작성할 AC 대응표

각 AC에 `status`, `implementationFiles`, `testOrAction`, `executionStatus`, `verificationStatus`, `evidencePaths`, `reviewReference`, `limitations`를 기록한다. 현재 값은 §12다. 체크박스 선택이나 테스트 코드 존재만으로 라이브 gameplay passed를 부여하지 않는다.

## 11. 설계문서 자체의 완료조건

- 기존 구현과 제안 기능이 구분돼 있다.
- 각 단계의 범위와 AC, 기대 관측, 필요한 증거가 연결돼 있다.
- FAIL/BLOCKED/NOT_RUN/PASS 및 기능 verified의 경계가 모순되지 않는다.
- 성공 외 취소·timeout·부분 실패·프로세스 중단·복원이 정의돼 있다.
- 기존 소유권·저장 격리·독립 리뷰·커밋 정책을 약화하지 않는다.
- 참조한 로컬 파일 경로가 존재하며 기존 사용자 파일을 수정하지 않는다.

이 문서 자체는 R0 경로로 자체 검토할 수 있다. 구현 계획·코드·독립 리뷰·Unity 실행 완료를 포함하지 않는다.

## 12. AC 대응표 (구현 현황)

기록일: 2026-09-15. 검증 명령: `python -m pytest scripts/qa/tests -q` (이 세션 88 passed, coordinator 포함 시 증가). 라이브 Editor 실행 0회. `independentReview: false`.

배선 조사 (AC08, 씬 YAML 정적 읽기, PlayMode 아님):

- `Hall_playerble` `CorridorEntranceController` `left` / `Left_Clicked` → `Hall_Left`
- `Hall_Left` Fungus `Front_clicked` LoadScene → `Hall_Left2`
- `Hall_Left2` Fungus `Door_Clicked` LoadScene → `Kitchen`
- `Hall_Left` / `Hall_Left2`에는 C# `interactionId`가 없다 (Clickable2D).
- `transition.hall-to-kitchen.json`은 중간 씬 없이 `scene.Kitchen`만 적는다. 이 hop을 생략한 계획은 `spec-mismatch`다.
- `HallQaAdapter` assert-route는 `HallQaRouteAssertion`으로 Kitchen 도착·전환 종료·입력 게이트를 본다. EditMode 단위 테스트만; 라이브 Kitchen 도착은 NOT_RUN.

| AC | status | implementationFiles | testOrAction | executionStatus | verificationStatus | limitations |
|---|---|---|---|---|---|---|
| AC01 | unit-green | `scripts/qa/tool/plan.py` | `pytest scripts/qa/tests/test_tool_plan.py` | succeeded | passed | 라이브 계획 CLI 없음 |
| AC02 | partial | `scripts/qa/tool/preflight.py` | `test_tool_preflight.py` 스냅샷 | succeeded | NOT_RUN | 실제 Editor 미연결/dirty/compile 미주입 |
| AC03 | partial | `preflight.py` + `rooms/preflight.py` | 같은 파일 | succeeded | NOT_RUN | live registry는 fixture |
| AC04 | partial | `preflight.acquire_lease` | in-memory store | succeeded | NOT_RUN | 실제 Gateway lease 아님 |
| AC05 | NOT_RUN | — | — | NOT_RUN | NOT_RUN | 격리 프로파일 미연결 |
| AC06 | partial | `scripts/qa/tool/hall_route.py` | `test_tool_hall_route.py` 이중 attempt 기록 | succeeded | NOT_RUN | api/event-system 실실행·reset은 fixture |
| AC07 | partial | `HallQaRouteAssertion.cs`, `HallQaAdapter.cs` | `HallQaRouteAssertionTests`, `HallQaCapabilityTests` | NOT_RUN | NOT_RUN | EditMode 단위만. 라이브 Kitchen 도착·화면 없음 |
| AC08 | unit-green | `hall_route.py` + 씬 YAML | 정적 hop 고정, 생략 시 spec-mismatch | succeeded | passed | PlayMode hop 미확인. `Hall_Left`/`Hall_Left2`에 C# interactionId 없음 |
| AC09 | unit-green | `scripts/qa/tool/verdict.py` | `test_tool_verdicts.py` | succeeded | passed | — |
| AC10 | unit-green | `scripts/qa/tool/evidence.py` | `test_tool_evidence.py` | succeeded | passed | — |
| AC11 | NOT_RUN | — | — | NOT_RUN | NOT_RUN | 콘솔 분류기 없음 |
| AC12 | partial | `scripts/qa/tool/coordinator.py` | `test_tool_coordinator.py` | succeeded | NOT_RUN | RecordingGateway만 |
| AC13 | partial | `coordinator.py` | 같은 파일 | succeeded | NOT_RUN | 실제 mutation 유실 없음 |
| AC14 | partial | `coordinator.py` | 같은 파일 | succeeded | NOT_RUN | 실제 profile/cleanup 없음 |
| AC15 | partial | `coordinator.py` journal | 같은 파일 | succeeded | NOT_RUN | 프로세스 강제 중단 아님 |
| AC16 | NOT_RUN | — | — | NOT_RUN | NOT_RUN | diffHash 없음 |
| AC17 | unit-green | `verdict.py` | `test_tool_verdicts.py` | succeeded | passed | — |
| AC18 | unit-green | `scripts/qa/tool/report.py` | `test_tool_report.py` | succeeded | passed | — |
| AC19 | NOT_RUN | — | — | NOT_RUN | NOT_RUN | stub만으로 완료 금지 |
| AC20 | NOT_RUN | — | — | NOT_RUN | NOT_RUN | — |
| AC21 | unit-green | `scripts/qa/tool/normalize.py` | `test_tool_normalize.py` | succeeded | passed | result_contract 래핑 |
| AC22 | NOT_RUN | — | — | NOT_RUN | NOT_RUN | domain reload 없음 |
| AC23 | NOT_RUN | — | — | NOT_RUN | NOT_RUN | 독립 리뷰 없음. verified 금지 |

`reviewReference`: 없음. `evidencePaths`: pytest 로컬 출력만. 런 디렉터리 보고서 없음.
