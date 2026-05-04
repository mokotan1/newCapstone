#!/usr/bin/env bash
set -euo pipefail

cd /opt/newcapstone || exit 1

TARGET="${1:-$(cat .last_good_sha 2>/dev/null || echo latest)}"
echo "Rolling back to ${TARGET}"

sed -i "s/^IMAGE_TAG=.*/IMAGE_TAG=${TARGET}/" .env
docker compose pull
docker compose up -d --remove-orphans
./scripts/postdeploy_healthcheck.sh
echo "Rollback OK: ${TARGET}"
