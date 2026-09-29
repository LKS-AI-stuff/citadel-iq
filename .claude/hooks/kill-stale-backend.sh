#!/bin/bash
# Stop hook: if backend (.cs/.csproj/appsettings) files have uncommitted changes,
# a running process on port 5157 is likely serving stale compiled code — kill it.
# Frontend (Vite, :5173) is deliberately untouched: it hot-reloads on file changes,
# so it never goes stale the way a compiled .NET process does.

cd "$(git rev-parse --show-toplevel 2>/dev/null)" || exit 0

CHANGED=$(git status --porcelain -- '*.cs' '*.csproj' '*.sln' '*.slnx' 'CitadelIQ.Api/appsettings*.json' 2>/dev/null)

if [ -n "$CHANGED" ]; then
  PID=$(lsof -tiTCP:5157 -sTCP:LISTEN 2>/dev/null)
  if [ -n "$PID" ]; then
    echo "citadel-iq stop-hook: uncommitted .cs/.csproj changes found and a backend process is still listening on :5157 (pid $PID) — it would be serving stale compiled code, so killing it." >&2
    kill -9 $PID 2>/dev/null
  fi
fi

exit 0
