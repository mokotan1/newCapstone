#!/usr/bin/env bash
#
# backend_ai 롤백 스크립트 — EC2 호스트(Linux)에서 실행한다.
#
#   사용법:
#     cd <repo>/backend_ai
#     ./rollback.sh            # 직전 정상 배포 SHA(.prev_deployed_sha)로 롤백
#     ./rollback.sh <git-ref>  # 지정 커밋/태그로 롤백
#
#   실제 빌드/기동/헬스체크는 deploy.sh 를 재사용한다(중복 제거).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

TARGET_REF="${1:-$(cat "$SCRIPT_DIR/.prev_deployed_sha" 2>/dev/null || true)}"
if [ -z "$TARGET_REF" ]; then
  echo "ERROR: 롤백 대상 ref 가 없습니다. 사용법: ./rollback.sh <git-sha>" >&2
  echo "       (직전 배포 기록 .prev_deployed_sha 도 존재하지 않습니다.)" >&2
  exit 1
fi

echo "==> 롤백 시작 → ${TARGET_REF}"
exec "$SCRIPT_DIR/deploy.sh" "$TARGET_REF"
