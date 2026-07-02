# MCP Example App (.NET 10 + Docker)

This solution is a **.NET 10 demo concept** showing how to connect:

- a `TodoApi` (business API)
- an `McpServer` (MCP-style translation/orchestration layer)
- an `LlmClient` (client workflow)
- an `Ollama` container (local LLM runtime)

Everything runs in Docker using `docker-compose.yml`.

## Architecture

```text
LlmClient  --->  McpServer  --->  TodoApi
    |                           
    +--------->  Ollama (health/tags check)
```

- `TodoApi` exposes REST endpoints for todo operations.
- `McpServer` wraps/forwards todo operations through MCP-style endpoints.
- `LlmClient` demonstrates the flow by:
  1. checking MCP health,
  2. listing todos,
  3. creating a new todo,
  4. listing todos again,
  5. checking Ollama availability.

## Services and ports

From `docker-compose.yml`:

- `todo-api` -> `http://localhost:5000`
- `mcp-server` -> `http://localhost:6000`
- `ollama-container` -> `http://localhost:11434`
- `llm-client` -> runs once, writes output, then exits

## Prerequisites

- Docker Desktop (or Docker Engine + Compose)
- Internet access for first-time image pulls

## Quick start

Run from the solution root (`MCPExampleApp`):

```powershell
docker compose up --build
```

On first run, Docker will:

- build .NET images for `TodoApi`, `McpServer`, and `LlmClient`
- pull `ollama/ollama:latest`

## How to see the client output (main demo proof)

The `llm-client` container is the key demo output.

### Option 1: While `docker compose up` is running

You will see log lines similar to:

```text
[Client] Booting .NET AI Orchestrator...
[Client] Checking MCP server health...
[MCP] {"status":"ok"}
[Client] Listing todos...
[Before] [{"id":1,"title":"Review Microsoft Learn Guidance","status":"Pending"},{"id":2,"title":"Build pure .NET MCP Server","status":"Completed"}]
[Client] Creating todo...
[Create] 201
{"id":3,"title":"Verify C# Contracts","status":"Pending"}
[Client] Listing todos after create...
[After] [... includes the new todo ...]
[Client] Checking Ollama endpoint...
[Ollama] { ... }
[Client] Done.
```

### Option 2: View logs after startup

```powershell
docker compose logs llm-client
```

### Option 3: Re-run only the client workflow

Because `llm-client` is a one-shot app, you can rerun it any time:

```powershell
docker compose run --rm llm-client
```

## Manual endpoint checks (optional)

### Check MCP health

```powershell
curl http://localhost:6000/health
```

### List todos through MCP

```powershell
curl http://localhost:6000/mcp/list_todos
```

### Create a todo through MCP

```powershell
curl -X POST http://localhost:6000/mcp/create_todo -H "Content-Type: application/json" -d "{\"title\":\"Created manually via MCP\"}"
```

### Query Todo API directly

```powershell
curl http://localhost:5000/todos
```

## Stopping and cleanup

Stop containers:

```powershell
docker compose down
```

Also remove volumes (including local Ollama model/cache volume):

```powershell
docker compose down -v
```

## Notes

- This demo currently validates the end-to-end MCP + Todo flow and connectivity to Ollama.
- `LlmClient` output is the best place to confirm the full scenario is working.
- You can iterate quickly by rebuilding and rerunning only changed services.
