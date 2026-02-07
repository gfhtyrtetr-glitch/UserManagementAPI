using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace UserManagementAPI.Middleware;

public sealed class TokenAuthenticationMiddleware
{
    private const string BearerPrefix = "Bearer ";
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenAuthenticationMiddleware> _logger;
    private readonly HashSet<string> _validTokens;

    public TokenAuthenticationMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<TokenAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;

        var tokens = configuration
            .GetSection("Authentication:Tokens")
            .Get<string[]>()
            ?? Array.Empty<string>();

        _validTokens = tokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .ToHashSet(StringComparer.Ordinal);

        if (_validTokens.Count == 0)
        {
            _logger.LogWarning("No authentication tokens configured; all API requests will be rejected.");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsApiRequest(context))
        {
            await _next(context);
            return;
        }

        if (!TryGetToken(context, out var token) || !_validTokens.Contains(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized." });
            return;
        }

        await _next(context);
    }

    private static bool IsApiRequest(HttpContext context)
    {
        return context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetToken(HttpContext context, out string token)
    {
        token = string.Empty;

        if (!context.Request.Headers.TryGetValue("Authorization", out var authorization))
        {
            return false;
        }

        var header = authorization.ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = header[BearerPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }
}
