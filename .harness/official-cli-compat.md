# Official Unity CLI compatibility (Phase C)

조사일: 2026-09-10  
조사 checkout: `C:\Users\user\orca\workspaces\newCapstone\feature-unity-harness`  
활성 backend: `legacy-unity-cli` (전환하지 않음)

## 확인한 실행 파일

| 도구 | 경로 | 버전 |
|---|---|---|
| legacy unity-cli | `%LOCALAPPDATA%\unity-cli\unity-cli.exe` | 0.3.21 |
| official `unity` | `%LOCALAPPDATA%\Unity\bin\unity.exe` | 1.0.0-beta.5 |

## 공식 CLI help에서 확인한 관련 명령

- `unity status` — Pipeline 패키지가 있는 Editor 인스턴스만 보고. 이 조사에서 `--json` 결과: `STATUS_NO_INSTANCES` ("No Unity Editor instances found with the Pipeline package installed").
- `unity test` — EditMode/PlayMode, `--output` NUnit XML(기본 `test-results.xml`), `--filter`, `--timeout`.
- `unity command` — 연결된 Editor의 Pipeline 등록 명령 실행.
- `unity pipeline install` — 프로젝트에 Pipeline 패키지 설치. **이 checkout의 `disputatio`에는 실행하지 않음.** 설계는 동시 설치를 전제로 두지 않는다.

## Phase C 진입 조건 판정

| 조건 | 결과 |
|---|---|
| 6000.0.36f1에서 공식 경로 compile | 미실행. Pipeline 미설치 + 이 브랜치에 패키지 mutation 금지 |
| 연결 | 실패. Pipeline 없는 인스턴스는 status에 안 잡힘 |
| 기본 테스트 | 미실행. 연결 실패와 동일 차단 |
| 사용자 정의 명령 API | help상 `unity command` / `list`는 Pipeline 명령. 기존 `[UnityCliTool]` QA와 동등한 공식 API는 이 프로젝트에서 미확인 |

실패 시 결정(설계 §13): Editor 업그레이드·Pipeline 자동 설치를 하지 않고 **기존 경로 유지**. Phase D(공식 QA 이식), E(동등성), F(기본 경로 전환)에 진입하지 않는다.

공식 어댑터(`scripts/unity-harness/official.ps1`)는 `blocked` / `version-mismatch`를 유지한다.
