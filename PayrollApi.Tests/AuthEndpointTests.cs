using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PayrollApi.Tests;

/// <summary>Integration tests for the JWT auth endpoints and endpoint protection.</summary>
public class AuthEndpointTests : IClassFixture<PayrollApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;
    private readonly PayrollApiFactory _factory;

    public AuthEndpointTests(PayrollApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // Required: login with valid credentials -> 200 with a JWT token.
    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "Admin@123",
        }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string? token = body.RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal("admin", body.RootElement.GetProperty("username").GetString());
        Assert.Equal("Admin", body.RootElement.GetProperty("role").GetString());
    }

    // Required: login with wrong password -> 401.
    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "wrong-password",
        }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Required: access protected endpoint without token -> 401.
    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Required: access protected endpoint with a valid token -> 200.
    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Returns200()
    {
        var client = _factory.CreateClient();
        await client.LoginAsAdminAsync();

        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Required: register a new user as Admin -> 2xx (201 Created).
    [Fact]
    public async Task Register_AsAdmin_Succeeds()
    {
        var client = _factory.CreateClient();
        await client.LoginAsAdminAsync();

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"hr-user-{Guid.NewGuid():N}",
            password = "HrPassword@1",
            role = "hr",
        }, Json);

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
            $"Expected 200/201 but got {response.StatusCode}");
    }

    // Required: register a new user without the Admin role -> 403.
    [Fact]
    public async Task Register_WithoutAdminRole_Returns403()
    {
        // Create a Viewer via the admin, then log in as that viewer.
        string viewerName = $"viewer-{Guid.NewGuid():N}";
        var adminClient = _factory.CreateClient();
        await adminClient.LoginAsAdminAsync();

        var createResponse = await adminClient.PostAsJsonAsync("/api/auth/register", new
        {
            username = viewerName,
            password = "ViewerPass@1",
            role = "viewer",
        }, Json);
        createResponse.EnsureSuccessStatusCode();

        // Anonymous (no token at all) also counts as "without Admin role" -> 401/403;
        // a logged-in non-admin is the exact scenario -> 403.
        var viewerClient = _factory.CreateClient();
        var login = await viewerClient.PostAsJsonAsync("/api/auth/login", new
        {
            username = viewerName,
            password = "ViewerPass@1",
        }, Json);
        login.EnsureSuccessStatusCode();

        using var loginBody = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        string viewerToken = loginBody.RootElement.GetProperty("token").GetString()!;
        viewerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", viewerToken);

        var register = await viewerClient.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"should-fail-{Guid.NewGuid():N}",
            password = "Whatever@1",
            role = "viewer",
        }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, register.StatusCode);

        // Sanity: a totally anonymous register attempt must not succeed either.
        var anonymous = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            username = $"anon-{Guid.NewGuid():N}",
            password = "Whatever@1",
            role = "viewer",
        }, Json);
        Assert.True(
            anonymous.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401/403 for anonymous register but got {anonymous.StatusCode}");
    }

    // Extra: GET /api/auth/me returns the current user's claims.
    [Fact]
    public async Task Me_WithToken_ReturnsCurrentUser()
    {
        var client = _factory.CreateClient();
        await client.LoginAsAdminAsync();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("admin", body.RootElement.GetProperty("username").GetString());
        Assert.Equal("Admin", body.RootElement.GetProperty("role").GetString());
    }

    // Extra: write endpoints (POST employees) reject Viewers with 403.
    [Fact]
    public async Task CreateEmployee_AsViewer_Returns403()
    {
        string viewerName = $"viewer2-{Guid.NewGuid():N}";
        var adminClient = _factory.CreateClient();
        await adminClient.LoginAsAdminAsync();
        await adminClient.PostAsJsonAsync("/api/auth/register", new
        {
            username = viewerName,
            password = "ViewerPass@2",
            role = "viewer",
        }, Json);

        var viewerClient = _factory.CreateClient();
        var login = await viewerClient.PostAsJsonAsync("/api/auth/login", new
        {
            username = viewerName,
            password = "ViewerPass@2",
        }, Json);
        using var loginBody = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        viewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", loginBody.RootElement.GetProperty("token").GetString());

        var response = await viewerClient.PostAsJsonAsync("/api/employees", new
        {
            employeeCode = $"V-{Guid.NewGuid():N}",
            name = "Viewer Attempt",
            state = "Maharashtra",
            isMetro = false,
            taxRegime = "old",
            monthlyBasic = 30_000m,
            monthlyHra = 0m,
            monthlyDa = 0m,
            monthlySpecialAllowance = 0m,
            monthlyLta = 0m,
            monthlyOtherAllowances = 0m,
            annualRentPaid = 0m,
            investment80C = 0m,
            investment80CCD1B = 0m,
            investment80D = 0m,
            investment80TTA = 0m,
            homeLoanInterest = 0m,
            isFirstYearEmployee = false,
            hasDisability = false,
        }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
