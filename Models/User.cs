namespace UserManagementAPI.Models;

public sealed record User(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Department,
    string? Title,
    string? Phone,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
