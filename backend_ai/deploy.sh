#!/usr/bin/env bash
#
# backend_ai EC2 배포 스크립트 — EC2 호스트(Linux)에서 실행한다.
#
#   사용법:
#     cd <repo>/backend_ai
#     ./deploy.sh            # 현재 체크아웃된 코드로 빌드/배포
#     ./deploy.sh <git-ref>  # 지정 브랜치/커밋으로 체크아웃 후 배포
#
#   동작: (선택)git 체크아웃 → .env·필수키 검증 → logs/ 보장 →
#         docker compose up --build -d → 헬스체크 → 직전 SHA 기록(롤백용)
#
#   전제: backend_ai/.env 에 GROQ_API_KEY 또는 GOOGLE_API_KEY 가 채워져 있어야 한다.
#         (.env 는 커밋 대상이 아니며 서버에서 직접 관리한다.)
set -euo pipefail

# --- 설정 (매직넘버 제거 / 환경변수로 덮어쓰기 가능) ---
CONTAINER_NAME="${CONTAINER_NAME:-disputatio-backend-ai}"
APP_PORT="${APP_PORT:-8000}"
HEALTH_TIMEOUT_SECONDS="${HEALTH_TIMEOUT_SECONDS:-90}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$SCRIPT_DIR"

# --- 0) 배포 대상 ref (인자로 주면 git 갱신) ---
TARGET_REF="${1:-}"
PREV_SHA="$(cat .last_deployed_sha 2>/dev/null || true)"

if [ -n "$TARGET_REF" ]; then
  echo "==> git fetch & checkout: ${TARGET_REF}"
  git -C "$REPO_DIR" fetch --all --prune
  git -C "$REPO_DIR" checkout "$TARGET_REF"
  # 브랜치면 최신으로 당겨오고, 분리 HEAD(태그/SHA)면 조용히 통과
  git -C "$REPO_DIR" pull --ff-only 2>/dev/null || true
fi

DEPLOY_SHA="$(git -C "$REPO_DIR" rev-parse --short HEAD)"
echo "==> 배포 커밋: ${DEPLOY_SHA}"

# --- 1) .env 및 필수 키 검증 (fail-fast) ---
if [ ! -f .env ]; then
  echo "ERROR: backend_ai/.env 가 없습니다. 'cp .env.example .env' 후 키를 채우세요." >&2
  exit 1
fi
if ! grep -Eq '^(GROQ_API_KEY|GOOGLE_API_KEY)=.+' .env; then
  echo "ERROR: .env 에 GROQ_API_KEY 또는 GOOGLE_API_KEY 중 하나는 설정되어야 합니다." >&2
  exit 1
fi

# --- 2) 로그 볼륨 디렉터리 보장 (./logs:/app/logs) ---
mkdir -p logs

# --- 3) 빌드 & 기동 ---
echo "==> docker compose up --build -d"
docker compose up --build -d --remove-orphans

# --- 4) 헬스체크: 컨테이너 health + HTTP GET / ---
echo "==> 헬스체크 대기 (최대 ${HEALTH_TIMEOUT_SECONDS}s)"
for i in $(seq 1 "$HEALTH_TIMEOUT_SECONDS"); do
  STATUS="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}unknown{{end}}' "$CONTAINER_NAME" 2>/dev/null || echo unknown)"
  if [ "$STATUS" = "healthy" ]; then
    break
  fi
  if [ "$i" -eq "$HEALTH_TIMEOUT_SECONDS" ]; then
    echo "ERROR: 컨테이너가 ${HEALTH_TIMEOUT_SECONDS}s 내 healthy 되지 않음 (마지막 상태: ${STATUS})" >&2
    docker compose logs --tail=200
    exit 1
  fi
  sleep 1
done

curl -fsS "http://127.0.0.1:${APP_PORT}/" >/dev/null
echo "==> GET / 응답 정상"

# --- 5) 성공 시 SHA 기록 (롤백용) ---
if [ -n "$PREV_SHA" ] && [ "$PREV_SHA" != "$DEPLOY_SHA" ]; then
  echo "$PREV_SHA" > .prev_deployed_sha
fi
echo "$DEPLOY_SHA" > .last_deployed_sha

echo "==> 배포 완료: ${DEPLOY_SHA}"
docker compose ps
