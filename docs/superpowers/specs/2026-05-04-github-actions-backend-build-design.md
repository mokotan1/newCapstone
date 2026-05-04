# GitHub Actions: 백엔드 빌드 파이프라인 설계

- 작성일: 2026-05-04
- 대상 디렉터리: `backend_ai/`
- 기존 워크플로: `.github/workflows/ci-check.yml` (Python lint + C# 구문 체크) — **유지**
- 신규 워크플로: `.github/workflows/backend-build.yml` (이 문서의 대상)

## 1. 목적과 범위

기존 `ci-check.yml` 은 *코드가 깨졌나* 만 본다 (py_compile / Ruff / Roslyn 구문 체크).
"이 코드로 만든 컨테이너가 실제로 부팅되고 응답하는가" 는 검증되지 않는다.
이 워크플로는 그 공백을 메운다.

**범위 안**
- `backend_ai/` Python FastAPI 코드 단위 테스트 (`pytest`)
- `backend_ai/Dockerfile` 로 이미지 빌드 — buildx + GitHub Actions 캐시
- 빌드한 이미지를 컨테이너로 띄워 HTTP 헬스체크
- `main` 브랜치에 한해 GHCR 푸시 (`ghcr.io/<owner>/newcapstone-ai:<sha>` + `:latest`)

**범위 밖**
- Render 자동 배포 — Render 가 GitHub push 를 직접 감지하므로 Actions 가 추가 트리거를 걸지 않는다 (이중 빌드 회피).
- Unity(`disputatio/`) 빌드 — 라이선스/시간 비용이 커서 별개 워크플로로 추후.
- 이미지 보안 스캔(Trivy 등) — 추후 옵션.
- 외부 LLM API 실호출 테스트 — CI 는 dummy 키로만, 실호출은 로컬/스테이징.

## 2. 파일 구성

```
.github/workflows/
├── ci-check.yml        (기존, 손대지 않음)
└── backend-build.yml   (신규)
```

**왜 분리했나**
- 두 파일의 트리거 path 가 다르다. `ci-check` 는 전체 레포, `backend-build` 는 `backend_ai/**`.
- path 필터는 워크플로 단위라 한 파일에 합치면 job 게이팅이 지저분해진다.
- 실패 원인 분리: lint 실패와 빌드 실패가 GitHub UI 에서 별도 워크플로로 보인다.

## 3. 트리거 / 동시성 / 캐시

```yaml
on:
  push:
    branches: ["**"]
    paths:
      - "backend_ai/**"
      - ".github/workflows/backend-build.yml"
  pull_request:
    paths:
      - "backend_ai/**"
      - ".github/workflows/backend-build.yml"

concurrency:
  group: backend-build-${{ github.ref }}
  cancel-in-progress: true
```

- **path 필터**: Unity/문서/시나리오만 바꾼 커밋에서는 안 돈다 → 시간·쿼터 절약.
- **동시성 그룹**: 같은 브랜치에 새 커밋이 들어오면 진행 중 빌드 취소.
- **캐시 2종**:
  - `actions/setup-python` 의 `cache: pip` (requirements 해시 기반)
  - Docker buildx 의 `cache-from: type=gha`, `cache-to: type=gha,mode=max`

## 4. 잡 구성 (단일 잡 `build`)

```
runs-on: ubuntu-latest
permissions:
  contents: read
  packages: write     # GHCR 푸시 (main 한정 step에서만 사용)

steps:
  1. checkout
  2. setup-python 3.10  (+ cache: pip)
  3. pip install -r backend_ai/requirements.txt
  4. pytest backend_ai/tests
        env: GROQ_API_KEY=dummy, GOOGLE_API_KEY=dummy
  5. setup-buildx
  6. docker buildx build
        context: backend_ai
        cache-from/to: type=gha
        load: true
        tag: backend-ai:ci
  7. docker run -d --name api
        -e GROQ_API_KEY=dummy -e GOOGLE_API_KEY=dummy
        -p 8000:8000 backend-ai:ci
  8. 헬스체크: curl -f http://localhost:8000/  (최대 30초 폴링)
  9. (if: failure()) docker logs api   ← 부팅 실패 원인 노출
 10. (if: always())  docker rm -f api
 11. [main 한정] login-ghcr (GITHUB_TOKEN)
 12. [main 한정] docker tag + push
        ghcr.io/${{ github.repository_owner }}/newcapstone-ai:${{ github.sha }}
        ghcr.io/${{ github.repository_owner }}/newcapstone-ai:latest
```

**핵심 결정**
- pytest 는 **Python 환경에서 직접** 실행 (Docker 안에서 안 돌림 → 빠르고 의존성 단순).
- 이미지는 **한 번만 빌드** 하고 `load: true` 로 로컬에 올린다. main 일 때 같은 이미지를 재태깅·push.
- 헬스체크는 **실제로 띄워서 HTTP 호출**. `requirements` 충돌 / `uvicorn` 시작 실패 / import 시점 에러는 빌드만으로는 안 잡힌다.
- 헬스체크 엔드포인트는 `main.py:71` 의 `@app.get("/")` 사용 — 이미 `{"status":"online"}` 반환하는 health_check 가 존재.
- `chat_service is None` 분기 덕에 키 없이도 부팅됨 → 코드 수정 불필요.

## 5. 시크릿 / 환경변수

**Repo Secrets 추가**: 없음.

GHCR 푸시는 워크플로에 자동 주입되는 `GITHUB_TOKEN` 으로 충분.
`jobs.permissions.packages: write` 만 명시하면 된다.

| 단계 | 변수 | 값 |
|---|---|---|
| pytest | `GROQ_API_KEY`, `GOOGLE_API_KEY` | `"dummy"` |
| docker run | 동일 | `"dummy"` |
| GHCR push (main only) | `REGISTRY=ghcr.io`, `IMAGE_NAME=${{ github.repository_owner }}/newcapstone-ai` | (워크플로 env) |

**LLM 실호출 정책**: CI 는 외부 API 에 의존하지 않는다. 외부 호출하는 테스트는 코드에서 mock 또는 skip. 현재 `backend_ai/tests/` 가 dummy 키로 통과한다고 가정 — 실패 시 별개 작업으로 분리.

**GHCR 가시성**: 첫 푸시 후 GitHub UI 에서 패키지를 **public** 으로 변경 (다른 환경에서 인증 없이 pull). private 유지 시 pull 쪽에 PAT 필요.

## 6. 실패 처리 / 관측

- step 선형 게이팅: pytest 실패 → 빌드 안 함, 빌드 실패 → 헬스체크 안 함, 헬스체크 실패 → push 안 함.
- 헬스체크 실패 시 `if: failure()` 로 `docker logs api` 출력 — 부팅 실패 원인 즉시 확인.
- 컨테이너 정리는 `if: always()` — 디스크/포트 누수 방지.
- 커밋 메시지에 `[skip ci]` / `[ci skip]` 포함 시 잡 스킵 (기존 `ci-check.yml` 과 동일 패턴).

## 7. 두 워크플로의 main 동시 실행

`ci-check` 와 `backend-build` 가 main push 에서 같이 돈다 — 의도된 동작.
`needs:` 로 묶지 않는다 (병렬이 더 빠르고, 결과가 분리되어 보임).

## 8. 예상 러닝 타임 (캐시 적중 후)

| 단계 | 시간 |
|---|---|
| pytest | 30~60s |
| Docker 빌드 (cache hit) | ~30s |
| 헬스체크 (폴링) | 5~15s |
| GHCR push | 10~30s |
| **총합** | **2~3분 목표** |

## 9. 미래 확장 (지금은 하지 않음)

- Unity 빌드 워크플로 (game-ci/unity-builder).
- 이미지 보안 스캔 (Trivy / docker scout).
- pytest 가 실제 LLM 호출을 요구하게 되면 → mock 레이어 도입 또는 별도 staging 워크플로.
- Render 자동 배포 외에 다른 호스팅 추가 시 — 그때 `backend-publish.yml` 분리 검토.

## 10. 변경되는 파일

| 파일 | 변경 |
|---|---|
| `.github/workflows/backend-build.yml` | 신규 |
| `.github/workflows/ci-check.yml` | 변경 없음 |
| `backend_ai/**` | 변경 없음 |
| `docs/superpowers/specs/2026-05-04-github-actions-backend-build-design.md` | 신규 (이 문서) |
