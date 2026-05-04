# 백엔드 배포(GHCR → 실행 환경) 실행 계획

> **For agentic workers:** 이 plan 은 사용자 회신 5 변수(설계서 §0)가 *기본 가정값* 으로 잠긴 상태에서 작성됐다. 회신이 오면 본 문서를 갱신·재실행한다. 실행 시에는 `superpowers:subagent-driven-development` (권장) 또는 `superpowers:executing-plans` 사용.

**Goal:** GHCR 에 올라간 `ghcr.io/mokotan1/newcapstone-ai:<sha>` 이미지를 Ubuntu VM 에 main 머지 자동(+ 수동 버튼) 배포하고, HTTPS 도메인으로 노출하며, 실패 시 이전 SHA 로 롤백할 수 있는 상태를 만든다.

**선행 자료:**
- 설계서: `docs/superpowers/specs/2026-05-04-backend-deploy-design.md`
- 빌드 plan: `docs/superpowers/plans/2026-05-04-github-actions-backend-build.md` (GHCR 까지)
- 운영 가이드: `backend_ai/DEPLOY.md` (pull/run 예시)

**기본 가정 (사용자 회신 전):** 클라우드 VM(Ubuntu 22.04) / 도메인 + HTTPS 사용 / GHCR public / 반자동(자동 + 수동 버튼) / 1 대 배포.

**Tech Stack:** GitHub Actions, SSH, Docker Compose v2, Caddy(자동 HTTPS), GHCR.

---

## 사용자 회신이 필요한 5 질문 (재게시)

설계서 §0 와 동일. 회신 전이라도 본 plan 은 기본 가정값으로 진행 가능하지만, 회신을 받는 즉시 영향 Task 를 표시했다.

| # | 질문 | 영향 받는 Task |
|---|---|---|
| 1 | 타깃 환경 (VM / 집 PC / PaaS …) | 거의 전체. PaaS 답이면 Task 1~6 → Appendix C 로 갈음 |
| 2 | 도메인·HTTPS 필요 여부 | Task 4 (Caddy), Task 5 (외부 헬스), Task 7 (DNS) |
| 3 | GHCR 가시성 | Task 2 (서버 docker login 추가 여부) |
| 4 | 자동화 수준 | Task 6 (트리거: 자동 / 수동 / 둘 다) |
| 5 | 배포 대상 PC 수 | Task 3 (compose 파일 다중 호스트 복제 여부) |

---

## File Structure

| 파일 | 역할 |
|---|---|
| `.github/workflows/deploy-backend.yml` | 신규. main 자동 + workflow_dispatch 수동. SSH 로 서버에 한 줄 명령 |
| `deploy/docker-compose.prod.yml` | 신규. 운영용 compose. 이미지 태그를 `${IMAGE_TAG}` 로 받음 |
| `deploy/Caddyfile` | 신규. 도메인 → backend:8000 역프록시 |
| `deploy/scripts/postdeploy_healthcheck.sh` | 신규. 컨테이너 readiness + 내부/외부 curl |
| `deploy/scripts/rollback.sh` | 신규. `.last_good_sha` 로 즉시 되돌리기 |
| `backend_ai/DEPLOY.md` | 변경. "VM 운영 환경" 섹션 추가, 트러블슈팅 |
| `/opt/newcapstone/.env` | **서버 전용**. 절대 git 에 들어가지 않음 |
| `/opt/newcapstone/docker-compose.prod.yml` | 서버에 SCP 또는 `git clone --depth=1` 으로 복사된 사본 |

---

## 작업 진행 원칙

- 모든 변경은 PR 로. main 직접 push 금지.
- 첫 자동 배포가 도는 시점에는 사용자가 클라우드 콘솔 / 물리 접근으로 서버를 즉시 잡을 수 있어야 한다 (잠금 사고 대비).
- Task 마다 끝에 *검증 항목* 이 있다. 통과 못 하면 다음 Task 로 안 넘어간다.
- "코드 수정 없이 문서 형태 플랜만" 이라는 사용자 지시는 *이번 턴* 한정. 본 plan 자체는 향후 실제 실행될 작업을 정의한다.

---

## Task 0: 운영 환경 결정 확정

**목적:** 설계서 §0 의 5 변수 회신을 받아 본 plan 의 분기를 잠근다.

**Files:** (없음 — 의사결정 단계)

- [ ] **Step 1: 사용자에게 5 질문 회신 요청**

설계서 §0 표 그대로 메시지로 보낸다. 회신 보관 위치는 PR 설명 또는 `docs/superpowers/specs/2026-05-04-backend-deploy-design.md` §0 표를 직접 갱신.

- [ ] **Step 2: 회신에 따라 분기 결정**

- 1번 답이 PaaS(Render/Fly/Railway) 면 → 본 plan 은 Appendix C 로 단축. Task 1~6 스킵, Task 7 만 진행.
- 1번 답이 VM 이면 → 그대로 진행.
- 2번 = 도메인 없음 → Task 4 의 Caddyfile 을 nip.io 도메인 또는 평문 :80 으로 변경.
- 3번 = private → Task 2 에 "서버 docker login" Step 추가.
- 4번 = 완전 자동만 → Task 6 의 `workflow_dispatch` 제거. 문서만 → Task 6 자체를 "수동 SSH 절차서" 로 대체.
- 5번 = 다수 → Task 3 의 compose 파일을 그대로 다른 호스트에 복제하는 추가 절차 메모.

- [ ] **Step 3: 운영 진리 한 줄 명문화**

`backend_ai/DEPLOY.md` 상단에 "운영 환경의 권위 = <결정된 환경>" 한 줄 추가. Render 와 VM 이 둘 다 도는 사고 방지.

---

## Task 1: VM 사전 준비

**목적:** 사람이 한 번만 손으로 하는 셋업. 이후 배포는 다 자동.

**Files:** (서버 측 작업, 레포 변경 없음)

- [ ] **Step 1: VM 생성**

Ubuntu 22.04 LTS, 1~2 vCPU / 2GB+ RAM / 20GB 디스크. 보안그룹: 22(자기 IP만), 80, 443.

- [ ] **Step 2: 비루트 배포 계정 생성**

```bash
sudo adduser --disabled-password --gecos "" deploy
sudo usermod -aG docker deploy
sudo mkdir -p /home/deploy/.ssh && sudo chmod 700 /home/deploy/.ssh
# Actions 가 쓸 public key 를 authorized_keys 에 추가
sudo tee /home/deploy/.ssh/authorized_keys < deploy_key.pub > /dev/null
sudo chown -R deploy:deploy /home/deploy/.ssh
sudo chmod 600 /home/deploy/.ssh/authorized_keys
```

- [ ] **Step 3: Docker / Compose 설치**

```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker deploy   # 이미 했어도 무해
docker compose version           # v2 인지 확인
```

- [ ] **Step 4: 작업 디렉터리**

```bash
sudo mkdir -p /opt/newcapstone
sudo chown deploy:deploy /opt/newcapstone
```

**검증:** `ssh deploy@<host> 'docker ps'` 가 권한 에러 없이 빈 표를 반환.

---

## Task 2: 서버 시크릿 / 환경변수 배치

**목적:** LLM 키를 *서버에서만* 보관. GitHub 에는 들어가지 않음.

**Files:**
- 서버: `/opt/newcapstone/.env` (신규, 레포 외부)

- [ ] **Step 1: `.env` 작성 (서버에서 직접 입력)**

```bash
ssh deploy@<host>
cat > /opt/newcapstone/.env <<'EOF'
GROQ_API_KEY=<실제 값>
GOOGLE_API_KEY=<실제 값>
IMAGE_TAG=latest
EOF
chmod 600 /opt/newcapstone/.env
```

- [ ] **Step 2: (질문 3 = private 인 경우만) GHCR 로그인**

PAT 발급 (`read:packages` 스코프) → 서버에서 1 회:

```bash
echo $GHCR_PAT | docker login ghcr.io -u mokotan1 --password-stdin
```

PAT 는 서버 사용자 홈에 저장되며 `.env` 에는 두지 않는다.

**검증:** `docker pull ghcr.io/mokotan1/newcapstone-ai:latest` 가 인증 에러 없이 성공.

---

## Task 3: 운영 compose 파일 작성

**Files:**
- Create: `deploy/docker-compose.prod.yml`

- [ ] **Step 1: 파일 작성**

```yaml
services:
  backend:
    image: ghcr.io/mokotan1/newcapstone-ai:${IMAGE_TAG}
    container_name: newcapstone-backend
    restart: unless-stopped
    env_file:
      - .env
    expose:
      - "8000"
    healthcheck:
      test: ["CMD", "curl", "-fsS", "http://localhost:8000/"]
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 20s
    networks: [web]

  caddy:
    image: caddy:2
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config
    networks: [web]
    depends_on:
      - backend

volumes:
  caddy_data:
  caddy_config:

networks:
  web:
    driver: bridge
```

설계 노트:
- `image` 가 `${IMAGE_TAG}` — `.env` 의 값으로 잠긴다. 롤백은 이 값만 바꾸면 끝.
- `expose` 만 두고 `ports` 는 caddy 만 — 8000 은 외부 노출 안 함.
- compose v2 의 `healthcheck` 사용. `curl` 이 슬림 이미지에 없으면 Dockerfile 에 `apk add --no-cache curl` 또는 `python -c "import urllib.request,sys;urllib.request.urlopen('http://localhost:8000/').read()"` 로 치환.

- [ ] **Step 2: 서버에 배치**

```bash
scp deploy/docker-compose.prod.yml deploy@<host>:/opt/newcapstone/docker-compose.yml
```

(서버에서는 파일명 `docker-compose.yml` 로 둬서 `docker compose` 가 자동으로 찾게.)

**검증:** 서버에서 `cd /opt/newcapstone && docker compose config` 가 에러 없이 파싱.

---

## Task 4: Caddy 역프록시 + HTTPS

**Files:**
- Create: `deploy/Caddyfile`

- [ ] **Step 1: Caddyfile 작성 (도메인 있는 경우)**

```
api.example.com {
    reverse_proxy backend:8000
    encode zstd gzip
    log {
        output file /var/log/caddy/access.log
    }
}
```

`api.example.com` 부분을 실제 도메인으로 치환. DNS A 레코드를 VM IP 로 미리 맞춰야 인증서 발급 성공.

- [ ] **Step 2: 도메인 없는 경우 (질문 2 = 아니오)**

```
:80 {
    reverse_proxy backend:8000
}
```

또는 nip.io 임시 도메인:

```
<vm-ip>.nip.io {
    reverse_proxy backend:8000
}
```

- [ ] **Step 3: 서버에 배치**

```bash
scp deploy/Caddyfile deploy@<host>:/opt/newcapstone/Caddyfile
```

**검증:** Task 5 의 첫 부팅 후 `curl -fsS https://<도메인>/` 가 `{"status":"online"...}` 반환.

---

## Task 5: 첫 수동 배포 (자동 트리거 켜기 전)

**목적:** 자동화 켜기 전에 사람 손으로 한 번 띄워서 모든 게 돌아가는 상태를 만든다.

**Files:** (서버 측 작업)

- [ ] **Step 1: 첫 부팅**

```bash
ssh deploy@<host>
cd /opt/newcapstone
sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=$(curl -s https://api.github.com/repos/mokotan1/newCapstone/commits/main | jq -r .sha)/" .env
docker compose pull
docker compose up -d
```

- [ ] **Step 2: 컨테이너 healthcheck 통과 확인**

```bash
docker compose ps   # backend healthy 가 될 때까지 대기 (~20s)
```

- [ ] **Step 3: 내부 헬스**

```bash
curl -fsS http://localhost:8000/
# {"status":"online","message":"Server is Running!"}
```

- [ ] **Step 4: 외부 헬스**

```bash
curl -fsS https://<도메인>/
```

같은 응답이 나와야 함. 인증서 발급 첫 시도가 1~30 초 걸릴 수 있음.

- [ ] **Step 5: `.last_good_sha` 기록**

```bash
echo "$(grep ^IMAGE_TAG .env | cut -d= -f2)" > /opt/newcapstone/.last_good_sha
```

**검증:** 외부 도메인 https 요청 200, `docker compose ps` 가 둘 다 healthy/running.

---

## Task 6: GitHub Actions 자동 배포 워크플로

**Files:**
- Create: `.github/workflows/deploy-backend.yml`
- Create: `deploy/scripts/postdeploy_healthcheck.sh`

- [ ] **Step 1: Repo Secrets 등록**

`Settings → Secrets and variables → Actions` 에:
- `DEPLOY_HOST` — VM 주소(IP 또는 도메인)
- `DEPLOY_USER` — `deploy`
- `DEPLOY_SSH_KEY` — Task 1 에서 만든 키쌍의 **private** key (PEM 전체)
- `DEPLOY_PORT` — 22 가 아니면
- (질문 2 = 도메인 있음 시) `DEPLOY_HEALTHCHECK_URL` — `https://api.example.com/`

- [ ] **Step 2: 워크플로 파일 작성**

```yaml
name: Deploy backend

on:
  workflow_run:
    workflows: ["Backend Build"]
    types: [completed]
    branches: [main]
  workflow_dispatch:
    inputs:
      sha:
        description: "Image SHA to deploy (default: triggering commit)"
        required: false

permissions:
  contents: read

concurrency:
  group: deploy-backend
  cancel-in-progress: false   # 배포는 줄을 세움. 중간 취소가 더 위험

jobs:
  deploy:
    if: |
      github.event_name == 'workflow_dispatch' ||
      (github.event.workflow_run.conclusion == 'success')
    runs-on: ubuntu-latest
    steps:
      - name: Checkout (for scripts)
        uses: actions/checkout@v4

      - name: Resolve target SHA
        id: sha
        run: |
          if [ "${{ github.event_name }}" = "workflow_dispatch" ] && [ -n "${{ inputs.sha }}" ]; then
            echo "value=${{ inputs.sha }}" >> "$GITHUB_OUTPUT"
          else
            echo "value=${{ github.event.workflow_run.head_sha || github.sha }}" >> "$GITHUB_OUTPUT"
          fi

      - name: SSH deploy
        uses: appleboy/ssh-action@v1
        with:
          host: ${{ secrets.DEPLOY_HOST }}
          username: ${{ secrets.DEPLOY_USER }}
          key: ${{ secrets.DEPLOY_SSH_KEY }}
          port: ${{ secrets.DEPLOY_PORT || 22 }}
          script: |
            set -euo pipefail
            cd /opt/newcapstone

            OLD_SHA=$(grep ^IMAGE_TAG .env | cut -d= -f2 || echo latest)
            NEW_SHA=${{ steps.sha.outputs.value }}
            echo "Deploying $OLD_SHA -> $NEW_SHA"

            sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=$NEW_SHA/" .env
            docker compose pull
            docker compose up -d --remove-orphans

            ./scripts/postdeploy_healthcheck.sh || {
              echo "::error::Health check failed, rolling back to $OLD_SHA"
              sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=$OLD_SHA/" .env
              docker compose up -d
              exit 1
            }

            echo "$NEW_SHA" > .last_good_sha
            echo "Deploy OK: $NEW_SHA"
```

설명:
- `workflow_run` 으로 build 워크플로 성공 후에만 자동 트리거. PR 이벤트는 안 받는다 (시크릿 노출 방지).
- `workflow_dispatch` 로 사용자가 임의 SHA 를 골라 재배포 가능.
- `concurrency.cancel-in-progress: false` — build 와 달리 배포는 줄세움이 안전.

- [ ] **Step 3: 헬스체크 스크립트 작성**

`deploy/scripts/postdeploy_healthcheck.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

# 1) 컨테이너 readiness — compose healthcheck 가 healthy 될 때까지 60초 대기
for i in $(seq 1 60); do
  STATUS=$(docker inspect -f '{{.State.Health.Status}}' newcapstone-backend 2>/dev/null || echo starting)
  if [ "$STATUS" = "healthy" ]; then
    break
  fi
  if [ "$i" = "60" ]; then
    echo "Container did not become healthy in 60s (last: $STATUS)"
    docker compose logs --tail=200 backend
    exit 1
  fi
  sleep 1
done

# 2) 내부 헬스
curl -fsS http://localhost:8000/ > /dev/null

# 3) 외부 헬스 (도메인 있는 경우만 — 환경변수로 제어)
if [ -n "${DEPLOY_HEALTHCHECK_URL:-}" ]; then
  curl -fsS "$DEPLOY_HEALTHCHECK_URL" > /dev/null
fi

echo "Health check passed"
```

`chmod +x deploy/scripts/postdeploy_healthcheck.sh` 후 서버 `/opt/newcapstone/scripts/` 에도 SCP. (또는 서버에서 git clone 으로 동기화 — Task 8 에서 정리.)

- [ ] **Step 4: 수동 한 번 검증**

`Actions → Deploy backend → Run workflow` 로 main HEAD SHA 한 번 배포. 

Actions 로그에서 `Deploy OK: <sha>` 가 보이는가? 외부 도메인이 정상 응답하는가?

- [ ] **Step 5: 자동 트리거 검증**

backend_ai 안 1 줄 의도된 변경 → PR → main 머지 → Backend Build 통과 → Deploy backend 가 자동으로 도는가? `.last_good_sha` 가 새 SHA 로 갱신됐는가?

---

## Task 7: 롤백 스크립트와 절차서

**Files:**
- Create: `deploy/scripts/rollback.sh`
- Modify: `backend_ai/DEPLOY.md`

- [ ] **Step 1: 롤백 스크립트**

```bash
#!/usr/bin/env bash
set -euo pipefail
cd /opt/newcapstone

TARGET=${1:-$(cat .last_good_sha 2>/dev/null || echo latest)}
echo "Rolling back to $TARGET"

sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=$TARGET/" .env
docker compose pull
docker compose up -d
./scripts/postdeploy_healthcheck.sh
echo "Rollback OK: $TARGET"
```

서버에서 한 줄로 `./scripts/rollback.sh <sha>` 또는 인자 없이 직전 정상 SHA 로.

- [ ] **Step 2: DEPLOY.md 보강**

VM 운영 섹션에 다음 절차를 추가:
1. 자동 배포 — 머지 후 Actions 가 알아서.
2. 수동 재배포 — `Actions → Deploy backend → Run workflow`.
3. 롤백 — Actions 에서 직전 SHA 입력 후 dispatch, 또는 SSH 후 `rollback.sh`.
4. 키 회전 — 서버 `.env` 만 수정 후 `docker compose up -d backend`. 재배포 불필요.

- [ ] **Step 3: 가짜 실패 리허설**

일부러 `IMAGE_TAG` 를 존재하지 않는 SHA 로 입력 → workflow_dispatch → 헬스체크 실패 → 자동 롤백 동작 확인. 실패가 진짜로 막히는지 본 후 정상 SHA 로 다시 한 번.

---

## Task 8: 모니터링 / 알림 (선택)

**Files:**
- (선택) Modify: `.github/workflows/deploy-backend.yml`

- [ ] **Step 1: 알림 채널 결정**

Discord / Slack / 이메일 중 택 1. 결정 안 하면 이 Task 스킵.

- [ ] **Step 2: webhook URL 을 secret 으로**

`DEPLOY_WEBHOOK_URL` 등록.

- [ ] **Step 3: 워크플로 끝에 알림 step 추가**

```yaml
      - name: Notify
        if: always()
        run: |
          STATUS="${{ job.status }}"
          curl -fsS -X POST -H "Content-Type: application/json" \
            -d "{\"content\":\"deploy $STATUS: ${{ steps.sha.outputs.value }}\"}" \
            "${{ secrets.DEPLOY_WEBHOOK_URL }}"
```

- [ ] **Step 4: 평시 로그 위치 문서화**

DEPLOY.md 에:
- `docker compose logs -f --tail=200 backend` — 앱 로그
- `/var/log/caddy/access.log` — HTTP 요청 로그

---

## Task 9: 문서 정리 / PR

- [ ] **Step 1: backend_ai/DEPLOY.md 최종 정리**

§4 GHCR 섹션 다음에 §5 "VM 운영" 추가, 자동 배포 / 수동 / 롤백 / 키 회전 절차 명문화.

- [ ] **Step 2: 운영 권위 한 줄**

DEPLOY.md 상단에 "현재 운영 권위: VM at `<host>`. Render 는 폐기/staging." 명시.

- [ ] **Step 3: 최종 PR**

`deploy/`, `.github/workflows/deploy-backend.yml`, `backend_ai/DEPLOY.md`, 본 plan 문서까지 한 PR. 리뷰 시 시크릿 등록 / DNS / SSH 키 셋업이 *서버 측 사전 작업으로 끝나 있는지* 확인.

---

## Self-Review

**1. Spec coverage**
- 환경 비교(설계 §2) → Task 0 분기. 권장안 = A 로 본 plan 전체.
- 시크릿/인증(설계 §4) → Task 1, 2, 6 Step 1.
- 트리거 옵션(설계 §5) → Task 6 (workflow_run + workflow_dispatch).
- 배포 절차(설계 §6) → Task 5 (수동 첫회) + Task 6 (자동).
- 네트워크/HTTPS(설계 §7) → Task 4.
- 검증/모니터링(설계 §8) → Task 5/6 (헬스), Task 8 (알림/로그).
- 리스크/롤백(설계 §9) → Task 7 (롤백 + 리허설).
- 변경 파일(설계 §10) → Task 0/3/4/6/7/9 가 전부 다룸.

→ 누락 없음.

**2. Placeholder scan**
- "TBD/적절히/비슷하게" 없음.
- 모든 코드 step 에 전체 스니펫 포함.
- 서버 측 절대 경로(`/opt/newcapstone`) / 컨테이너 이름(`newcapstone-backend`) / 이미지 풀네임(`ghcr.io/mokotan1/newcapstone-ai`) 일관.

**3. 이름 일관성**
- 컨테이너: `newcapstone-backend` — Task 3 정의, Task 6 헬스체크 스크립트에서 동일하게 inspect.
- env 파일 키: `IMAGE_TAG`, `GROQ_API_KEY`, `GOOGLE_API_KEY` — Task 2/3/5/6/7 일관.
- 시크릿 이름: `DEPLOY_HOST/USER/SSH_KEY/PORT/HEALTHCHECK_URL/WEBHOOK_URL` — Task 6/8 일관.
- 권위 SHA 파일: `.last_good_sha` — Task 5/6/7 일관.

→ 통과.

---

## Appendix C — Render 단축 경로 (Task 0 답변이 PaaS 일 때만)

이 경우 본 plan 의 Task 1~7 은 의미 없음. 다음만 수행:

- [ ] Render 대시보드 → 서비스 선택 → Environment → `GROQ_API_KEY`, `GOOGLE_API_KEY` 입력
- [ ] `render.yaml` 의 sync: false 가 그대로인지 확인
- [ ] main 머지 후 자동 빌드 성공 확인
- [ ] Render URL 에서 `/` 가 200
- [ ] 사용자 도메인 연결 → Render 가 인증서 자동 발급
- [ ] 롤백 = Render UI "Manual Deploy → 이전 커밋"
- [ ] DEPLOY.md 에 "운영 권위 = Render. GHCR 이미지는 백업/검증용" 명시

본 plan 의 Task 8 (모니터링), Task 9 (문서) 는 그대로 적용 가능.

---

**참고:** GHCR 까지의 빌드 파이프라인은 `docs/superpowers/plans/2026-05-04-github-actions-backend-build.md`, 컨테이너 사용 예시는 `backend_ai/DEPLOY.md` 에 기존 문서가 있다.
