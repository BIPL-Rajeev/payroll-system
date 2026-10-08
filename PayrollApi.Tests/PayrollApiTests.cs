using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PayrollApi.Tests;

public class PayrollApiTests : IClassFixture<PayrollApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    public PayrollApiTests(PayrollApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // Valid payload: Maharashtra, Rs 50,000 basic + 20,000 HRA + 5,000 DA + 5,000 special,
    // old regime, January -> net pay 74,533 (same as the PayrollCalculator scenario 1).
    private static object ValidPayload() => new
    {
        employeeId = "EMP001",
        name = "Asha Patil",
        state = "Maharashtra",
        isMetro = true,
        taxRegime = "old",
        monthlyBasic = 50_000m,
        monthlyHra = 20_000m,
        monthlyDa = 5_000m,
        monthlySpecialAllowance = 5_000m,
        monthlyLta = 0m,
        monthlyOtherAllowances = 0m,
        annualRentPaid = 180_000m,
        section80C = 150_000m,
        section80CCD1B = 0m,
        section80D = 0m,
        section80TTA = 0m,
        homeLoanInterest = 0m,
        isFirstYearEmployee = false,
        hasDisability = false,
        monthNumber = 1,
    };

    [Fact]
    public async Task Health_ReturnsOkWithStatus()
    {
        var response = await _client.GetAsync("/api/payroll/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Calculate_ValidInput_Returns200WithCorrectNetPay()
    {
        var response = await _client.PostAsJsonAsync("/api/payroll/calculate", ValidPayload(), Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(80_000m, root.GetProperty("grossMonthlySalary").GetDecimal());
        Assert.Equal(1_800m, root.GetProperty("employeePf").GetDecimal());
        Assert.Equal(0m, root.GetProperty("employeeEsi").GetDecimal());
        Assert.Equal(200m, root.GetProperty("professionalTax").GetDecimal());
        Assert.Equal(3_467m, root.GetProperty("tds").GetDecimal());
        Assert.Equal(5_467m, root.GetProperty("totalDeductions").GetDecimal());
        Assert.Equal(74_533m, root.GetProperty("netMonthlyTakeHome").GetDecimal());
        Assert.Equal(81_950m, root.GetProperty("totalCtc").GetDecimal());
    }

    [Fact]
    public async Task Calculate_InvalidState_Returns400()
    {
        object payload = new
        {
            employeeId = "EMP999",
            name = "Test User",
            state = "Gujarat",
            isMetro = false,
            taxRegime = "new",
            monthlyBasic = 50_000m,
            monthlyHra = 0m,
            monthlyDa = 0m,
            monthlySpecialAllowance = 0m,
            monthlyLta = 0m,
            monthlyOtherAllowances = 0m,
            annualRentPaid = 0m,
            section80C = 0m,
            section80CCD1B = 0m,
            section80D = 0m,
            section80TTA = 0m,
            homeLoanInterest = 0m,
            isFirstYearEmployee = false,
            hasDisability = false,
            monthNumber = 1,
        };

        var response = await _client.PostAsJsonAsync("/api/payroll/calculate", payload, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string? error = doc.RootElement.GetProperty("error").GetString();
        Assert.NotNull(error);
        Assert.Contains("Gujarat", error);
        Assert.Contains("Supported states", error);
    }

    [Fact]
    public async Task Calculate_NegativeSalary_Returns400()
    {
        object payload = new
        {
            employeeId = "EMP004",
            name = "Negative Salary",
            state = "Karnataka",
            isMetro = false,
            taxRegime = "new",
            monthlyBasic = -1_000m,
            monthlyHra = 0m,
            monthlyDa = 0m,
            monthlySpecialAllowance = 0m,
            monthlyLta = 0m,
            monthlyOtherAllowances = 0m,
            annualRentPaid = 0m,
            section80C = 0m,
            section80CCD1B = 0m,
            section80D = 0m,
            section80TTA = 0m,
            homeLoanInterest = 0m,
            isFirstYearEmployee = false,
            hasDisability = false,
            monthNumber = 1,
        };

        var response = await _client.PostAsJsonAsync("/api/payroll/calculate", payload, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("non-negative", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Calculate_MonthOutOfRange_Returns400()
    {
        object payload = new
        {
            employeeId = "EMP005",
            name = "Bad Month",
            state = "Tamil Nadu",
            isMetro = false,
            taxRegime = "new",
            monthlyBasic = 50_000m,
            monthlyHra = 0m,
            monthlyDa = 0m,
            monthlySpecialAllowance = 0m,
            monthlyLta = 0m,
            monthlyOtherAllowances = 0m,
            annualRentPaid = 0m,
            section80C = 0m,
            section80CCD1B = 0m,
            section80D = 0m,
            section80TTA = 0m,
            homeLoanInterest = 0m,
            isFirstYearEmployee = false,
            hasDisability = false,
            monthNumber = 13,
        };

        var response = await _client.PostAsJsonAsync("/api/payroll/calculate", payload, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("between 1 and 12", doc.RootElement.GetProperty("error").GetString());
    }
}
