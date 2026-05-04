#!/usr/bin/env bash
set -euo pipefail

cd /opt/newcapstone || exit 1

# 1) Wait for compose-defined healthcheck to report healthy (up to ~60s)
for i in $(seq 1 60); do
	STATUS="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}unknown{{end}}' newcapstone-backend 2>/dev/null || echo unknown)"
	if [ "$STATUS" = "healthy" ]; then
		break
	fi
	if [ "$i" -eq 60 ]; then
		echo "Container did not become healthy in 60s (last status: ${STATUS:-unknown})"
		docker compose logs --tail=200 backend
		exit 1
	fi
	sleep 1
done

# 2) In-container API from host perspective
curl -fsS http://localhost:8000/ >/dev/null

# 3) External URL (HTTPS) when configured on the VM
if [ -n "${DEPLOY_HEALTHCHECK_URL:-}" ]; then
	curl -fsS "$DEPLOY_HEALTHCHECK_URL" >/dev/null
fi

echo "Health check passed"
