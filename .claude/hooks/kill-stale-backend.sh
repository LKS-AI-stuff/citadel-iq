#!/bin/bash
# Stop hook: kill the backend on :5157 only if a backend source file (.cs/.csproj/.sln/.slnx/appsettings)
# was modified AFTER that process started — i.e. it is genuinely serving stale compiled code.
# (Checking `git status` alone isn't enough: uncommitted-but-already-running changes would kill the
# server after every turn, even frontend-only ones.)
# Frontend (Vite, :5173) is deliberately untouched: it hot-reloads on file changes.

cd "$(git rev-parse --show-toplevel 2>/dev/null)" || exit 0

PID=$(lsof -tiTCP:5157 -sTCP:LISTEN 2>/dev/null | head -n1)
[ -z "$PID" ] && exit 0

# Process start time (epoch seconds) from ps's [[dd-]hh:]mm:ss elapsed time.
ETIME=$(ps -o etime= -p "$PID" 2>/dev/null | tr -d ' ')
[ -z "$ETIME" ] && exit 0
DAYS=0
[[ "$ETIME" == *-* ]] && { DAYS=${ETIME%%-*}; ETIME=${ETIME#*-}; }
IFS=: read -r A B C <<< "$ETIME"
if [ -n "$C" ]; then H=$A; M=$B; S=$C; else H=0; M=$A; S=$B; fi
ELAPSED=$(( 10#$DAYS*86400 + 10#$H*3600 + 10#$M*60 + 10#$S ))
STARTED=$(( $(date +%s) - ELAPSED ))

# Modified or untracked backend files (bin/obj and other ignored paths excluded).
NEWER=""
while IFS= read -r f; do
  [ -f "$f" ] || continue
  if [ "$(stat -f %m "$f" 2>/dev/null)" -gt "$STARTED" ]; then NEWER="$f"; break; fi
done < <(git ls-files -m -o --exclude-standard -- '*.cs' '*.csproj' '*.sln' '*.slnx' 'CitadelIQ.Api/appsettings*.json' 2>/dev/null)

if [ -n "$NEWER" ]; then
  echo "citadel-iq stop-hook: $NEWER changed after the backend on :5157 (pid $PID) started — it would be serving stale compiled code, so killing it." >&2
  kill -9 "$PID" 2>/dev/null
fi

exit 0
