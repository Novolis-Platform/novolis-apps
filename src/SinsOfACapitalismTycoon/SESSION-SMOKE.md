# Sins game agent protocol — multi-transport smoke

Domain agent (`agent.*`) is separate from Avalonia glass agent (`ui.*`).
One Sins EXE (Avalonia **or** `--mode captain`) exposes the surface via `AgentSurface`.

## Build

```powershell
dotnet build d:\novolis\novolis-agent\src\Novolis.Agent.Surface -p:NovolisUseProjectReferences=true
dotnet build d:\novolis\novolis-apps\src\SinsOfACapitalismTycoon -p:NovolisUseProjectReferences=true
```

## Run host

```powershell
$env:NOVOLIS_GAME_SESSION = "1"          # LocalIpc + HTTP (default)
# $env:NOVOLIS_GAME_SESSION_HTTP = "0"   # LocalIpc only
# $env:NOVOLIS_GAME_SESSION_TCP = "1"    # also TCP JSONL :18766
# $env:NOVOLIS_GAME_SESSION_HTTP_PORT = "18765"

dotnet run --project d:\novolis\novolis-apps\src\SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- `
  --mode captain --days 30d --seed 1001 --player on
```

Markers: `%TEMP%/novolis-game-session.ipc` · `.http` · `.tcp`

## HTTP smoke (preferred wide transport)

```powershell
curl http://127.0.0.1:18765/agent/hello
curl http://127.0.0.1:18765/agent/snapshot
curl http://127.0.0.1:18765/agent/actions
curl -X POST http://127.0.0.1:18765/agent/command `
  -H "content-type: application/json" `
  -d '{"actionId":"travel","destSystemId":"ez-aquarii"}'
# SSE: curl -N http://127.0.0.1:18765/agent/events
```

`/session/...` paths remain accepted for backward compatibility (rewritten to `/agent/...`).

## MCP

`session_hosts` → `session_http_connect` (or `session_connect` for LocalIpc) → `session_snapshot` / `session_command`.

## Playtest gates

```powershell
dotnet run --project d:\novolis\novolis-apps\src\SinsOfACapitalismTycoon `
  -p:NovolisUseProjectReferences=true -- --playtest --seed 1001
```
