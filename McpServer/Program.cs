using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient("TodoClient", c => c.BaseAddress = new Uri("http://todo-api:5000/"));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/mcp/list_todos", async (IHttpClientFactory clientFactory) =>
{
    var client = clientFactory.CreateClient("TodoClient");
    var response = await client.GetStringAsync("/todos");
    return Results.Text(response, "application/json");
});

app.MapPost("/mcp/create_todo", async (CreateTodoArgs args, IHttpClientFactory clientFactory) =>
{
    var client = clientFactory.CreateClient("TodoClient");
    var response = await client.PostAsJsonAsync("/todos", new { title = args.Title });
    var content = await response.Content.ReadAsStringAsync();
    return Results.Text(content, "application/json", statusCode: (int)response.StatusCode);
});

app.MapPost("/mcp/update_todo_status", async (UpdateTodoArgs args, IHttpClientFactory clientFactory) =>
{
    var client = clientFactory.CreateClient("TodoClient");
    var response = await client.PutAsJsonAsync($"/todos/{args.Id}/status", new { status = args.Status });
    var content = await response.Content.ReadAsStringAsync();
    return Results.Text(content, "application/json", statusCode: (int)response.StatusCode);
});

app.Run("http://0.0.0.0:6000");

// Parameter arguments mapped natively via system JSON serializers
public record CreateTodoArgs(string Title);
public record UpdateTodoArgs(int Id, string Status);
