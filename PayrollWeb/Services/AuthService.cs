using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PayrollWeb.Services;

/// <summary>Holds the JWT token + basic user info for the Blazor session (scoped, per circuit).</summary>
public sealed class AuthState
{
    public string? Token { get; private set; }
    public string? Username { get; private set; }
    public string? Role { get; private set; }

    public bool IsLoggedIn => Token is not null;

    public void SetLogin(string token, string username, string role)
    {
        Token = token;
        Username = username;
        Role = role;
    }

    public void Clear()
    {
        Token = null;
        Username = null;
        Role = null;
    }
}

/// <summary>
/// DelegatingHandler that attaches "Authorization: Bearer {token}" to every
/// API request made through the typed HttpClient.
/// </summary>
public sealed class AuthTokenHandler(AuthState auth) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (auth.Token is { } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>Login/logout calls against the API auth endpoints.</summary>
public sealed class AuthService(HttpClient http, AuthState auth)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Login and populate AuthState. Throws ApiException on 401/400.</summary>
    public async Task LoginAsync(string username, string password)
    {
        var response = await http.PostAsJsonAsync("/api/auth/login", new { username, password }, Json);
        await ApiClient.EnsureSuccessPublicAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(Json)
            ?? throw new ApiException("Empty login response.");

        auth.SetLogin(payload.Token, payload.Username, payload.Role);
    }

    public void Logout() => auth.Clear();

    private sealed record LoginResponse(string Token, string Username, string Role);
}
