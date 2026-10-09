using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using PayrollApi.Core.Entities;
using PayrollApi.Core.Persistence;
using PayrollApi.Services;

namespace PayrollApi;

/// <summary>Auth endpoints: register (Admin only), login (anonymous), me.</summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/auth/register - create a new user (Admin only).
        app.MapPost("/api/auth/register", async (
            RegisterRequest request, AppDbContext db, ILogger<Program> logger) =>
        {
            string? error = ValidateRegister(request);
            if (error is not null)
            {
                logger.LogWarning("Register rejected: {Error}", error);
                return Results.BadRequest(new { error });
            }

            string username = request.Username!.Trim().ToLowerInvariant();
            bool exists = await db.Users.AnyAsync(u => u.Username == username);
            if (exists)
            {
                logger.LogWarning("Register rejected: duplicate username {Username}", username);
                return Results.BadRequest(new { error = $"Username \"{username}\" already exists." });
            }

            User user = new()
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = request.Role!.Trim().ToLowerInvariant() switch
                {
                    "admin" => UserRoles.Admin,
                    "hr" => UserRoles.HR,
                    _ => UserRoles.Viewer,
                },
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();
            logger.LogInformation("User registered: id={Id} username={Username} role={Role}",
                user.Id, user.Username, user.Role);

            return Results.Created($"/api/auth/me", ToResponse(user));
        })
        .WithName("Register")
        .RequireAuthorization(UserRoles.AdminOnlyPolicy);

        // POST /api/auth/login - authenticate, return a JWT token.
        app.MapPost("/api/auth/login", async (
            LoginRequest request, AppDbContext db, TokenService tokens, ILogger<Program> logger) =>
        {
            string username = request.Username?.Trim().ToLowerInvariant() ?? string.Empty;
            User? user = await db.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);

            if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                logger.LogWarning("Login rejected: invalid credentials for \"{Username}\"", username);
                return Results.Unauthorized();
            }

            logger.LogInformation("Login succeeded: username={Username} role={Role}", user.Username, user.Role);
            return Results.Ok(new { token = tokens.CreateToken(user), username = user.Username, role = user.Role });
        })
        .WithName("Login");

        // GET /api/auth/me - current user info from the token claims.
        app.MapGet("/api/auth/me", (HttpContext context) =>
        {
            return Results.Ok(new
            {
                id = int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out int id) ? id : 0,
                username = context.User.FindFirstValue(ClaimTypes.Name),
                role = context.User.FindFirstValue(ClaimTypes.Role),
            });
        })
        .WithName("Me")
        .RequireAuthorization();
    }

    private static string? ValidateRegister(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return "Username is required.";
        }

        if (request.Username.Trim().Length < 3)
        {
            return "Username must be at least 3 characters.";
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return "Password must be at least 8 characters.";
        }

        string role = request.Role?.Trim().ToLowerInvariant() ?? string.Empty;
        if (role is not ("admin" or "hr" or "viewer"))
        {
            return $"Role must be \"admin\", \"hr\" or \"viewer\" (got \"{request.Role}\").";
        }

        return null;
    }

    internal static object ToResponse(User user) => new
    {
        id = user.Id,
        username = user.Username,
        role = user.Role,
        isActive = user.IsActive,
        createdAt = user.CreatedAt,
    };
}

public record LoginRequest(string? Username, string? Password);
public record RegisterRequest(string? Username, string? Password, string? Role);

/// <summary>Canonical role constants stored on <see cref="PayrollApi.Core.Entities.User"/>.</summary>
public static class UserRoles
{
    public const string Admin = "Admin";
    public const string HR = "HR";
    public const string Viewer = "Viewer";

    /// <summary>Authorization policy: Admin OR HR (for write operations).</summary>
    public const string WritePolicy = "Writer";
    public const string AdminOnlyPolicy = "AdminOnly";
}
