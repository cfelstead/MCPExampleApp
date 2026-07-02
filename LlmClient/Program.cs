using System.Net.Http.Json;
using System.Text.Json;

Console.WriteLine("[Client] Booting .NET AI Orchestrator...");

using var mcpHttpClient = new HttpClient { BaseAddress = new Uri("http://mcp-server:6000") };
using var ollamaHttpClient = new HttpClient { BaseAddress = new Uri("http://ollama-container:11434") };
ollamaHttpClient.Timeout = GetOllamaTimeout();

Console.WriteLine("[Client] Checking MCP server health...");
var health = await mcpHttpClient.GetStringAsync("/health");
Console.WriteLine($"[MCP] {health}");

Console.WriteLine("[Client] Listing todos...");
var before = await mcpHttpClient.GetStringAsync("/mcp/list_todos");
Console.WriteLine($"[Before] {before}");

Console.WriteLine("[Client] Discovering available Ollama models...");
var modelName = await EnsureModelAsync(ollamaHttpClient);
Console.WriteLine($"[Client] Using Ollama model: {modelName}");

var userRequests = new[]
{
    "List my todos.",
    "Create a todo named Verify conversational MCP flow.",
    "List my todos again."
};

Console.WriteLine("[Client] Starting conversational flow (LLM acts on user requests via MCP tools)...");

foreach (var userRequest in userRequests)
{
    Console.WriteLine($"[User] {userRequest}");

    var toolPlan = await PlanToolCallAsync(ollamaHttpClient, modelName, userRequest);
    Console.WriteLine($"[LLM Tool Plan] Tool={toolPlan.Tool}, Title={toolPlan.Title ?? "<none>"}, Id={toolPlan.Id?.ToString() ?? "<none>"}, Status={toolPlan.Status ?? "<none>"}");

    var toolResult = await ExecuteMcpToolAsync(mcpHttpClient, toolPlan);
    Console.WriteLine($"[Tool Result] {toolResult}");

    var assistantReply = await GenerateAssistantReplyAsync(ollamaHttpClient, modelName, userRequest, toolPlan, toolResult);
    Console.WriteLine($"[Assistant] {assistantReply}");
}

Console.WriteLine("[Client] Done.");

static TimeSpan GetOllamaTimeout()
{
    const int defaultTimeoutSeconds = 1800;
    var configuredTimeout = Environment.GetEnvironmentVariable("OLLAMA_HTTP_TIMEOUT_SECONDS");

    if (int.TryParse(configuredTimeout, out var seconds) && seconds > 0)
    {
        return TimeSpan.FromSeconds(seconds);
    }

    return TimeSpan.FromSeconds(defaultTimeoutSeconds);
}

static async Task<string> GenerateTextAsync(HttpClient ollamaHttpClient, string model, string prompt)
{
    var response = await ollamaHttpClient.PostAsJsonAsync("/api/generate", new
    {
        model,
        prompt,
        stream = false
    });

    response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();
    return body?.Response?.Trim() ?? string.Empty;
}

static async Task<string> GeneratePlannerTextAsync(HttpClient ollamaHttpClient, string model, string prompt)
{
    var response = await ollamaHttpClient.PostAsJsonAsync("/api/generate", new
    {
        model,
        prompt,
        stream = false,
        format = "json",
        options = new
        {
            temperature = 0,
            num_predict = 120
        }
    });

    response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();
    return body?.Response?.Trim() ?? string.Empty;
}

static async Task<string> EnsureModelAsync(HttpClient ollamaHttpClient)
{
    var configuredModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL")?.Trim();
    var preferredModel = string.IsNullOrWhiteSpace(configuredModel) ? "llama3.2" : configuredModel;

    var tagsResponse = await ollamaHttpClient.GetFromJsonAsync<OllamaTagsResponse>("/api/tags");
    var models = tagsResponse?.Models ?? [];

    if (models.Count > 0)
    {
        var exactMatch = models.FirstOrDefault(m => string.Equals(m.Name, preferredModel, StringComparison.OrdinalIgnoreCase))?.Name;
        return exactMatch ?? models[0].Name;
    }

    Console.WriteLine($"[Client] No Ollama model found. Pulling default model '{preferredModel}' (this can take a few minutes)...");

    try
    {
        var pullResponse = await ollamaHttpClient.PostAsJsonAsync("/api/pull", new
        {
            name = preferredModel,
            stream = false
        });

        pullResponse.EnsureSuccessStatusCode();

        tagsResponse = await ollamaHttpClient.GetFromJsonAsync<OllamaTagsResponse>("/api/tags");
        var pulledModelName = tagsResponse?.Models?.FirstOrDefault(m => string.Equals(m.Name, preferredModel, StringComparison.OrdinalIgnoreCase))?.Name
            ?? tagsResponse?.Models?.FirstOrDefault()?.Name;

        if (string.IsNullOrWhiteSpace(pulledModelName))
        {
            throw new InvalidOperationException("Ollama model pull completed but no model is available. Check Ollama container logs.");
        }

        return pulledModelName;
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"Failed to pull Ollama model '{preferredModel}'. Pull error: {ex.Message}", ex);
    }
}

static async Task<ToolPlan> PlanToolCallAsync(HttpClient ollamaHttpClient, string model, string userRequest)
{
    var planningPrompt = $$"""
You are an MCP tool planner.

Available tools:
1) list_todos
   - Use for requests about listing/showing todos.
2) create_todo
   - Use for requests to add/create a new todo.
   - Requires: title (string)
3) update_todo_status
   - Use for status updates.
   - Requires: id (number), status (string)

User request:
{{userRequest}}

Return exactly one compact JSON object and nothing else with this schema:
{"tool":"list_todos|create_todo|update_todo_status|none","title":"string or null","id":number or null,"status":"string or null"}
""";

    const int maxAttempts = 3;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        var raw = await GeneratePlannerTextAsync(ollamaHttpClient, model, planningPrompt);
        if (TryParseToolPlan(raw, out var plan))
        {
            return plan;
        }

        Console.WriteLine($"[Planner] Attempt {attempt}/{maxAttempts} returned non-parseable output: {Truncate(raw, 400)}");
    }

    Console.WriteLine("[Planner] Falling back to heuristic tool selection.");
    return FallbackToolPlan(userRequest);
}

static bool TryParseToolPlan(string raw, out ToolPlan plan)
{
    plan = new ToolPlan("none", null, null, null);

    if (!TryExtractJsonObject(raw, out var json))
    {
        return false;
    }

    try
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var root = document.RootElement;
        var tool = root.TryGetProperty("tool", out var toolProp) && toolProp.ValueKind == JsonValueKind.String
            ? toolProp.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(tool))
        {
            return false;
        }

        var normalizedTool = tool.Trim().ToLowerInvariant();
        if (normalizedTool is not ("list_todos" or "create_todo" or "update_todo_status" or "none"))
        {
            return false;
        }

        string? title = root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String
            ? titleProp.GetString()
            : null;

        int? id = root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number && idProp.TryGetInt32(out var parsedId)
            ? parsedId
            : null;

        string? status = root.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.String
            ? statusProp.GetString()
            : null;

        plan = new ToolPlan(normalizedTool, string.IsNullOrWhiteSpace(title) ? null : title.Trim(), id, string.IsNullOrWhiteSpace(status) ? null : status.Trim());
        return true;
    }
    catch
    {
        return false;
    }
}

static bool TryExtractJsonObject(string raw, out string json)
{
    raw = raw.Trim();

    if (raw.StartsWith("```") && raw.EndsWith("```"))
    {
        var firstNewLine = raw.IndexOf('\n');
        var lastFence = raw.LastIndexOf("```");
        if (firstNewLine >= 0 && lastFence > firstNewLine)
        {
            raw = raw[(firstNewLine + 1)..lastFence].Trim();
        }
    }

    json = raw.Trim();

    if (json.StartsWith("{") && json.EndsWith("}"))
    {
        return true;
    }

    var start = raw.IndexOf('{');
    var end = raw.LastIndexOf('}');
    if (start < 0 || end <= start)
    {
        return false;
    }

    json = raw[start..(end + 1)];
    return true;
}

static ToolPlan FallbackToolPlan(string userRequest)
{
    var text = userRequest.Trim();
    var lower = text.ToLowerInvariant();

    if (lower.Contains("list") || lower.Contains("show") || lower.Contains("what") && lower.Contains("todo"))
    {
        return new ToolPlan("list_todos", null, null, null);
    }

    if (lower.Contains("create") || lower.Contains("add") || lower.Contains("new todo"))
    {
        return new ToolPlan("create_todo", text, null, null);
    }

    return new ToolPlan("none", null, null, null);
}

static string Truncate(string value, int maxLength)
{
    if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
    {
        return value;
    }

    return value[..maxLength] + "...";
}

static async Task<string> ExecuteMcpToolAsync(HttpClient mcpHttpClient, ToolPlan plan)
{
    switch (plan.Tool.ToLowerInvariant())
    {
        case "list_todos":
            return await mcpHttpClient.GetStringAsync("/mcp/list_todos");

        case "create_todo":
            var title = string.IsNullOrWhiteSpace(plan.Title)
                ? "Follow up on MCP conversational flow"
                : plan.Title;
            var createResponse = await mcpHttpClient.PostAsJsonAsync("/mcp/create_todo", new { title });
            return await createResponse.Content.ReadAsStringAsync();

        case "update_todo_status":
            if (plan.Id is null || string.IsNullOrWhiteSpace(plan.Status))
            {
                return "{\"error\":\"Missing id or status for update_todo_status\"}";
            }

            var updateResponse = await mcpHttpClient.PostAsJsonAsync("/mcp/update_todo_status", new { id = plan.Id.Value, status = plan.Status });
            return await updateResponse.Content.ReadAsStringAsync();

        default:
            return "{\"message\":\"No tool call executed\"}";
    }
}

static async Task<string> GenerateAssistantReplyAsync(HttpClient ollamaHttpClient, string model, string userRequest, ToolPlan toolPlan, string toolResult)
{
    var assistantPrompt = $$"""
You are a helpful assistant.

User request:
{{userRequest}}

Executed tool:
{{toolPlan.Tool}}

Tool result (JSON/text):
{{toolResult}}

Respond to the user in one or two concise sentences.
""";

    return await GenerateTextAsync(ollamaHttpClient, model, assistantPrompt);
}

file sealed record ToolPlan(string Tool, string? Title, int? Id, string? Status);

file sealed record OllamaTagsResponse(List<OllamaModelInfo> Models);
file sealed record OllamaModelInfo(string Name);
file sealed record OllamaGenerateResponse(string Response);
