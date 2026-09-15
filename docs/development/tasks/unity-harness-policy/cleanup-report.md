# 하네스 작업 파일 정리 보고

날짜: 2026-09-10  
브랜치: `feature/unity-harness-policy`  
판단 기준: 완료 기준에 쓰이지 않음, Editor가 자동으로 더럽힘, 저장소 루트에 잘못 생긴 Unity 프로젝트, 테스트 임시물이 커밋에 섞임.

이 보고는 지운 것과 **남긴 것**을 같이 적는다.

## 1. Git에서 뺀 것 (추적 중이던 쓰레기)

| 경로 | 이유 |
|---|---|
| `.pytest_tmp/` (39파일) | pytest `--basetemp` 산출물. CI가 런타임에 다시 만든다. 소스 아님 |
| `.superpowers/` (10파일) | 로컬 브레인스토밍 산출물. `.gitignore`에 이미 있는데 예전에 커밋됨 |
| `_live_gettext.txt` | 주간 보고 덤프. 코드·문서 진입점이 없음 |
| `unity-dialogue-log-final-editmode-results-2.xml` | EditMode NUnit 결과 XML. 재생성 가능 |

`.gitignore`에 `.pytest_tmp/`, 루트 `/Assets/`, `/Packages/`, `/ProjectSettings/`를 추가했다. 실제 Unity 프로젝트는 `disputatio/`만 쓴다.

## 2. 커밋에서 뺀 것 (이 worktree Editor 부산물)

워킹 카피를 되돌렸다. 하네스 변경이 아니다.

| 경로 | 이유 |
|---|---|
| `disputatio/Assets/Fungus/EditorResources/FungusEditorResources.asset` | Fungus 에디터 리소스 |
| `disputatio/Assets/Fungus/EditorResources/FungusEditorResources.asset.meta` | 위와 쌍 |
| `disputatio/ProjectSettings/EditorBuildSettings.asset` | 빌드 씬 목록 |
| `disputatio/ProjectSettings/ProjectSettings.asset` | 플레이어 설정 |

## 3. 원래 checkout 디스크에서만 지운 것

경로: `C:\Users\user\orca\workspaces\newCapstone\develop`  
Unity가 **저장소 루트**를 프로젝트로 열어서 생긴 복제본이다. `disputatio/` 쪽은 건드리지 않았다.

| 경로 | 이유 |
|---|---|
| `Assets/` (루트) | 잘못된 Unity 프로젝트 |
| `Library/` (루트) | 루트 프로젝트 캐시. 용량만 큼 |
| `Logs/` (루트) | 루트 프로젝트 로그 |
| `Packages/` (루트) | `disputatio/Packages`의 잘못된 복제 |
| `ProjectSettings/` (루트) | `disputatio/ProjectSettings`의 잘못된 복제 |
| `UserSettings/` (루트) | 로컬 에디터 레이아웃 |

`scripts/CSharpSyntaxChecker/bin/`은 커밋하지 않았다. 로컬 빌드 잔여물이다.

## 4. 지우지 않은 것

| 경로 | 이유 |
|---|---|
| `.harness/unity-policy.md` 등 하네스 정책 | 설계 진입점 |
| `scripts/unity-harness*` / `scripts/unity_harness/` | 어댑터·계약 테스트 |
| `docs/superpowers/specs/2026-09-10-unity-harness-design.md` | 설계 원문 |
| `report.pdf`, `기획서/`, `시나리오/`, `QA문서/` | 위키 RAG·기획 원문 |
| `disputatio/Packages/manifest.json` + lock | Pipeline `0.6.0-exp.1` 선언. 이번 작업 산물 |

## 5. 문서로만 고친 것

- `docs/architecture.md` §8: Pipeline lock·이 worktree에서 `unity status` ready인 현재 사실로 갱신
- `.harness/official-cli-compat.md`: 연결 증거와 공식 `qa_*` 0개 기록
