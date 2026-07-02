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
    +--------->  Ollama (conversational planning + assistant responses)
```

- `TodoApi` exposes REST endpoints for todo operations.
- `McpServer` wraps/forwards todo operations through MCP-style endpoints.
- `LlmClient` demonstrates a conversational agent flow by:
  1. acting as a user with natural-language requests,
  2. sending each request to Ollama,
  3. letting Ollama choose which MCP tool to call (`list_todos`, `create_todo`, `update_todo_status`),
  4. executing the selected MCP call,
  5. returning a user-facing assistant response based on the tool result.

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

## Ollama model behavior

The client needs at least one Ollama model for generation.

- If no model exists, `llm-client` now automatically pulls `llama3.2`.
- You can override that by setting `OLLAMA_MODEL` for the client container.

Example manual pull (optional):

```powershell
docker exec -it ollama-container ollama pull llama3.2
```

## How to see the client output (main demo proof)

The `llm-client` container is the key demo output.

### Option 1: While `docker compose up` is running

You will see log lines similar to:

```text
[Client] Booting .NET AI Orchestrator...
[Client] Checking MCP server health...
[MCP] {"status":"ok"}
[Client] Listing todos...
[Before] [...]
[Client] Discovering available Ollama models...
[Client] Using Ollama model: llama3.2
[Client] Starting conversational flow (LLM acts on user requests via MCP tools)...
[User] List my todos.
[LLM Tool Plan] Tool=list_todos, Title=<none>, Id=<none>, Status=<none>
[Tool Result] [... todo list json ...]
[Assistant] You currently have ...
[User] Create a todo named Verify conversational MCP flow.
[LLM Tool Plan] Tool=create_todo, Title=Verify conversational MCP flow, Id=<none>, Status=<none>
[Tool Result] {"id":3,"title":"Verify conversational MCP flow","status":"Pending"}
[Assistant] Done — I created the todo ...
[User] List my todos again.
[LLM Tool Plan] Tool=list_todos, Title=<none>, Id=<none>, Status=<none>
[Tool Result] [... now includes the new todo ...]
[Assistant] Here is your updated list ...
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
- The client now follows the standard conversational pattern: user prompt -> LLM tool decision -> MCP call -> assistant response.
- `LlmClient` output is the best place to confirm the full scenario is working.
- You can iterate quickly by rebuilding and rerunning only changed services.
