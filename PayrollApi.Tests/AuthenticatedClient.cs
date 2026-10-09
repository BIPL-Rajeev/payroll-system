using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PayrollApi.Tests;

/// <summary>
/// Extension helpers for authenticated test requests:
/// login as the seeded admin, then attach "Bearer {token}" to every call.
/// </summary>
public static class AuthenticatedClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <summary>Logs in as the seeded default admin and returns the JWT (throws if login fails).</summary>
    public static async Task<string> GetAdminTokenAsync(this HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "Admin@123",
        }, JsonOpts);

        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("token").GetString()
            ?? throw new InvalidOperationException("Login response had no token.");
    }

    /// <summary>Logs in as admin and sets the Authorization header on the client.</summary>
    public static async Task LoginAsAdminAsync(this HttpClient client)
    {
        string token = await client.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
