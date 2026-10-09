using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace PayrollWeb.Services;

/// <summary>A binary file downloaded from the API (xlsx / pdf) plus its server-suggested filename.</summary>
public sealed record ApiFileDownload(byte[] Content, string? FileName)
{
    public string FileNameOr(string fallback) =>
        string.IsNullOrWhiteSpace(FileName) ? fallback : FileName;
}

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

    /// <summary>Fetches a binary export and triggers a browser download via JS interop.</summary>
    public async Task DownloadFileAsync(IJSRuntime js, string url, string fallbackName)
    {
        var response = await http.GetAsync(url);
        await ApiClient.EnsureSuccessPublicAsync(response);

        string? fileName = response.Content.Headers.ContentDisposition?.FileName;
        var download = new ApiFileDownload(
            await response.Content.ReadAsByteArrayAsync(), fileName);

        await js.InvokeVoidAsync(
            "downloadFile",
            download.FileNameOr(fallbackName),
            response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream",
            Convert.ToBase64String(download.Content));
    }

    private sealed record LoginResponse(string Token, string Username, string Role);
}
