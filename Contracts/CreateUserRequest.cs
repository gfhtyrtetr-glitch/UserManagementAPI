namespace UserManagementAPI.Contracts;

public sealed record CreateUserRequest(
    string? FirstName,
    string? LastName,
    string? Email,
    string? Department,
    string? Title,
    string? Phone,
    bool? IsActive
);
