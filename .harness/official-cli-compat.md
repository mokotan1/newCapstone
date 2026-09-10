# Official Unity CLI compatibility (Phase C)

조사·설치일: 2026-09-10  
조사 checkout: `C:\Users\user\orca\workspaces\newCapstone\feature-unity-harness`  
활성 backend: `legacy-unity-cli` (아직 전환하지 않음)

## 확인한 실행 파일

| 도구 | 경로 | 버전 |
|---|---|---|
| legacy unity-cli | `%LOCALAPPDATA%\unity-cli\unity-cli.exe` | 0.3.21 |
| official `unity` | `%LOCALAPPDATA%\Unity\bin\unity.exe` | 1.0.0-beta.5 (`unity doctor` pass, auth logged in) |

CLI 자체는 이미 설치되어 있었다. 추가 설치한 것은 프로젝트 패키지다.

## Pipeline 패키지

- 명령: `unity pipeline install --project-path <this>/disputatio --package-version 0.6.0-exp.1 --non-interactive --json`
- 결과: `success: true`, `alreadyInstalled: false`, `com.unity.pipeline` `0.6.0-exp.1`를 이 브랜치 `disputatio/Packages/manifest.json`과 `packages-lock.json`에 기록

## 연결 증거 (이 worktree의 disputatio)

이 프로젝트 경로를 Unity 6000.0.36f1로 연 뒤:

| 항목 | 결과 |
|---|---|
| Pipeline | `0.6.0-exp.1`, 서버 `127.0.0.1:7800` |
| `unity status` | ready |
| Editor | Unity 6000.0.36f1 |
| `unity command editor_status` | 성공 (`compiling: false`, playMode stopped) |
| legacy `unity-cli --project disputatio status` | 같은 인스턴스에서 ready (connector 0.3.21). 상태 조회는 이중 커넥터가 공존 |

## 공식 QA (Phase D 미진입)

`unity command --query qa` → 등록된 `qa_*` 명령 **0개**. Pipeline에 기존 unity-cli QA 도구가 없다. 기본 backend를 바꾸지 않는다.

## Phase C 게이트

| 조건 | 결과 |
|---|---|
| CLI 설치 | 충족. 1.0.0-beta.5 |
| 프로젝트에 Pipeline 선언 | 충족. manifest + lock `0.6.0-exp.1` |
| `unity status` 연결 | 충족. 이 worktree disputatio |
| 공식 `qa_*` | 미충족. 명령 0개 |
| 기본 테스트 / compile 게이트 | 공식 경로로는 아직 완료 판정하지 않음 |

공식 어댑터(`scripts/unity-harness/official.ps1`)는 QA·기본 경로 전환 전까지 `blocked`를 유지한다.
