using System.Net.Http.Json;

Console.WriteLine("[Client] Booting .NET AI Orchestrator...");

using var mcpHttpClient = new HttpClient { BaseAddress = new Uri("http://mcp-server:6000") };
using var ollamaHttpClient = new HttpClient { BaseAddress = new Uri("http://ollama-container:11434") };

Console.WriteLine("[Client] Checking MCP server health...");
var health = await mcpHttpClient.GetStringAsync("/health");
Console.WriteLine($"[MCP] {health}");

Console.WriteLine("[Client] Listing todos...");
var before = await mcpHttpClient.GetStringAsync("/mcp/list_todos");
Console.WriteLine($"[Before] {before}");

Console.WriteLine("[Client] Creating todo...");
var createResponse = await mcpHttpClient.PostAsJsonAsync("/mcp/create_todo", new
{
    title = "Verify C# Contracts"
});
Console.WriteLine($"[Create] {(int)createResponse.StatusCode}");
Console.WriteLine(await createResponse.Content.ReadAsStringAsync());

Console.WriteLine("[Client] Listing todos after create...");
var after = await mcpHttpClient.GetStringAsync("/mcp/list_todos");
Console.WriteLine($"[After] {after}");

Console.WriteLine("[Client] Checking Ollama endpoint...");
var ollamaTags = await ollamaHttpClient.GetStringAsync("/api/tags");
Console.WriteLine($"[Ollama] {ollamaTags}");

Console.WriteLine("[Client] Done.");
