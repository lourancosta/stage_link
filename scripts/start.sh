#!/usr/bin/env bash
# Starts the whole StageLink demo: 3 simulated speakers, the backend and the web app.
# Then open http://localhost:4200. Press Ctrl+C to stop everything.
#
# Needs: .NET 10 SDK and Node.js 22+ (macOS, Linux, or WSL on Windows).

set -euo pipefail
cd "$(dirname "$0")/.."

# Each background program gets its own process group, so Ctrl+C here can stop
# it together with any child processes it starts.
set -m

SPEAKERS=("51000:Main L" "51010:Main R" "51020:Sub")
BACKEND_PORT=5080
FRONTEND_PORT=4200
BIN="bin/Debug/net10.0"

pids=()

stop_all() {
  trap - INT TERM EXIT
  echo
  echo "Stopping..."
  for pid in "${pids[@]}"; do
    kill -TERM -- "-$pid" 2>/dev/null || true
  done
  wait 2>/dev/null || true
  echo "Stopped."
}
trap stop_all INT TERM EXIT

port_in_use() {
  lsof -nP -iTCP:"$1" -sTCP:LISTEN >/dev/null 2>&1
}

# 1. Make sure the ports are free (a leftover instance would block them).
busy=()
for port in "$BACKEND_PORT" "$FRONTEND_PORT" "${SPEAKERS[@]%%:*}"; do
  if port_in_use "$port"; then busy+=("$port"); fi
done
if [ ${#busy[@]} -gt 0 ]; then
  echo "These ports are already in use: ${busy[*]}"
  echo "Is StageLink already running? Stop it first, then try again."
  trap - INT TERM EXIT
  exit 1
fi

# 2. Build once, and install the frontend packages the first time.
echo "Building..."
dotnet build StageLink.slnx --nologo -v quiet
if [ ! -d web/node_modules ]; then
  echo "Installing frontend packages (first run only)..."
  (cd web && npm ci --no-audit --no-fund)
fi

# 3. Start everything.
for speaker in "${SPEAKERS[@]}"; do
  port="${speaker%%:*}"
  name="${speaker#*:}"
  dotnet "src/StageLink.Simulator.Host/$BIN/StageLink.Simulator.Host.dll" "$port" "$name" >/dev/null &
  pids+=($!)
  echo "  speaker \"$name\" on TCP port $port"
done

(cd src/StageLink.Web && exec dotnet "$BIN/StageLink.Web.dll" --urls "http://localhost:$BACKEND_PORT") >/dev/null &
pids+=($!)
echo "  backend on http://localhost:$BACKEND_PORT"

(cd web && exec node_modules/.bin/ng serve --port "$FRONTEND_PORT") >/dev/null 2>&1 &
pids+=($!)
echo "  web app starting on http://localhost:$FRONTEND_PORT ..."

# 4. Wait until the web app answers.
for _ in $(seq 1 120); do
  if curl -fsS "http://localhost:$FRONTEND_PORT" >/dev/null 2>&1; then
    echo
    echo "StageLink is running: open http://localhost:$FRONTEND_PORT"
    echo "Press Ctrl+C to stop everything."
    break
  fi
  sleep 1
done

wait
