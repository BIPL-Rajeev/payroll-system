using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PayrollApi.Tests;

/// <summary>Integration tests for the Form 16 endpoints (validation paths).</summary>
public class Form16EndpointTests : IClassFixture<PayrollApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    public Form16EndpointTests(PayrollApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static object EmployeePayload(string code) => new
    {
        employeeCode = code,
        name = "Form16 Test",
        state = "Maharashtra",
        isMetro = false,
        taxRegime = "old",
        monthlyBasic = 40_000m,
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
        pan = "ABCDE1234F",
    };

    private async Task<int> CreateEmployeeAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/employees", EmployeePayload($"F16-{Guid.NewGuid():N}"), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("id").GetInt32();
    }

    // Required: invalid FY format "2025/26" -> 400 (slash URL-encoded so routing keeps it one segment).
    [Fact]
    public async Task InvalidFinancialYear_Returns400()
    {
        int employeeId = await CreateEmployeeAsync();

        var response = await _client.GetAsync($"/api/form16/{employeeId}/2025%2F26");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string? error = body.RootElement.GetProperty("error").GetString();
        Assert.NotNull(error);
        Assert.Contains("Invalid financial year", error);
        Assert.Contains("2025-26", error);
    }

    // Required: employee with no payroll runs -> 400.
    [Fact]
    public async Task NoPayrollRuns_Returns400()
    {
        int employeeId = await CreateEmployeeAsync();

        var response = await _client.GetAsync($"/api/form16/{employeeId}/2025-26");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string? error = body.RootElement.GetProperty("error").GetString();
        Assert.NotNull(error);
        Assert.Contains("No payroll runs", error);
    }

    // Non-existent employee -> 404.
    [Fact]
    public async Task UnknownEmployee_Returns404()
    {
        var response = await _client.GetAsync("/api/form16/999999/2025-26");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("not found", body.RootElement.GetProperty("error").GetString());
    }

    // PDF endpoint also validates the FY format.
    [Fact]
    public async Task Pdf_InvalidFinancialYear_Returns400()
    {
        int employeeId = await CreateEmployeeAsync();

        var response = await _client.GetAsync($"/api/form16/{employeeId}/2025%2F26/pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
