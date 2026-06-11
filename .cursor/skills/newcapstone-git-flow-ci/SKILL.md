---
name: newcapstone-git-flow-ci
description: >-
  Git 상태 확인, 커밋 준비, PR 준비, CI 실패 대응, 브랜치 전략 판단, 릴리즈 준비 시
  사용. newCapstone의 Git Flow·CI/CD 운영 절차를 따른다.
---

# newCapstone Git Flow · CI/CD

## When to use

다음 작업 **시작 시** 이 스킬을 연다.

- 새 기능·버그 수정 전 **브랜치 선택**
- `git status` / diff 해석, **커밋 전 정리**
- **PR 생성·리뷰** 준비 (`feature` → `develop` 등)
- **CI 실패** 후 원인 분류·수정 순서 결정
- `release/*`, `hotfix/*` **릴리즈·긴급 수정** 절차
- “지금 커밋해도 되나?”, “어느 브랜치에 PR?” 같은 **운영 판단**

코드 구현 상세·폴더 배치는 `newcapstone-architecture`를 먼저 따른다.

---

## Git Flow 기준

```
main          ─── 배포 가능·안정 (일반 기능 직접 커밋 금지)
  ↑ merge       release/*, hotfix/*
develop       ─── 통합 브랜치
  ↑ merge       feature/*
feature/*     ─── 일반 기능 개발 → PR to develop
release/*     ─── QA·버전 고정·릴리즈 노트 → main (+ develop 동기화)
hotfix/*      ─── main에서 분기 긴급 수정 → main + develop
```

| 브랜치 | 분기 기준 | 병합 대상 |
|--------|-----------|-----------|
| `feature/<topic>` | `develop` | `develop` |
| `release/<version>` | `develop` | `main`, then `develop` |
| `hotfix/<topic>` | `main` | `main`, then `develop` |

**커밋 메시지**: 변경 이유 중심. CI 스킵이 필요할 때만 `[skip ci]` / `[ci skip]` (무한 루프·의도적 스킵에 한정).

---

## 현재 Git 상태 판정 절차

에이전트는 **답변·커밋·PR 제안 전** 아래 순서로 판정한다.

### 1. 현재 브랜치 확인

```bash
git branch --show-current
git status -sb
```

### 2. 브랜치 유형 판단

| 패턴 | 유형 | 기대 작업 |
|------|------|-----------|
| `main` | 배포 트랙 | hotfix만; 그 외는 중단·분기 |
| `develop` | 통합 | 소규모 통합 커밋만; 큰 기능은 `feature/*` |
| `feature/*` | 기능 개발 | develop 대상 PR |
| `release/*` | 릴리즈 준비 | QA·버전·문서 |
| `hotfix/*` | 긴급 수정 | main·develop 반영 |

### 3. 변경 파일 존재 여부

- 변경 없음 → 단계 **1 (브랜치 준비)** 또는 대기
- 변경 있음 → **4. 분류**로 진행

### 4. 변경 파일 분류

각 경로를 다음 중 하나로 태깅한다.

| 분류 | 예시 (newCapstone) |
|------|---------------------|
| **소스** | `disputatio/Assets/**/*.cs`, `backend_ai/**/*.py`, `scripts/**/*.cs` |
| **테스트** | `Assets/Editor/Tests/`, `backend_ai/tests/`, `CSharpSyntaxChecker.Tests/` |
| **문서** | `docs/`, `README.md`, `.cursor/rules/`, `.cursor/skills/` |
| **CI/배포** | `.github/workflows/`, `deploy/`, `scripts/collect-errors.py` |
| **자동 생성** | `Library/`, `Temp/`, `Logs/`, `Build/`, `Builds/`, `.vs/`, `**/bin/`, `**/obj/`, `__pycache__/`, `*.AssemblyInfo.cs`(obj 내) |
| **에셋** | `disputatio/Assets/**` (이미지·오디오·프리팹·씬) — `.meta` 쌍 유지 |

### 5. 자동 생성 파일 처리

- 분류 결과 **자동 생성**이 있으면 → **커밋 대상에서 제외** 안내
- 이미 추적 중이면 → `.gitignore` 보강 **제안**(사용자 확인 후 적용), `git rm --cached` 안내
- `scripts/CSharpSyntaxChecker/**/obj/` 등이 status에 보이면 **스테이징하지 말 것**

### 6. 워크플로 단계 매핑

| 조건 | 단계 |
|------|------|
| `feature/*`, 작업 중 | **2. feature 개발 중** |
| 커밋·스테이징 직전 | **3. 커밋 전 정리** |
| 검증 명령 실행 중 | **4. 로컬 검증** |
| `develop`에 큰 기능 직접 수정 | **feature 분리 권장** (단계 1) |
| `main`에 비-hotfix 수정 | **작업 중단·브랜치 이동** 권장 |

전체 단계표는 `.cursor/rules/git-flow-ci-workflow.mdc` §단계표 참고.

---

## 변경 파일 분류 기준

### 커밋해도 되는 것

- 의도한 소스·테스트·문서·CI 설정
- Unity 에셋 + **대응 `.meta`**
- `scripts/CSharpSyntaxChecker/**/*.csproj` (`.gitignore` 예외로 추적)

### 커밋하면 안 되는 것

- `disputatio/Library/`, `Temp/`, `Logs/`, `Build/`, `Builds/`
- `.vs/`, `ExportedObj/`, 로컬 `UserSettings/`
- `**/bin/`, `**/obj/` (빌드 산출물)
- `__pycache__/`, `.pytest_cache/`, `.venv/`, `.env`
- `.cursor/errors/`, 개인 MCP 설정

### 주의

- **`.meta` 삭제 금지** — Unity GUID 깨짐
- **대용량 바이너리** — LFS·에셋 정책 없이 무분별 커밋 금지
- 루트 `.gitignore`의 `/[Oo]bj/`는 **저장소 루트만** 매칭; `scripts/**/obj/`는 별도 패턴 필요(현재 gap — §관련 규칙 참고)

---

## 커밋 전 체크리스트

- [ ] 현재 브랜치가 작업 목적에 맞음 (`feature/*` 등)
- [ ] `git status`에서 자동 생성 파일 **미스테이징**
- [ ] 변경을 소스/테스트/문서로 분류 완료
- [ ] **영역별 로컬 검증** (하나 이상 해당 시)

### 영역별 로컬 검증

| 변경 | 명령·규칙 |
|------|-----------|
| `disputatio/` C#·씬·프리팹 | `unity-verification-postflight` + `newcapstone-unity-automation` |
| `backend_ai/` | `ruff check backend_ai`, 관련 `pytest` |
| C# 전역 구문 | `dotnet test scripts/CSharpSyntaxChecker/CSharpSyntaxChecker.Tests/` |
| `backend_ai/` Docker 관련 | 로컬 빌드 또는 PR 후 `backend-build` CI 확인 |
| `.github/workflows/` | YAML 검토; push 후 워크플로 실행 확인 |

- [ ] 커밋 메시지가 **why** 중심
- [ ] 사용자가 커밋을 **명시 요청**했을 때만 `git commit` (저장소 user rule)

---

## PR 전 체크리스트

- [ ] **base 브랜치**: `feature/*` → `develop` (hotfix/release는 목적에 맞게)
- [ ] `git log develop..HEAD` / `git diff develop...HEAD`로 PR 범위 확인
- [ ] 자동 생성·로컬 설정 파일 **포함 여부** 재확인
- [ ] 테스트 추가/갱신 여부와 **실행 결과** 기록
- [ ] Unity 변경 시 EditMode `--filter` 통과 (postflight)
- [ ] PR 본문: Summary + Test plan
- [ ] CI 실패 없음 (또는 실패 원인·수정 계획 명시)
- [ ] `main` 직접 PR은 hotfix/release 시나리오가 아니면 **거부·재분기** 안내

`gh pr create` 등은 저장소 user rule의 PR 절차를 따른다.

---

## CI/CD 검증 기준

### GitHub Actions (이 저장소)

| 워크플로 | 트리거 | 검증 내용 |
|----------|--------|-----------|
| `ci-check.yml` | push/PR (대부분 브랜치) | `backend_ai` Python 구문·ruff; `disputatio/Assets` C# 구문 |
| `backend-build.yml` | `backend_ai/**` 변경 | Docker 이미지 빌드·검증 |
| `deploy-backend.yml` | 배포 파이프라인 | 운영 배포 (브랜치·시크릿 정책 확인) |

### CI 실패 시

1. Actions 로그 또는 `python scripts/fetch-errors.py`로 오류 수집
2. `.cursor/rules/fix-ci-errors.mdc` 워크플로 참고
3. 수정 후 **재push**; `[skip ci]`는 루프 방지 등 정당한 경우만
4. **테스트 실패를 숨기고 PR하지 않음**

### CI를 대체하지 않는 것

- `CSharpSyntaxChecker`만 통과 → Unity 컴파일·EditMode 대체 **불가**
- 로컬 ruff만 → CI의 전체 `backend_ai` 스캔 대체 **불가**

---

## 브랜치별 권장 행동

### `feature/*`

1. `develop`에서 최신 pull 후 분기
2. 구현 + 테스트 (`architecture-preflight`, Unity postflight)
3. PR → `develop`
4. CI green 후 merge

### `develop`

- 통합·소규모 수정만
- 큰 기능은 `feature/*`로 분리해 PR
- 릴리즈 직전 `release/*` 분기 준비

### `release/*`

- 버전 번호·changelog·QA
- `main` merge 후 태그·배포
- `develop`에 release 변경 백머지

### `hotfix/*`

- `main`에서 분기
- 최소 수정 + CI
- `main` merge → `develop` merge (동일 수정 유지)

### `main`

- 배포 가능 상태 유지
- 일반 기능 커밋 **금지**
- hotfix·release 병합만

---

## 금지 사항

- `main`에 일반 기능 직접 커밋
- `develop`에 큰 기능을 `feature` 없이 직접 커밋
- `obj/`, `Library/`, `Temp/`, `Build/`, `Builds/`, `bin/`, `.vs/` 산출물 커밋
- Unity `.meta` 임의 삭제
- 테스트 실패·CI 실패 상태로 PR 생성·완료 처리
- `.github/workflows/` 변경 후 push/CI 확인 없이 완료
- 대용량 에셋을 LFS·정책 없이 일반 Git에 추가
- 사용자 요청 없이 `git commit` / `git push` / force push

---

## 관련 Cursor Rules

| 규칙 | 역할 |
|------|------|
| `.cursor/rules/git-flow-ci-workflow.mdc` | 항상 적용되는 짧은 Git Flow·CI 요약 |
| `.cursor/rules/architecture-preflight.mdc` | 구현 전 `docs/architecture.md` |
| `.cursor/rules/unity-verification-postflight.mdc` | Unity 변경 후 unity-cli 검증 |
| `.cursor/rules/fix-ci-errors.mdc` | CI 오류 자동 수집·수정 흐름 |
| `.cursor/rules/notion-capstone-spec.mdc` | 기획서 동기화 (커밋·릴리즈 시 선택) |

## 관련 스킬

| 스킬 | 역할 |
|------|------|
| `newcapstone-architecture` | 코드 위치·패턴 |
| `newcapstone-unity-automation` | unity-cli·MCP 검증 |

## `.gitignore` gap (수정 제안만)

다음은 **제안**이며, 본 스킬 작성 시 자동 적용하지 않았다.

1. **`**/obj/`, `**/bin/`** — `scripts/CSharpSyntaxChecker/**/obj/` 등이 현재 추적될 수 있음. 루트 `/[Oo]bj/`만으로는 부족.
2. **`*.AssemblyInfo.cs`, `*.AssemblyInfoInputs.cache`** — obj 내부 자동 생성 (이미 status에 노출된 사례 있음).
3. **Git LFS** — `.gitattributes`에 LFS 규칙 없음. 대용량 에셋 정책 수립 시 `*.psd`, `*.wav` 등 패턴 검토.

사용자 확인 후 `.gitignore` 보강 및 `git rm --cached` 정리를 진행한다.
