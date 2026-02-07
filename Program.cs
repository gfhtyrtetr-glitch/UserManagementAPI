using System.ComponentModel.DataAnnotations;
using UserManagementAPI.Contracts;
using UserManagementAPI.Data;
using UserManagementAPI.Middleware;
using UserManagementAPI.Models;

const int DefaultPageSize = 50;
const int MaxPageSize = 200;
const int MaxNameLength = 100;
const int MaxEmailLength = 320;
const int MaxDepartmentLength = 200;
const int MaxTitleLength = 100;
const int MaxPhoneLength = 30;

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

users.MapGet("/", (int? skip, int? take, IUserRepository repo) =>
    {
        var safeSkip = Math.Max(skip ?? 0, 0);
        var safeTake = Math.Clamp(take ?? DefaultPageSize, 1, MaxPageSize);
        var allUsers = repo.GetAll();
        var items = allUsers.Skip(safeSkip).Take(safeTake).ToList();

        return Results.Ok(new
        {
            items,
            total = allUsers.Count,
            skip = safeSkip,
            take = safeTake
        });
    })
    .WithName("GetUsers");

users.MapGet("/{id:guid}", (Guid id, IUserRepository repo) =>
    {
        var user = repo.GetById(id);
        return user is null
            ? Results.NotFound(new { error = "User not found." })
            : Results.Ok(user);
    })
    .WithName("GetUserById");

users.MapPost("/", (CreateUserRequest? request, IUserRepository repo) =>
    {
        if (request is null)
        {
            return Results.BadRequest(new { error = "Request body is required." });
        }

        var validation = ValidateCreate(request);
        if (validation.Count > 0)
        {
            return Results.ValidationProblem(validation);
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User(
            Guid.NewGuid(),
            NormalizeRequired(request.FirstName)!,
            NormalizeRequired(request.LastName)!,
            NormalizeRequired(request.Email)!,
            NormalizeRequired(request.Department)!,
            NormalizeOptional(request.Title),
            NormalizeOptional(request.Phone),
            request.IsActive ?? true,
            now,
            now);

        repo.Create(user);
        return Results.Created($"/api/users/{user.Id}", user);
    })
    .WithName("CreateUser");

users.MapPut("/{id:guid}", (Guid id, UpdateUserRequest? request, IUserRepository repo) =>
    {
        if (request is null)
        {
            return Results.BadRequest(new { error = "Request body is required." });
        }

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

        var normalizedFirstName = NormalizeOptional(request.FirstName);
        var normalizedLastName = NormalizeOptional(request.LastName);
        var normalizedEmail = NormalizeOptional(request.Email);
        var normalizedDepartment = NormalizeOptional(request.Department);
        var normalizedTitle = NormalizeOptional(request.Title);
        var normalizedPhone = NormalizeOptional(request.Phone);

        var updated = existing with
        {
            FirstName = request.FirstName is null ? existing.FirstName : normalizedFirstName!,
            LastName = request.LastName is null ? existing.LastName : normalizedLastName!,
            Email = request.Email is null ? existing.Email : normalizedEmail!,
            Department = request.Department is null ? existing.Department : normalizedDepartment!,
            Title = request.Title is null ? existing.Title : normalizedTitle,
            Phone = request.Phone is null ? existing.Phone : normalizedPhone,
            IsActive = request.IsActive ?? existing.IsActive,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        if (!repo.Update(updated))
        {
            return Results.NotFound(new { error = "User not found." });
        }

        return Results.Ok(updated);
    })
    .WithName("UpdateUser");

users.MapDelete("/{id:guid}", (Guid id, IUserRepository repo) =>
    {
        return repo.Delete(id)
            ? Results.NoContent()
            : Results.NotFound(new { error = "User not found." });
    })
    .WithName("DeleteUser");

app.Run();

static Dictionary<string, string[]> ValidateCreate(CreateUserRequest request)
{
    var errors = new Dictionary<string, string[]>();
    var firstName = NormalizeRequired(request.FirstName);
    var lastName = NormalizeRequired(request.LastName);
    var email = NormalizeRequired(request.Email);
    var department = NormalizeRequired(request.Department);
    var title = NormalizeOptional(request.Title);
    var phone = NormalizeOptional(request.Phone);

    if (firstName is null)
    {
        errors["firstName"] = ["First name is required."];
    }
    else if (firstName.Length > MaxNameLength)
    {
        errors["firstName"] = [$"First name must be {MaxNameLength} characters or fewer."];
    }

    if (lastName is null)
    {
        errors["lastName"] = ["Last name is required."];
    }
    else if (lastName.Length > MaxNameLength)
    {
        errors["lastName"] = [$"Last name must be {MaxNameLength} characters or fewer."];
    }

    if (email is null)
    {
        errors["email"] = ["Email is required."];
    }
    else if (email.Length > MaxEmailLength)
    {
        errors["email"] = [$"Email must be {MaxEmailLength} characters or fewer."];
    }
    else if (!IsValidEmail(email))
    {
        errors["email"] = ["Email is not valid."];
    }

    if (department is null)
    {
        errors["department"] = ["Department is required."];
    }
    else if (department.Length > MaxDepartmentLength)
    {
        errors["department"] = [$"Department must be {MaxDepartmentLength} characters or fewer."];
    }

    if (request.Title is not null && title is null)
    {
        errors["title"] = ["Title cannot be empty."];
    }
    else if (title is not null && title.Length > MaxTitleLength)
    {
        errors["title"] = [$"Title must be {MaxTitleLength} characters or fewer."];
    }

    if (request.Phone is not null && phone is null)
    {
        errors["phone"] = ["Phone cannot be empty."];
    }
    else if (phone is not null && phone.Length > MaxPhoneLength)
    {
        errors["phone"] = [$"Phone must be {MaxPhoneLength} characters or fewer."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateUpdate(UpdateUserRequest request)
{
    var errors = new Dictionary<string, string[]>();
    var hasAny = false;
    var firstName = NormalizeOptional(request.FirstName);
    var lastName = NormalizeOptional(request.LastName);
    var email = NormalizeOptional(request.Email);
    var department = NormalizeOptional(request.Department);
    var title = NormalizeOptional(request.Title);
    var phone = NormalizeOptional(request.Phone);

    if (request.FirstName is not null)
    {
        hasAny = true;
        if (firstName is null)
        {
            errors["firstName"] = ["First name cannot be empty."];
        }
        else if (firstName.Length > MaxNameLength)
        {
            errors["firstName"] = [$"First name must be {MaxNameLength} characters or fewer."];
        }
    }

    if (request.LastName is not null)
    {
        hasAny = true;
        if (lastName is null)
        {
            errors["lastName"] = ["Last name cannot be empty."];
        }
        else if (lastName.Length > MaxNameLength)
        {
            errors["lastName"] = [$"Last name must be {MaxNameLength} characters or fewer."];
        }
    }

    if (request.Email is not null)
    {
        hasAny = true;
        if (email is null)
        {
            errors["email"] = ["Email cannot be empty."];
        }
        else if (email.Length > MaxEmailLength)
        {
            errors["email"] = [$"Email must be {MaxEmailLength} characters or fewer."];
        }
        else if (!IsValidEmail(email))
        {
            errors["email"] = ["Email is not valid."];
        }
    }

    if (request.Department is not null)
    {
        hasAny = true;
        if (department is null)
        {
            errors["department"] = ["Department cannot be empty."];
        }
        else if (department.Length > MaxDepartmentLength)
        {
            errors["department"] = [$"Department must be {MaxDepartmentLength} characters or fewer."];
        }
    }

    if (request.Title is not null)
    {
        hasAny = true;
        if (title is null)
        {
            errors["title"] = ["Title cannot be empty."];
        }
        else if (title.Length > MaxTitleLength)
        {
            errors["title"] = [$"Title must be {MaxTitleLength} characters or fewer."];
        }
    }

    if (request.Phone is not null)
    {
        hasAny = true;
        if (phone is null)
        {
            errors["phone"] = ["Phone cannot be empty."];
        }
        else if (phone.Length > MaxPhoneLength)
        {
            errors["phone"] = [$"Phone must be {MaxPhoneLength} characters or fewer."];
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

static string? NormalizeRequired(string? value)
{
    return NormalizeOptional(value);
}

static bool IsValidEmail(string email)
{
    return new EmailAddressAttribute().IsValid(email);
}
