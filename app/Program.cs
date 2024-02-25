using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ITodoService>(new InMemoryTodoService());

var app = builder.Build();

// middleware, piece of code that is executed before and/or after a request is processed
app.UseRewriter(new RewriteOptions().AddRedirect("tasks/(.*)", "todos/$1")); // middlewares run in the application pipeline

// custom middleware
app.Use(async (context, next) => {
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] started...");
    await next(context); // call the next middleware in the pipeline
    Console.WriteLine($"[{context.Request.Method} {context.Request.Path} {DateTime.UtcNow}] finished...");
});

var todos = new List<Todo>();

// get all todos
app.MapGet("/todos", (ITodoService service) => service.GetTodos());

// get a todo by id
app.MapGet("/todos/{id}", Results<Ok<Todo>, NotFound> (int id, ITodoService service) => 
{
    var targetTodo = service.GetTodoById(id);
    return targetTodo is null
        ? TypedResults.NotFound()
        : TypedResults.Ok(targetTodo);
});

// add a todo
app.MapPost("/todos", (Todo todo, ITodoService service) => 
{
    service.AddTodo(todo);
    return TypedResults.Created("/todos/{id}", todo);
})
// endpoint filter for todo validation
.AddEndpointFilter(async (context, next) => { // endpoint filters run in the context of the endpoint
    var todoArgument = context.GetArgument<Todo>(0);
    var errors = new Dictionary<string, string[]>();

    if(todoArgument.DueDate < DateTime.UtcNow){
        errors.Add(nameof(Todo.DueDate), ["Cannot have due date in the past."]);
    }
    if(todoArgument.IsCompleted){
        errors.Add(nameof(Todo.IsCompleted), ["Cannot add completed to-do."]);
    }
    if(errors.Count > 0){
        return Results.ValidationProblem(errors);
    }

    return await next(context);
});

// delete a todo by id
app.MapDelete("/todos/{id}", (int id, ITodoService service) => 
{
    service.DeleteTodoById(id);
    return TypedResults.NoContent();
});

app.Run();

public record Todo(int Id, string Name, DateTime DueDate, bool IsCompleted);

interface ITodoService
{
    Todo? GetTodoById(int id);
    List<Todo> GetTodos();
    void DeleteTodoById(int id);
    Todo AddTodo(Todo todo);
}

class InMemoryTodoService : ITodoService
{
    private readonly List<Todo> _todos = [];

    public Todo AddTodo(Todo todo) 
    {
        _todos.Add(todo);
        return todo;
    }

    public Todo? GetTodoById(int id)
    {
        return _todos.SingleOrDefault(todo => id == todo.Id);
    }

    public void DeleteTodoById(int id)
    {
        _todos.RemoveAll(todo => id == todo.Id);
    }

    public List<Todo> GetTodos()
    {
        return _todos;
    }
}