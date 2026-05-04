# GitHub Actions Backend Build Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `.github/workflows/backend-build.yml` 한 파일을 단계적으로 추가해, 백엔드 변경 시 자동으로 (1) pytest, (2) Docker 이미지 빌드, (3) 컨테이너 헬스체크, (4) main 한정 GHCR 푸시까지 수행하는 CI 파이프라인을 만든다.

**Architecture:** 기존 `.github/workflows/ci-check.yml` (lint) 은 그대로 두고 책임이 다른 새 워크플로 1 개를 추가한다. 단일 잡(`build`) 안에서 step 단위로 게이팅하며, 이미지를 한 번 빌드해 로컬 로드 후 헬스체크한 다음 main 일 때만 같은 이미지를 GHCR 로 재태깅·푸시한다. TDD 가 어울리지 않는 대상(YAML 워크플로)이라 *증분 푸시 → Actions 결과 확인 → 다음 step 추가* 방식으로 검증한다.

**Tech Stack:** GitHub Actions, `actions/checkout@v4`, `actions/setup-python@v5`, `docker/setup-buildx-action@v3`, `docker/login-action@v3`, `docker/build-push-action@v6`, GHCR (`ghcr.io`).

**참고 문서:** `docs/superpowers/specs/2026-05-04-github-actions-backend-build-design.md`

---

## File Structure

| 파일 | 동작 |
|---|---|
| `.github/workflows/backend-build.yml` | **신규.** 이 plan 의 산출물. |
| `.github/workflows/ci-check.yml` | 변경 없음. |
| `backend_ai/main.py` | 변경 없음. `:71` 의 `@app.get("/")` 를 헬스체크로 사용. |
| `backend_ai/Dockerfile` | 변경 없음. 그대로 사용. |
| `backend_ai/tests/` | 변경 없음. 단, 외부 LLM 호출이 필요한 테스트가 있으면 Task 2 에서 발견되며 그땐 별개 plan 으로 분리. |

워크플로 파일은 일부러 한 파일만 만든다 (분할 시 main push 마다 파일 3 개가 따로 도는 오버킬).

---

## 작업 진행 원칙 (모든 Task 공통)

- 작업 브랜치는 `ci/backend-build` 같은 별개 브랜치를 만들어 거기서 점진적으로 push 하며 Actions 결과를 확인한다. main 에 직접 커밋 금지 — main 한정 step (Task 5) 은 PR 머지 후에 검증한다.
- 각 Task 는 **한 번 push → Actions 결과 확인 → 통과면 commit 메시지 그대로 두고 다음 Task** 로.
- 실패 시 직전 step 의 로그만 보고 고친다. 더 큰 step 을 묶어서 디버깅하지 않는다.
- 워크플로 안의 모든 step 이름(`name:`)은 `Actions` UI 에서 그대로 보이므로 한국어/영어 섞지 말고 일관되게 영어로 쓴다.

---

## Task 1: 작업 브랜치와 빈 워크플로 스캐폴드 생성

**목적:** 워크플로 파일이 GitHub Actions 에 인식되고, path 필터/동시성/트리거가 의도대로 동작하는지부터 검증.

**Files:**
- Create: `.github/workflows/backend-build.yml`

- [ ] **Step 1: 작업 브랜치 생성**

```bash
git switch -c ci/backend-build
```

- [ ] **Step 2: 최소 스캐폴드 작성**

`.github/workflows/backend-build.yml` 전체 내용:

```yaml
name: Backend Build

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

permissions:
  contents: read
  packages: write

concurrency:
  group: backend-build-${{ github.ref }}
  cancel-in-progress: true

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository_owner }}/newcapstone-ai

jobs:
  build:
    name: Build & verify backend image
    runs-on: ubuntu-latest
    if: |
      !contains(github.event.head_commit.message, '[skip ci]') &&
      !contains(github.event.head_commit.message, '[ci skip]')

    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Print trigger info
        run: |
          echo "ref=${{ github.ref }}"
          echo "event=${{ github.event_name }}"
          echo "sha=${{ github.sha }}"
```

- [ ] **Step 3: path 필터 검증을 위한 의도적 변경 (필수, "관계없는 변경에 안 도는지" 도 같이 본다)**

`backend_ai/` 안 어떤 파일이든 1줄 주석 추가 (예: `backend_ai/README.md` 끝에 빈 줄 1개). 워크플로 파일 자체도 변경됐으니 path 필터에 둘 다 걸려야 한다.

- [ ] **Step 4: 푸시하고 Actions 탭에서 확인**

```bash
git add .github/workflows/backend-build.yml backend_ai/README.md
git commit -m "ci: scaffold backend-build workflow"
git push -u origin ci/backend-build
```

확인 항목:
- GitHub repo → Actions → "Backend Build" 가 새로 나타났는가
- "Print trigger info" step 로그에 `event=push`, 올바른 `ref` 가 찍히는가
- 잡이 **success** 로 끝나는가

- [ ] **Step 5: path 필터 음성(negative) 검증 — 선택이지만 강력 권장**

`README.md` (루트) 같이 `backend_ai/` 바깥 파일만 1줄 변경하고 push. 이 커밋에서는 "Backend Build" 워크플로가 **돌지 않아야** 한다 (Actions UI 에서 새 run 이 안 만들어진다).

```bash
# 루트 README 끝에 빈 줄 1개 추가 후
git add README.md
git commit -m "docs: trivial change (should NOT trigger backend-build)"
git push
```

확인: Actions 탭에 새 Backend Build run 이 생성되지 않았다.

문제 있으면 path 필터부터 고친다.

---

## Task 2: pytest 단계 추가

**목적:** 컨테이너 빌드 전에 Python 단위 테스트 통과를 강제. CI 가 외부 API 에 의존하지 않게 dummy 키 주입.

**Files:**
- Modify: `.github/workflows/backend-build.yml` (steps 추가)

- [ ] **Step 1: pytest 관련 step 4 개를 jobs.build.steps 의 "Print trigger info" 다음에 삽입**

```yaml
      - name: Setup Python
        uses: actions/setup-python@v5
        with:
          python-version: "3.10"
          cache: pip
          cache-dependency-path: backend_ai/requirements.txt

      - name: Install backend dependencies
        working-directory: backend_ai
        run: |
          python -m pip install --upgrade pip
          pip install -r requirements.txt

      - name: Run pytest
        working-directory: backend_ai
        env:
          GROQ_API_KEY: dummy
          GOOGLE_API_KEY: dummy
        run: pytest tests -q
```

- [ ] **Step 2: 커밋 후 푸시**

```bash
git add .github/workflows/backend-build.yml
git commit -m "ci: run backend pytest with dummy keys"
git push
```

- [ ] **Step 3: Actions 결과 확인**

확인 항목:
- "Setup Python" / "Install backend dependencies" / "Run pytest" 3개 step 모두 success
- "Setup Python" 로그에 `Cache restored` 가 두 번째 실행부터 보이는가 (첫 실행은 cache miss 가 정상)
- pytest 가 외부 LLM 호출에서 멈추지 않는가

- [ ] **Step 4: 만약 pytest 가 dummy 키로 실패한다면**

해당 테스트가 실제 LLM 응답에 의존하는 경우다. 이 plan 의 범위 밖이므로:
1. 어떤 테스트가 왜 실패했는지를 정확히 적은 별개 이슈/plan 을 만든다 (mocking 도입 작업).
2. **임시 우회는 하지 않는다.** `--ignore` 나 `pytest.skip` 으로 대충 넘기면 CI 가 본래 막아줄 회귀를 못 막는다.
3. 차단된 채로 사용자에게 보고하고 의사결정을 받는다.

---

## Task 3: Docker 이미지 빌드 단계 추가 (load 만, push 아직 X)

**목적:** Dockerfile 이 실제로 빌드되는지, buildx GHA 캐시가 작동하는지 확인.

**Files:**
- Modify: `.github/workflows/backend-build.yml`

- [ ] **Step 1: pytest step 다음에 buildx + 빌드 step 2 개 추가**

```yaml
      - name: Setup Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Build backend image (load locally)
        uses: docker/build-push-action@v6
        with:
          context: backend_ai
          file: backend_ai/Dockerfile
          load: true
          push: false
          tags: backend-ai:ci
          cache-from: type=gha
          cache-to: type=gha,mode=max
```

- [ ] **Step 2: 커밋 후 푸시**

```bash
git add .github/workflows/backend-build.yml
git commit -m "ci: build backend Docker image with GHA cache"
git push
```

- [ ] **Step 3: Actions 결과 확인**

확인 항목:
- "Build backend image (load locally)" step success
- 첫 실행 후 두 번째 실행에서 빌드 로그에 `CACHED` 라인이 등장
- 이미지가 로컬 daemon 에 로드됐는지는 다음 Task 에서 `docker images` / `docker run` 으로 검증

---

## Task 4: 컨테이너 띄우기 + HTTP 헬스체크 + 정리

**목적:** 빌드된 이미지가 실제로 부팅되고 `GET /` 가 200 을 주는지 검증. 이게 이 워크플로 존재 이유의 핵심.

**Files:**
- Modify: `.github/workflows/backend-build.yml`

- [ ] **Step 1: build step 다음에 헬스체크/로그/정리 step 3 개 추가**

```yaml
      - name: Run container
        run: |
          docker run -d --name api \
            -e GROQ_API_KEY=dummy \
            -e GOOGLE_API_KEY=dummy \
            -p 8000:8000 \
            backend-ai:ci

      - name: Health check (poll up to 30s)
        run: |
          for i in $(seq 1 30); do
            if curl -fsS http://localhost:8000/ > /tmp/health.json; then
              echo "Healthy on attempt $i:"
              cat /tmp/health.json
              exit 0
            fi
            sleep 1
          done
          echo "::error::Health check failed after 30s"
          exit 1

      - name: Dump container logs on failure
        if: failure()
        run: docker logs api || true

      - name: Stop and remove container
        if: always()
        run: docker rm -f api || true
```

- [ ] **Step 2: 커밋 후 푸시**

```bash
git add .github/workflows/backend-build.yml
git commit -m "ci: run container and verify HTTP health"
git push
```

- [ ] **Step 3: Actions 결과 확인**

확인 항목:
- "Health check (poll up to 30s)" success, 로그에 `{"status":"online","message":"Server is Running!"}` 가 찍힘
- "Stop and remove container" 가 항상 마지막에 실행됨 (success 케이스에서도)
- 일부러 헬스체크를 깨뜨려 보고 싶다면(선택): backend_ai 안에서 import 에러를 일으키는 1줄 변경 → 푸시 → "Dump container logs on failure" step 이 발동하고 잡이 fail 하는지 확인 → 변경 되돌리고 다시 푸시. 이 음성 검증을 하면 헬스체크가 진짜로 막아준다는 확신이 생긴다.

---

## Task 5: GHCR 푸시 (main 한정)

**목적:** main 브랜치에 머지된 이미지를 SHA + latest 태그로 GHCR 에 보관. PR/feature 브랜치는 푸시하지 않는다.

**Files:**
- Modify: `.github/workflows/backend-build.yml`

- [ ] **Step 1: 헬스체크/정리 step 다음에 GHCR 푸시 step 2 개 추가**

```yaml
      - name: Log in to GHCR
        if: github.ref == 'refs/heads/main' && github.event_name == 'push'
        uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Tag and push to GHCR
        if: github.ref == 'refs/heads/main' && github.event_name == 'push'
        run: |
          IMAGE_SHA="${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ github.sha }}"
          IMAGE_LATEST="${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:latest"
          docker tag backend-ai:ci "$IMAGE_SHA"
          docker tag backend-ai:ci "$IMAGE_LATEST"
          docker push "$IMAGE_SHA"
          docker push "$IMAGE_LATEST"
          echo "Pushed: $IMAGE_SHA"
          echo "Pushed: $IMAGE_LATEST"
```

설명:
- `github.event_name == 'push'` 조건은 PR 이벤트에서는 main 으로 가는 PR 이라도 토큰 권한이 다르고 이중 푸시가 되므로 제외하기 위한 것.
- `secrets.GITHUB_TOKEN` 은 자동 주입. 별도 시크릿 등록 불필요.

- [ ] **Step 2: feature 브랜치(=현재 ci/backend-build)에서 푸시 step 이 스킵되는지 먼저 검증**

```bash
git add .github/workflows/backend-build.yml
git commit -m "ci: push image to GHCR on main"
git push
```

확인 항목:
- 잡 전체 success
- "Log in to GHCR" / "Tag and push to GHCR" step 두 개가 모두 **skipped** 상태로 표시 (`if:` 조건 때문)

- [ ] **Step 3: PR 생성 → main 머지 → main 의 푸시 이벤트로 GHCR 업로드까지 검증**

```bash
gh pr create --title "ci: backend Docker build & GHCR publish" \
  --body "구현 plan: docs/superpowers/plans/2026-05-04-github-actions-backend-build.md"
```

PR 의 Backend Build 잡이 success 인지 확인 후 머지. 머지 직후 main 에 트리거된 Backend Build run 의:
- "Log in to GHCR" / "Tag and push to GHCR" step 이 이번에는 **executed** 됐는가
- 로그에 `Pushed: ghcr.io/<owner>/newcapstone-ai:<sha>` 와 `:latest` 가 찍혔는가
- 레포 메인 페이지 우측 "Packages" 에 `newcapstone-ai` 가 등장하는가

---

## Task 6: GHCR 패키지 가시성 변경 + 사용 메모

**목적:** 첫 푸시 직후 패키지는 private 이 기본. 다른 환경에서 인증 없이 pull 가능하게 public 으로 바꾸고, 사용 방법을 README/DEPLOY 에 흔적으로 남긴다.

**Files:**
- Modify: `backend_ai/DEPLOY.md` (한 섹션 추가)

- [ ] **Step 1: GHCR 패키지 가시성 public 으로 변경 (코드 작업 아님)**

레포 → Packages → `newcapstone-ai` → Package settings → "Change visibility" → Public.
private 유지를 원할 경우 이 step 을 건너뛰고 pull 하는 환경에 PAT (`read:packages`) 를 따로 설정한다.

- [ ] **Step 2: `backend_ai/DEPLOY.md` 끝에 GHCR 사용 섹션 추가**

```markdown
## 4. GitHub Container Registry 에서 이미지 받기

CI 가 main 에 머지될 때마다 다음 두 태그로 이미지를 푸시합니다:

- `ghcr.io/<owner>/newcapstone-ai:<commit-sha>` — 특정 커밋의 재현 가능한 빌드
- `ghcr.io/<owner>/newcapstone-ai:latest` — main 의 최신 이미지

사용 예:

```bash
docker pull ghcr.io/<owner>/newcapstone-ai:latest
docker run -e GROQ_API_KEY=... -e GOOGLE_API_KEY=... -p 8000:8000 \
  ghcr.io/<owner>/newcapstone-ai:latest
```

`<owner>` 는 이 저장소 owner 의 GitHub 사용자/조직명입니다.
```

(`<owner>` 부분은 작업자가 실제 owner 로 치환해서 커밋해도 좋고, 그대로 두고 일반 가이드로 남겨도 좋다. 일관성만 유지.)

- [ ] **Step 3: 커밋 (별도 PR)**

```bash
git switch main
git pull
git switch -c docs/ghcr-usage
git add backend_ai/DEPLOY.md
git commit -m "docs: GHCR pull/run instructions for backend image"
git push -u origin docs/ghcr-usage
gh pr create --title "docs: GHCR usage" --body ""
```

머지까지 진행.

---

## Task 7: 최종 점검

**목적:** plan 의 의도대로 모든 게 도는지 한 번 끝에서 확인.

- [ ] **Step 1: 동시성 검증**

`ci/backend-build` 를 다시 만들어서 1초 간격으로 두 번 푸시. 첫 번째 run 이 `cancelled` 로 표시되고 두 번째 run 만 끝까지 도는가?

- [ ] **Step 2: `[skip ci]` 검증**

커밋 메시지에 `[skip ci]` 를 넣어 1번 푸시. 잡이 잡 단위 `if:` 조건으로 인해 **skipped** 표시되는가?

- [ ] **Step 3: 최종 path 음성 검증 (한 번 더)**

루트 README 만 1줄 바꿔서 푸시. Backend Build 가 트리거되지 않는가?

- [ ] **Step 4: 캐시 적중 시간 확인**

main 의 최근 두 run 의 총 소요 시간을 비교. 두 번째 run 이 2~3 분 안에 들어오는가? (목표 — 안 들어오면 캐시 키나 step 순서를 점검)

- [ ] **Step 5: 작업 완료 보고**

설계서 + plan 의 모든 항목 체크된 상태로 사용자에게 보고. 다음 단계(예: Render 배포 통합 / Trivy / Unity 빌드) 는 별개 plan.

---

## Self-Review

**1. Spec coverage**
- 설계 §1 범위 → Task 1~5 가 직접 대응.
- 설계 §2 파일 구성 (분리 운영) → Task 1 의 새 파일 + 기존 ci-check.yml 미수정.
- 설계 §3 트리거/동시성/캐시 → Task 1 (트리거/동시성) + Task 2 (pip 캐시) + Task 3 (buildx GHA 캐시).
- 설계 §4 잡 구성 11 step → Task 1~5 누적이 정확히 그 step 시퀀스를 만든다.
- 설계 §5 시크릿 / 환경변수 → Task 1 의 `permissions: packages: write`, Task 2/4 의 dummy 키, Task 5 의 `GITHUB_TOKEN`. 별도 secret 등록 불필요(설계대로).
- 설계 §6 실패 처리 → Task 4 의 `if: failure()` 로그 덤프 + `if: always()` 정리, 잡 단위 `[skip ci]` 가드.
- 설계 §7 동시성 검증 → Task 7 Step 1.
- 설계 §8 러닝 타임 목표 → Task 7 Step 4.
- 설계 §9 미래 확장 → 의도적으로 plan 범위 밖 (메모만).
- 설계 §10 변경 파일 → Task 1 (workflow), Task 6 (DEPLOY.md). README 변경은 path 필터 검증용 임시 변경이므로 plan 범위 안에서 처리.

→ 누락 없음.

**2. Placeholder scan**
- TBD/TODO/"적절히 처리"/"비슷하게" 없음.
- 코드가 필요한 step 은 모두 전체 스니펫 포함.
- "Similar to Task N" 패턴 없음 — Task 5 의 GHCR step 은 코드 풀세트로 적었다.

**3. Type/이름 일관성**
- 이미지 로컬 태그: `backend-ai:ci` — Task 3 (build), Task 4 (run), Task 5 (tag) 동일.
- 컨테이너 이름: `api` — Task 4 (run/logs/rm) 동일.
- 헬스체크 URL: `http://localhost:8000/` — Task 4 동일.
- env 변수명: `REGISTRY`, `IMAGE_NAME` — Task 1 정의 / Task 5 사용. 동일.
- 브랜치 이름: `ci/backend-build` — Task 1 / Task 5 / Task 7 일관.

→ 통과.

---

## 진행 방식

이 plan 은 사용자가 "구현은 하지 말고 계획만" 으로 명시했으므로 실행 위임은 보류한다. 실행을 시작할 때는 별도 지시를 받아 `superpowers:subagent-driven-development` (권장) 또는 `superpowers:executing-plans` 중 선택하여 진행한다.
