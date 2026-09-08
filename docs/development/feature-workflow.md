# 기능 개발 워크플로 (얇은 총괄)

결정일: 2026-09-08. 담당: 이 checkout에서 요청을 받은 부모 에이전트.

가상 조직도를 돌리지 않는다. 총괄이 맥락을 가진 채 구현하고,
위임은 **조사 · (조건 충족 시) 구현 · 리뷰 · QA** 네 갈래만 쓴다.

## 실제 도구 (이 Cursor 러너, 2026-09-08 확인)

시작 시 목록이 바뀌었으면 이 표를 고친다. 없는 타입·모델을 있다고 하지 않는다.

| 용도 | Cursor `Task` `subagent_type` | 모델 지정 |
|---|---|---|
| 경로 조사 | `explore` | `model` 인자 있음 |
| 조건 충족 구현 | `generalPurpose` | `model` 인자 있음. 안 넣으면 상속 |
| 명세·품질 리뷰 | `code-reviewer` 또는 `generalPurpose` **새 세션** | 가능하면 구현과 다른 slug |
| 플레이 QA | `qa-coordinator` 및 `qa-*` | 기존 QA 규칙 |
| 격리 worktree 실험 | `best-of-n-runner` | 파일 겹침 금지. 기본 경로 아님 |

`backend-implementer` 같은 이름은 **도구가 아니다.** 필요하면 `generalPurpose` 프롬프트에
한 문장으로만 넣는다.

이 러너에서 `Task`에 넘길 수 있는 model slug (2026-09-08):

- `inherit` (기본, 부모와 같음)
- `composer-2.5-fast`
- `cursor-grok-4.6-high-fast`
- `claude-sonnet-5-thinking-high`
- `claude-opus-5-thinking-high`
- `claude-fable-5-1-thinking-high`
- `gpt-5.6-sol-medium`
- `gpt-5.6-terra-medium`

목록 밖 slug를 지어내지 않는다. 대표 작업 평가 전에는 모든 선택을 `provisional`로 적는다.
역할 프롬프트에 모델 이름을 적는 것은 배정이 아니다. `model`을 생략하면 **상속**이다.

## 기본 실행 순서

```text
요청 → 총괄이 AC·제외를 적는다
     → 범위 산정(아래)으로 허용 파일·공유 자원을 고정한다
     → 분할 조건 미달: 부모가 구현하고 검증한다
     → 분할 조건 충족: Task로 한 작업만 위임한다 (같은 checkout은 순차)
     → 구현 후 별도 Task로 리뷰한다
     → 플레이가 바뀌면 기존 qa-* 만. QA 중 Editor는 playtester 단독
```

작은 수정(한 모듈, 한 테스트 명령)은 패킷 여러 개가 아니라 부모 한 세션으로 충분하다.

## 범위 산정

수정 범위는 폴더를 먼저 고르지 않는다. **AC에 필요한 파일만** 남긴다.
허용 목록을 비워 두고 구현을 시작하지 않는다.

```text
사용자 요청
  → AC (관찰 가능 동작) + 제외 (하지 않을 것)
  → docs/architecture.md §2는 폴더 힌트일 뿐
  → 심볼을 검색해 정의 파일 → 참조 파일 → 기존 테스트를 모은다
  → 소유 / 테스트 쌍 / 공유 자원으로 나눈다
  → 허용 목록 = 소유 + 그 테스트
  → 공유 자원이 AC에 필수면 쪼개지 않고 부모가 가져간다
```

AC가 범위의 천장이다. “GPU로 추론되게”면 그 동작과 검증에 필요한 파일만 넣는다.
같은 워킹트리의 CI yml, Fungus 에디터 에셋, `bin/` 산출물은 AC에 없으면 제외다.

파일은 검색으로 고른다. 추측으로 `backend_ai/**`를 열지 않는다.
`services/local_ai/**`처럼 글롭은 그 모듈 계약이 한 덩어리일 때만 쓴다.

| 바구니 | 넣는 것 | 누가 고치나 |
|---|---|---|
| 소유 | AC를 구현하는 모듈 | 그 작업 담당 |
| 테스트 쌍 | 그 모듈의 기존·신규 테스트 | 같은 담당 |
| 공유 | 씬, 프리팹, meta, `ProjectSettings`, `SceneNames`/`FungusVariableKeys`, `/chat` 스키마, `docs/architecture.md` | 부모 또는 Editor 단독. 구현 Task 허용 목록에 넣지 않음 |

공유 자원을 빼면 AC가 깨지면 위임하지 않는다. 범위를 억지로 좁혀 Task에 넘기지 않는다.

범위가 커지면 **폴더가 아니라 AC를 나눈다.** 한 명령으로 검증이 안 되면 패킷을 두 개 만들기 전에 동작을 두 개로 자른다. 예: CUDA 핀과 설정창 부담 단계는 파일이 갈라지므로 두 범위. 런타임 + 설정 패널 + 씬은 한 위임에 넣지 않는다.

제외를 명시한다. 이번 AC와 무관한 미커밋, 자동 생성물, `Assets/Fungus/` 코어, 비밀·`.env` 실값.

범위 밖 수정이 필요하면 담당자가 몰래 고치지 않고 부모에게 재배정을 요청한다.
리뷰는 diff가 허용 목록을 넘었는지도 본다.

체셔 GPU 예시:

| AC | 허용 | 제외 |
|---|---|---|
| Gate 1 핀으로 CUDA 선택 | 매니페스트, `gate1.py`, `cuda_manifest.py`, `process_host.py`, 해당 pytest, architecture §8 해당 행 | 설정 UI, CI yml, Unity 씬 |
| 설정창 CPU/GPU/자동 | 패널·`LocalAiControlApi`·UI CSV·EditMode 테스트·패널을 붙이는 팩토리 | CUDA 매니페스트, `main.py` lifespan |
| GPU 부담 단계 | 백엔드 매핑 다음 패널 버튼·문자열 (순차, 파일 안 겹치게) | % 슬라이더, 클라우드 폴백 |

한 세션이 여러 AC를 만지는 것은 부모 구현이다. Task 허용 목록만 위 표처럼 자른다.

## 분할 조건 (모두 충족해야 구현을 위임)

1. 허용 파일이 진행 중인 다른 쓰기 작업과 겹치지 않는다.
2. 완료를 한 종류의 명령으로 검증할 수 있다
   (`pytest …`, `.\scripts\unity-cli.cmd … --filter ClassName`).
3. 이 대화 없이도 패킷만으로 재현 가능하다.
4. 실패해도 부모 checkout의 무관한 파일을 바꾸지 않는다.

하나라도 아니면 위임하지 말고 부모가 한다.

## 남기는 정직 규칙

- 실행하지 않은 위임·리뷰·테스트를 완료로 보고하지 않는다.
- 같은 checkout의 쓰기 구현자는 순차. 병렬은 별도 checkout + 안 겹치는 파일일 때만.
- Unity Editor / 씬 / 프리팹 / `ProjectSettings` 쓰기는 한 세션만.
  `qa-playtester` lease가 있으면 구현은 Editor를 만지지 않는다.
- `verified`는 관련 테스트 통과 + **별도 세션** 리뷰 + 통합 검사가 있을 때만.
  같은 채팅 자기검토는 `independentReview: false`. 이 경우 최종 완료를 주장하지 않는다.
- 커밋·push·배포는 사용자 요청이 있을 때만.
- 기존 사용자 변경을 보존한다. 관련 없는 파일을 고치지 않는다.

## 모델 고르는 법 (추상 등급 없음)

| 작업 | 기본 | 비고 |
|---|---|---|
| 조사 | `explore` + `composer-2.5-fast` 또는 `inherit` | 검색이 목적 |
| 구현 | 부모 `inherit` | 위임할 때만 `generalPurpose` |
| 리뷰 | 새 세션, 가능하면 `gpt-5.6-terra-medium` 또는 `claude-sonnet-5-thinking-high` | 구현자와 세션 분리. 다른 slug가 독립성을 보장하지는 않음 |
| QA | 기존 `qa-*` | 코드 수정 금지 |

실패가 같은 원인으로 2회면 범위를 줄이거나 리뷰 slug를 바꾼다.
환경·Unity 미연결·권한 문제는 모델 교체로 풀지 않는다.

## 기록 위치

- 기능 한 장: `docs/development/tasks/<feature-id>/index.md`
- 위임한 작업만 패킷: `docs/development/tasks/<feature-id>/<task-id>.md`
- 위임하지 않은 부모 구현은 index의 진행 칸에 명령·exit code만 적어도 된다.

## 참고

- 역할 프롬프트: `docs/development/agent-roles.md`
- 패킷 칸: `docs/development/task-packet-template.md`
- 인계: `docs/development/provider-neutral-handoff.md`
- 아키텍처: `docs/architecture.md`
- Unity postflight: `.cursor/rules/unity-verification-postflight.mdc`
- QA: `.cursor/rules/qa-subagent-orchestration.mdc`
