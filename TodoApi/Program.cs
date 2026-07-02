using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var todos = new ConcurrentDictionary<int, Todo>();
todos[1] = new Todo(1, "Review Microsoft Learn Guidance", "Pending");
todos[2] = new Todo(2, "Build pure .NET MCP Server", "Completed");

int nextId = 3;

app.MapGet("/todos", () => todos.Values.ToList());
app.MapPost("/todos", (CreateTodoDto dto) => {
    var newTodo = new Todo(nextId++, dto.Title, "Pending");
    todos[newTodo.Id] = newTodo;
    return Results.Created($"/todos/{newTodo.Id}", newTodo);
});
app.MapPut("/todos/{id:int}/status", (int id, UpdateStatusDto dto) => {
    if (!todos.TryGetValue(id, out var todo)) return Results.NotFound();
    var updated = todo with { Status = dto.Status };
    todos[id] = updated;
    return Results.Ok(updated);
});

app.Run("http://0.0.0.0:5000");

public record Todo(int Id, string Title, string Status);
public record CreateTodoDto(string Title);
public record UpdateStatusDto(string Status);