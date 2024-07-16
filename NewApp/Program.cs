using System.Data.Common;
using System.Formats.Tar;
using System.Reflection.Metadata.Ecma335;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ITaskService>(new InMemoryTaskService());
var app = builder.Build();

var todos = new List<Todo>();

app.Use(async (context, next) =>
{
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Started");
    await next(context);
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] Finished");
});
app.MapGet("/todos", (ITaskService service) => service.GetTodos());
app.MapGet("/todos/{id}", Results<Ok<Todo>, NotFound> (int id) =>
{
    var targetTodo = todos.SingleOrDefault(t => id == t.ID);
    return targetTodo is null ? TypedResults.NotFound() : TypedResults.Ok(targetTodo);

});
app.MapPost("/todos", (Todo task, ITaskService service) =>
{
    service.AddTodo(task);
    return TypedResults.Created("/todos/{id}", task);

}).AddEndpointFilter(async (context, next) =>
{
    var taskArguments = context.GetArgument<Todo>(0);
    var errors = new Dictionary<string, string[]>();

    if (taskArguments.DueDate < DateTime.UtcNow)
    {
        errors.Add(nameof(Todo.DueDate), ["Cannot have due date in the past"]);
    }
    if (taskArguments.IsCompleted)
    {
        errors.Add(nameof(Todo.IsCompleted), ["Cannot add completed Todos"]);
    }

    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    return await next(context);
});

app.MapDelete("/todos/{id}", (int id, ITaskService service) =>
{
    service.DeleteTodoByID(id);
    return TypedResults.NoContent();
});
app.Run();


record Todo(int ID, string Name, DateTime DueDate, bool IsCompleted);

interface ITaskService
{
    Todo? GetTodoByID(int id);
    List<Todo> GetTodos();
    void DeleteTodoByID(int id);
    Todo AddTodo(Todo task);
}

class InMemoryTaskService : ITaskService
{
    private readonly List<Todo> _todos = [];

    public Todo AddTodo(Todo task)
    {
        _todos.Add(task);
        return task;
    }
    public void DeleteTodoByID(int id)
    {
        _todos.RemoveAll(task => id == task.ID);
    }
}