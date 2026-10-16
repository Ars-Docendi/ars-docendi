#!/usr/bin/env bash
# SessionStart hook for Claude Code cloud sessions.
#
# The environment setup script installs the toolchains (.NET 10, Node 24) once and
# is cached; this hook handles what the cache cannot keep: dependencies that follow
# the checked-out branch and services that are not running after a restore.
#
# Outside the cloud (CLAUDE_CODE_REMOTE != true) it does nothing.

set -euo pipefail

[ "${CLAUDE_CODE_REMOTE:-}" = "true" ] || exit 0

cd "${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"

LOG=/tmp/cloud-session-start.log
: >"$LOG"

paso() { echo "[session-start] $1"; }

# Docker: required by Testcontainers (integration tests) and by the compose Postgres.
if ! docker info >/dev/null 2>&1; then
    paso "starting docker"
    service docker start >>"$LOG" 2>&1 || (nohup dockerd >>"$LOG" 2>&1 &)
    for _ in $(seq 1 30); do
        docker info >/dev/null 2>&1 && break
        sleep 1
    done
fi
docker info >/dev/null 2>&1 || paso "WARNING: docker is not available (see $LOG)"

[ -f .env ] || cp .env.example .env

paso "pnpm install"
pnpm install --frozen-lockfile >>"$LOG" 2>&1

paso "dotnet restore"
dotnet restore backend/ArsDocendi.slnx >>"$LOG" 2>&1

if docker info >/dev/null 2>&1; then
    paso "postgres (compose)"
    docker compose up -d postgres >>"$LOG" 2>&1 || paso "WARNING: postgres did not start (see $LOG)"
fi

paso "ready"
