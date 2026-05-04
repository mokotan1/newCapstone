# 백엔드 배포(GHCR → 실행 환경) 설계

- 작성일: 2026-05-04
- 대상 이미지: `ghcr.io/mokotan1/newcapstone-ai:latest`, `:<sha>`
- 선행 산출물:
  - `docs/superpowers/plans/2026-05-04-github-actions-backend-build.md` — GHCR 푸시까지
  - `backend_ai/DEPLOY.md` — pull/run 예시
- 후속 산출물(이 설계와 짝):
  - `docs/superpowers/plans/2026-05-04-backend-deploy-rollout.md` — 실행 계획(체크리스트)

## 0. 가장 합리한 기본 가정과 사용자에게 묻는 질문

다음 5 변수는 사용자 회신 전이라 다음과 같이 가정하고, 회신이 오면 본 문서를 갱신한다.

| 변수 | 기본 가정(이 문서가 따르는 값) | 영향 |
|---|---|---|
| 타깃 환경 | **클라우드 VM (Ubuntu 22.04, 1~2 vCPU, 2GB+ RAM)** — 학교 캡스톤이 가장 흔히 쓰는 형태 | 권장 기본안 = Ubuntu + Docker Compose + GHCR pull (Option A) |
| 도메인·HTTPS | **있음** (외부에서 시연 필요) | Caddy 역프록시 + Let's Encrypt 자동 발급 포함 |
| GHCR 패키지 가시성 | **public** (이전 설계가 기본 권장) | docker login 없이 pull 가능, 시크릿 단순화 |
| 자동화 수준 | **반자동** — main 머지 시 GitHub Actions 가 SSH 로 배포 스크립트 호출. 단, 사용자가 원하면 `workflow_dispatch` 수동 트리거도 같이 둔다 | deploy-backend.yml 별도 파일, 수동 버튼 병행 |
| 롤백 정책 | **이전 SHA 태그로 즉시 되돌리기** (latest 만으로는 불가) | 컴포즈 파일에 SHA 변수 사용, 실패 시 직전 SHA 로 재기동 |

**사용자 회신 필요 질문 (5개 이하)**
1. 타깃 환경이 위 가정(클라우드 VM)이 맞나? 아니면 집 PC / 학교 클러스터 / Render·Fly·Railway 같은 PaaS 인가?
2. 사용할 도메인이 있나? 있으면 어디서 발급받았는지 (DNS A 레코드 직접 조작 가능한지)?
3. GHCR 가시성을 public 으로 둬도 되나? (private 이면 서버에 PAT 가 필요해 시크릿 1 개 추가됨)
4. main 머지 시 *완전 자동* 배포가 맞나? 아니면 머지 후 사람이 한 번 버튼 눌러야 하는가?
5. 한 PC 만 배포 대상인가, 다수(예: 시연 PC 2 대) 인가?

위 질문이 미회신이라도 본 설계는 "기본 가정값" 으로 끝까지 진행한다.

## 1. 목적

GHCR 에 올라간 백엔드 이미지를 **실제 외부에서 접근 가능한 상태**로 띄우는 표준 절차를 정의한다. 사람이 매번 SSH 들어가 `docker pull && docker run` 치는 무명의 의식이 되지 않게, **재현 가능 / 롤백 가능 / 비밀이 GitHub 에 안 새는** 구조를 만든다.

## 2. 환경 선택지 비교

| 항목 | A. Ubuntu VM + Docker Compose + GHCR pull (권장) | B. Ubuntu VM + systemd unit | C. PaaS (Render — 이미 `render.yaml` 존재) |
|---|---|---|---|
| 난이도 | 중 (Compose/Caddy 셋업 1회) | 중상 (unit 파일·재시작 정책 직접 작성) | **하** (GitHub 연동 클릭 몇 번) |
| 비용(월 추정) | VM \$5~10 (Oracle Free Tier 가능) | 동일 | Render Free 가능, Hobby 유료(\$7) |
| 팀 접근 편의 | SSH 키 공유 필요, 절차서 명확 | 동일 | 대시보드 로그인만 — **가장 쉬움** |
| HTTPS · 도메인 | Caddy 자동(80/443) + 사용자 도메인 A 레코드 | Caddy/Nginx 직접 + certbot | Render 가 `*.onrender.com` 자동, 커스텀 도메인 자동 인증 |
| GHCR 연동 | `docker pull` 직접 — 가장 직관적 | 동일 | Render 는 GitHub repo 직접 빌드(이미 설정됨) — **GHCR 이미지를 안 씀** |
| main 자동 배포 | GitHub Actions → SSH → `compose pull && up -d` | 동일 + `systemctl reload` | Render 가 push 감지해서 자동(현재 동작) |
| 롤백 | `IMAGE_TAG=<old-sha> docker compose up -d` | unit ExecStart 변경 후 `daemon-reload` | Render UI 의 "Rollback" 버튼 |
| 무중단 | 약함 (컨테이너 교체 1~3 초 다운) | 동일 | 약함~중간 (zero-downtime deploy 옵션) |
| LLM 키 보관 | 서버 `.env` (chmod 600) — GitHub 에 없음 | 동일 | Render Env Vars (대시보드) — GitHub 에 없음 |
| 의존성 lock-in | 거의 없음 | 거의 없음 | Render 종속 (이전 시 재설정) |
| **이 프로젝트 적합성** | ✅ 시연용 + GHCR 활용 + 학습 가치 | △ Compose 보다 굳이 쓸 이유 적음 | ⚠️ 이미 push-기반 자동 빌드라 GHCR 가 무용 |

**왜 A 를 권장**:
- GHCR 에 이미지를 올려둔 의의를 살리려면 *그 이미지를 그대로 끌어 쓰는 환경* 이 자연스럽다. C 는 Render 가 자체 빌드하므로 GHCR 푸시가 사실상 보관소로만 쓰임.
- Compose 는 "한 파일에 컨테이너+포트+env" 가 모두 적혀 있어 다른 PC 에 복제하기 쉽다 (사용자가 다수 시연 PC 시나리오 5 번 질문에 yes 라면 동일 compose 파일을 그대로 재사용).
- B 는 Compose 가 이미 멱등 재시작·헬스체크·재기동 정책을 다 제공하므로 굳이 systemd 로 한 단계 더 내려갈 이유가 적다.

C(Render) 가 더 편한 케이스(도메인/HTTPS 신경 쓰기 싫고 키만 잘 넣고 싶다)에 한해 **Appendix C** 에 핵심 절차만 따로 둔다 — 권장안과 양립.

## 3. 권장안(A)의 아키텍처

```
[GitHub main push]
       │
       ▼
[backend-build.yml] ── 빌드/테스트/GHCR push (이미 구현됨)
       │  on: workflow_run (success) 또는 동일 워크플로 추가 job
       ▼
[deploy-backend.yml] ── workflow_dispatch + main push 자동
       │
       │  ssh -i $DEPLOY_KEY user@host \
       │     "cd /opt/newcapstone && IMAGE_TAG=<sha> docker compose pull && \
       │      docker compose up -d --remove-orphans && \
       │      ./scripts/postdeploy_healthcheck.sh"
       ▼
[VM: Ubuntu 22.04]
   ├── /opt/newcapstone/docker-compose.prod.yml
   ├── /opt/newcapstone/.env            (chmod 600, root:root)
   ├── /opt/newcapstone/scripts/postdeploy_healthcheck.sh
   ├── /opt/newcapstone/scripts/rollback.sh
   └── caddy (host network or :80/:443) → backend(:8000)
                  │
                  ▼
              사용자 도메인 (HTTPS 자동)
```

핵심 결정:
- **이미지 태그는 SHA 로 고정**. `latest` 만 쓰면 롤백할 때 어떤 버전으로 돌아갈지 알 수 없다. compose 파일에 `image: ghcr.io/mokotan1/newcapstone-ai:${IMAGE_TAG}` 로 두고 환경변수로 주입.
- **시크릿은 서버 `.env` 에만**. GitHub Actions secrets 에는 SSH 접속 정보(host, user, private key) 만 넣고 LLM 키는 절대 넣지 않는다.
- **헬스체크는 두 단계**: (1) 컨테이너 자체 healthcheck (Dockerfile 또는 compose), (2) 배포 직후 외부에서 `curl https://<도메인>/` 검증. (1) 만으로는 역프록시까지 살아있는지 모름.

## 4. 시크릿 · 인증

### 4.1 GHCR pull 인증

| 가시성 | 서버에서 docker login 필요? | 방법 |
|---|---|---|
| **public** | 불필요 | `docker pull ghcr.io/...` 가 그냥 됨 |
| private | 필요 | 서버에서 `echo $GHCR_PAT \| docker login ghcr.io -u <user> --password-stdin`. PAT 는 `read:packages` 스코프, `~root/.docker/config.json` 에 저장됨 |

기본 가정은 public 이라 docker login 자체를 안 한다. 사용자가 private 으로 가겠다고 하면 PAT 발급 + 서버에 1 회 login 단계가 추가되며, 그 PAT 도 `.env` 가 아니라 root 의 `.docker/config.json` 에 들어간다.

### 4.2 GitHub Actions → 서버 SSH

- Repo Settings → Secrets:
  - `DEPLOY_HOST` — VM public IP 또는 도메인
  - `DEPLOY_USER` — 배포용 비루트 계정 (예: `deploy`)
  - `DEPLOY_SSH_KEY` — private key (서버 `~deploy/.ssh/authorized_keys` 에 대응 public key 등록)
  - (선택) `DEPLOY_PORT` — 22 가 아니면 등록
- 서버 측 `deploy` 계정은 sudo 권한 없음. 단, `docker` 그룹에 넣어 docker 명령은 실행 가능.

### 4.3 서버 환경변수 (LLM 키)

- `/opt/newcapstone/.env` 에:
  ```
  GROQ_API_KEY=...
  GOOGLE_API_KEY=...
  IMAGE_TAG=<처음에는 latest 또는 첫 배포 SHA>
  ```
- chmod 600, owner `root` 또는 `deploy`. compose 가 이 파일을 자동 로드.
- **이 파일은 SCP / 직접 입력 / 패스워드 매니저 로만 옮긴다. git, GitHub, Slack DM 모두 금지.**

## 5. 트리거 옵션 비교

| 옵션 | 설명 | 장 | 단 |
|---|---|---|---|
| (1) `backend-build.yml` 에 deploy job 추가 | 한 워크플로 안에서 push job 다음에 deploy | 단순 | build 결과/배포 결과가 한 화면에 섞임. 권한도 한 워크플로에 모임 |
| (2) **별도 `deploy-backend.yml` (권장)** | `on: workflow_run` (build 성공 시) + `workflow_dispatch` 병행 | 책임 분리, 수동 재배포 가능, 권한 분리 (deploy 만 SSH 키 접근) | 파일 1 개 더 |
| (3) `workflow_dispatch` 만 (완전 수동 버튼) | main 자동 배포 안 함 | 가장 안전, 시연 직전에만 손으로 돌릴 때 좋음 | "자동" 약속을 못 지킴 |
| (4) 태그 릴리스 (`on: release`) | `git tag v1.0.0` push 시만 배포 | 의도된 시점만 운영에 반영 | 캡스톤 일정에 비해 무겁고, main 머지 자동성을 잃음 |

권장 = (2). main 머지 자동 + 손으로도 누를 수 있어 시연 직전 비상 대응 가능. (3)/(4) 가 좋은 케이스는 사용자가 "자동화 수준 = 문서만 / 반자동" 이라 답하면 그쪽으로 전환.

## 6. 배포 절차(단계별)

서버에서 실제 일어나는 일을 시간 순으로 못 박아 둔다. Actions 가 SSH 한 줄로 실행할 스크립트의 의미가 이 표 안에 다 들어 있다.

| # | 단계 | 명령/검증 | 실패 시 |
|---|---|---|---|
| 1 | 새 SHA 로 .env 갱신 | `sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=$NEW_SHA/" .env` | abort, 이전 상태 유지 |
| 2 | 이미지 pull | `docker compose pull` | abort, 이전 상태 유지 |
| 3 | 컨테이너 교체 | `docker compose up -d --remove-orphans` | rollback (8) |
| 4 | 컨테이너 readiness 대기 | 30s 폴링 `docker inspect` healthcheck | rollback (8) |
| 5 | 내부 헬스 | `curl -f http://localhost:8000/` | rollback (8) |
| 6 | 외부 헬스 | `curl -f https://<도메인>/` (역프록시·인증서 동작 확인) | rollback (8) |
| 7 | 이전 SHA 메모 | `echo $OLD_SHA > .last_good_sha` | — |
| 8 | (실패 시) 롤백 | `IMAGE_TAG=$OLD_SHA docker compose up -d` | 사람 호출 |

**무중단 트레이드오프 명시**
- 단순 `compose up -d` 는 1~3 초 다운타임 발생 (컨테이너 교체 시점). 캡스톤 시연 환경에서는 허용 가능 수준.
- 진짜 무중단을 원하면 (a) blue/green: 컨테이너 두 벌 + Caddy 가 새 컨테이너로 라우팅 후 옛 컨테이너 종료, (b) `docker compose up -d --no-deps --scale backend=2 backend` 후 정리. 둘 다 복잡도가 크게 늘어나서 **이번 plan 범위 밖**으로 둔다.

**롤백 핵심**
- `latest` 만 가지고는 롤백 못 한다. `:<sha>` 태그가 있어서 가능. 운영 적용 시점부터 `latest` 가 아니라 SHA 를 권위로 본다.

## 7. 네트워크 / HTTPS

- VM 80/443 만 보안그룹에서 허용. 8000 은 외부에 절대 공개하지 않음 — 역프록시(Caddy) 가 8000 으로 forward.
- Caddy 한 줄 설정 예 (`/opt/newcapstone/Caddyfile`):
  ```
  api.example.com {
      reverse_proxy backend:8000
  }
  ```
- 인증서는 Caddy 가 Let's Encrypt 자동 발급/갱신. DNS A 레코드만 VM IP 로 맞춰두면 끝.
- 도메인 없을 때 임시 대안: `https://<vm-ip>.nip.io` 처럼 nip.io 사용 — 인증서도 정상 발급되지만 시연 도메인으로는 어색함. 사용자가 도메인 없음 답변이면 이 임시안으로 전환.

## 8. 검증 · 모니터링

| 레이어 | 체크 | 위치 |
|---|---|---|
| 컨테이너 healthcheck | Compose `healthcheck` 가 `curl -f localhost:8000/` 매 30s | `docker compose ps` |
| 배포 직후 검증 | 위 6 단계 | Actions 로그 + 서버 `~/deploy/last_run.log` |
| 평시 로그 | `docker compose logs -f --tail=200 backend` | SSH 로 보거나, 가능하면 `journalctl -u docker` |
| Caddy 로그 | `/var/log/caddy/*` | 도메인·HTTPS 문제 디버깅 |
| (선택) 알림 | 배포 실패 시 Discord/Slack webhook 1 개 — Actions secret 으로 webhook URL | optional, plan Task 7 |

LLM 키 누출 같은 사건성 모니터링은 plan 범위 밖. README 에 "키가 노출됐다 싶으면 즉시 회전" 이라는 정책 한 줄만 둔다.

## 9. 리스크 · TODO

| 리스크 | 대응 |
|---|---|
| 첫 SSH 자동화에서 키/권한 실수 — 서버 잠겨버림 | 콘솔 접속 가능한 클라우드 콘솔(또는 물리 접근) 확보된 상태에서만 자동화 활성. 미확보 시 수동 절차로 1 회 검증. |
| `latest` 만 보고 롤백 시도 → 어디로 돌아가는지 모름 | SHA 고정. `.last_good_sha` 관리. plan Task 5. |
| `.env` 가 백업/스냅샷에 포함돼 외부로 새는 케이스 | 백업에서 `.env` 제외 규칙 명문화. `chmod 600`. |
| Caddy 인증서 발급 실패 (DNS 미전파) | 배포 첫 회는 도메인 A 레코드 전파 후에만 시도. 실패 시 `:80` 평문으로 임시 운영 후 재시도. |
| Render(C) 와 VM(A) 동시 운영 → 무엇이 진짜 운영인지 헷갈림 | "운영 = A" 명시. Render 는 staging 또는 폐기 결정. plan Task 0. |
| Actions 시크릿이 fork PR 에 노출 | `pull_request_target` 안 씀. `workflow_run` 은 main push 한정. |
| 시연 직전 GHCR 장애로 pull 실패 | 서버에 직전 이미지가 캐시돼 있음 → 그대로 동작. 일부러 prune 자제. |

## 10. 산출물 / 변경되는 파일 (요약)

| 위치 | 신규/변경 |
|---|---|
| `.github/workflows/deploy-backend.yml` | 신규 |
| `deploy/docker-compose.prod.yml` | 신규 (compose 파일은 레포 내 보관, 서버에는 SCP 또는 git pull) |
| `deploy/Caddyfile` | 신규 |
| `deploy/scripts/postdeploy_healthcheck.sh` | 신규 |
| `deploy/scripts/rollback.sh` | 신규 |
| 서버: `/opt/newcapstone/.env` | **레포 외부.** 서버에서 직접 작성 (gitignore 와 무관) |
| `backend_ai/DEPLOY.md` | 변경 (운영 절차 섹션 추가) |
| `docs/superpowers/plans/2026-05-04-backend-deploy-rollout.md` | 신규 (실행 계획서) |

## 11. Appendix C — Render 만 가는 경우 (대안 빠른 길)

만약 사용자가 질문 1 회신에서 "Render 로 가겠다" 라 답하면 위 플로우 대부분이 무력화된다. 그 경우 핵심만:
- `render.yaml` 그대로 쓰되 `envVars` 에 `GROQ_API_KEY`, `GOOGLE_API_KEY` 를 Render 대시보드에서 채운다 (sync: false).
- main 머지 = 자동 빌드/배포 (GHCR 이미지는 사용 안 됨).
- 롤백은 Render UI "Manual Deploy → 이전 커밋".
- 본 plan 의 Task 1~6 은 스킵, Task 7 검증/문서만 남는다.

이 경우 GHCR 푸시는 백업/검증용으로만 의미가 남으므로, 사용자에게 "이미지를 두 군데(Render+GHCR) 에 둘 가치" 를 명시적으로 결정하게 한다.
