#!/usr/bin/env bash
set -euo pipefail

cd /workspaces/ars-docendi

# Docker creates new named-volume roots as root. Give the non-root development
# user ownership before pnpm, NuGet, or Codex writes to those volumes.
cache_directories=(
  /workspaces/ars-docendi/node_modules
  /workspaces/ars-docendi/frontend/node_modules
  "$HOME/.local"
  "$HOME/.nuget"
  "$HOME/.codex"
)
sudo mkdir -p "${cache_directories[@]}"
sudo chown -R "$(id -u):$(id -g)" "${cache_directories[@]}"

if [[ ! -f .env ]]; then
  cp .env.example .env
fi

corepack enable
pnpm install --no-frozen-lockfile
dotnet restore backend/ArsDocendi.slnx

# CodeGraph is a developer-side MCP service, not an application dependency.
# Keep the version explicit so rebuilding the container does not silently pull
# a different parser/runtime. Override with CODEGRAPH_VERSION when upgrading.
CODEGRAPH_VERSION="${CODEGRAPH_VERSION:-1.6.0}"
if [[ ! -x "$HOME/.local/bin/codegraph" ]]; then
  npm install --global --prefix "$HOME/.local" "@colbymchenry/codegraph@${CODEGRAPH_VERSION}"
fi

if [[ ! -d .codegraph ]]; then
  CODEGRAPH_TELEMETRY=0 codegraph init
fi

if [[ ! -x "$HOME/.local/bin/codex" ]]; then
  curl -fsSL https://chatgpt.com/codex/install.sh | sh
fi

echo "Development container ready."
echo "Backend:  dotnet run --project backend/src/ArsDocendi.Host"
echo "Frontend: pnpm --filter frontend dev"
echo "Codex:    codex login --device-auth, then codex"
