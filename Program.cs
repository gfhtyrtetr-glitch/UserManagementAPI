using System.ComponentModel.DataAnnotations;
using UserManagementAPI.Contracts;
using UserManagementAPI.Data;
using UserManagementAPI.Middleware;
using UserManagementAPI.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<TokenAuthenticationMiddleware>();
app.UseMiddleware<RequestResponseLoggingMiddleware>();

app.UseHttpsRedirection();

var users = app.MapGroup("/api/users");

users.MapGet("/", (IUserRepository repo) => Results.Ok(repo.GetAll()))
    .WithName("GetUsers");

users.MapGet("/{id:guid}", (Guid id, IUserRepository repo) =>
    {
        var user = repo.GetById(id);
        return user is null ? Results.NotFound() : Results.Ok(user);
    })
    .WithName("GetUserById");

users.MapPost("/", (CreateUserRequest request, IUserRepository repo) =>
    {
        var validation = ValidateCreate(request);
        if (validation.Count > 0)
        {
            return Results.ValidationProblem(validation);
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User(
            Guid.NewGuid(),
            request.FirstName!.Trim(),
            request.LastName!.Trim(),
            request.Email!.Trim(),
            request.Department!.Trim(),
            NormalizeOptional(request.Title),
            NormalizeOptional(request.Phone),
            request.IsActive ?? true,
            now,
            now);

        repo.Create(user);
        return Results.Created($"/api/users/{user.Id}", user);
    })
    .WithName("CreateUser");

users.MapPut("/{id:guid}", (Guid id, UpdateUserRequest request, IUserRepository repo) =>
    {
        var validation = ValidateUpdate(request);
        if (validation.Count > 0)
        {
            return Results.ValidationProblem(validation);
        }

        var existing = repo.GetById(id);
        if (existing is null)
        {
            return Results.NotFound();
        }

        var updated = existing with
        {
            FirstName = request.FirstName?.Trim() ?? existing.FirstName,
            LastName = request.LastName?.Trim() ?? existing.LastName,
            Email = request.Email?.Trim() ?? existing.Email,
            Department = request.Department?.Trim() ?? existing.Department,
            Title = request.Title is null ? existing.Title : NormalizeOptional(request.Title),
            Phone = request.Phone is null ? existing.Phone : NormalizeOptional(request.Phone),
            IsActive = request.IsActive ?? existing.IsActive,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        repo.Update(updated);
        return Results.Ok(updated);
    })
    .WithName("UpdateUser");

users.MapDelete("/{id:guid}", (Guid id, IUserRepository repo) =>
    {
        return repo.Delete(id) ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteUser");

app.Run();

static Dictionary<string, string[]> ValidateCreate(CreateUserRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.FirstName))
    {
        errors["firstName"] = ["First name is required."];
    }

    if (string.IsNullOrWhiteSpace(request.LastName))
    {
        errors["lastName"] = ["Last name is required."];
    }

    if (string.IsNullOrWhiteSpace(request.Email))
    {
        errors["email"] = ["Email is required."];
    }
    else if (!IsValidEmail(request.Email))
    {
        errors["email"] = ["Email is not valid."];
    }

    if (string.IsNullOrWhiteSpace(request.Department))
    {
        errors["department"] = ["Department is required."];
    }

    if (request.Title is not null && string.IsNullOrWhiteSpace(request.Title))
    {
        errors["title"] = ["Title cannot be empty."];
    }

    if (request.Phone is not null && string.IsNullOrWhiteSpace(request.Phone))
    {
        errors["phone"] = ["Phone cannot be empty."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateUpdate(UpdateUserRequest request)
{
    var errors = new Dictionary<string, string[]>();
    var hasAny = false;

    if (request.FirstName is not null)
    {
        hasAny = true;
        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            errors["firstName"] = ["First name cannot be empty."];
        }
    }

    if (request.LastName is not null)
    {
        hasAny = true;
        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            errors["lastName"] = ["Last name cannot be empty."];
        }
    }

    if (request.Email is not null)
    {
        hasAny = true;
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = ["Email cannot be empty."];
        }
        else if (!IsValidEmail(request.Email))
        {
            errors["email"] = ["Email is not valid."];
        }
    }

    if (request.Department is not null)
    {
        hasAny = true;
        if (string.IsNullOrWhiteSpace(request.Department))
        {
            errors["department"] = ["Department cannot be empty."];
        }
    }

    if (request.Title is not null)
    {
        hasAny = true;
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            errors["title"] = ["Title cannot be empty."];
        }
    }

    if (request.Phone is not null)
    {
        hasAny = true;
        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            errors["phone"] = ["Phone cannot be empty."];
        }
    }

    if (request.IsActive is not null)
    {
        hasAny = true;
    }

    if (!hasAny)
    {
        errors["request"] = ["At least one field must be provided for update."];
    }

    return errors;
}

static string? NormalizeOptional(string? value)
{
    return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

static bool IsValidEmail(string email)
{
    return new EmailAddressAttribute().IsValid(email);
}
