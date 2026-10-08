using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PayrollApi.Core;
using Xunit;
using CoreEmployee = PayrollApi.Core.Employee;

namespace PayrollApi.Tests;

public class PayrollPersistenceTests : IClassFixture<PayrollApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    public PayrollPersistenceTests(PayrollApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // Scenario payload: Maharashtra, Rs 50,000 basic + 20,000 HRA + 5,000 DA + 5,000 special,
    // old regime, metro, rent 1,80,000, 80C 1,50,000.
    private static object EmployeePayload(string code) => new
    {
        employeeCode = code,
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
        investment80C = 150_000m,
        investment80CCD1B = 0m,
        investment80D = 0m,
        investment80TTA = 0m,
        homeLoanInterest = 0m,
        isFirstYearEmployee = false,
        hasDisability = false,
    };

    // The same inputs as a calculation-model Employee, for independent verification.
    private static CoreEmployee CalcInput(int month) => new(
        EmployeeId: "EMP",
        Name: "Asha Patil",
        State: PtState.Maharashtra,
        IsMetro: true,
        TaxRegime: TaxRegime.Old,
        MonthlyBasic: 50_000m,
        MonthlyHra: 20_000m,
        MonthlyDa: 5_000m,
        MonthlySpecialAllowance: 5_000m,
        MonthlyLta: 0m,
        MonthlyOtherAllowances: 0m,
        AnnualRentPaid: 180_000m,
        Section80C: 150_000m,
        Section80CCD1B: 0m,
        Section80D: 0m,
        Section80TTA: 0m,
        HomeLoanInterest: 0m,
        IsFirstYearEmployee: false,
        HasDisability: false,
        MonthNumber: month);

    // Required test 1: create employee -> run payroll -> fetch history -> saved data matches calculation.
    [Fact]
    public async Task CreateEmployee_RunPayroll_HistoryMatchesCalculation()
    {
        string code = $"EMP-{Guid.NewGuid():N}";

        // Create.
        var createResponse = await _client.PostAsJsonAsync("/api/employees", EmployeePayload(code), Json);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var created = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        int employeeId = created.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(code, created.RootElement.GetProperty("employeeCode").GetString());
        Assert.True(created.RootElement.GetProperty("isActive").GetBoolean());

        // Run payroll for January.
        var runResponse = await _client.PostAsync($"/api/payroll/run/{employeeId}/1/2026", null);
        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);

        using var run = JsonDocument.Parse(await runResponse.Content.ReadAsStringAsync());
        int payrollRunId = run.RootElement.GetProperty("payrollRunId").GetInt32();

        // Independent calculation with the same inputs.
        PayrollSlip expected = PayrollCalculatorCore.Calculate(CalcInput(month: 1));

        Assert.Equal(expected.GrossMonthlySalary, run.RootElement.GetProperty("grossSalary").GetDecimal());
        Assert.Equal(expected.TotalDeductions, run.RootElement.GetProperty("totalDeductions").GetDecimal());
        Assert.Equal(expected.NetMonthlyTakeHome, run.RootElement.GetProperty("netPay").GetDecimal());
        Assert.Equal(expected.TotalEmployerCost, run.RootElement.GetProperty("totalEmployerCost").GetDecimal());
        Assert.Equal(expected.TotalCtc, run.RootElement.GetProperty("totalCtc").GetDecimal());

        // Fetch history - the saved row must match both the run response and the calculation.
        var historyResponse = await _client.GetAsync($"/api/payroll/history/{employeeId}");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        using var history = JsonDocument.Parse(await historyResponse.Content.ReadAsStringAsync());
        var runs = history.RootElement.EnumerateArray().ToList();
        Assert.Single(runs);

        var saved = runs[0];
        Assert.Equal(payrollRunId, saved.GetProperty("payrollRunId").GetInt32());
        Assert.Equal(1, saved.GetProperty("monthNumber").GetInt32());
        Assert.Equal(2026, saved.GetProperty("year").GetInt32());
        Assert.Equal(expected.GrossMonthlySalary, saved.GetProperty("grossSalary").GetDecimal());
        Assert.Equal(expected.NetMonthlyTakeHome, saved.GetProperty("netPay").GetDecimal());

        // Fetch the slip - details are persisted per component.
        var slipResponse = await _client.GetAsync($"/api/payroll/slip/{payrollRunId}");
        Assert.Equal(HttpStatusCode.OK, slipResponse.StatusCode);

        using var slip = JsonDocument.Parse(await slipResponse.Content.ReadAsStringAsync());
        var details = slip.RootElement.GetProperty("details").EnumerateArray().ToList();
        Assert.Equal(15, details.Count);

        decimal earnings = details
            .Where(d => d.GetProperty("componentType").GetString() == "Earning")
            .Sum(d => d.GetProperty("amount").GetDecimal());
        decimal deductions = details
            .Where(d => d.GetProperty("componentType").GetString() == "Deduction")
            .Sum(d => d.GetProperty("amount").GetDecimal());
        decimal employer = details
            .Where(d => d.GetProperty("componentType").GetString() == "EmployerContribution")
            .Sum(d => d.GetProperty("amount").GetDecimal());

        // Leaf components sum to their section totals (gross includes no other lines).
        Assert.Equal(expected.GrossMonthlySalary, earnings);
        Assert.Equal(expected.TotalDeductions, deductions);
        // Employer contributions exclude nothing; slip.TotalEmployerCost is their sum.
        Assert.Equal(expected.TotalEmployerCost, employer);
    }

    // Required test 2: duplicate employee code -> 400.
    [Fact]
    public async Task DuplicateEmployeeCode_Returns400()
    {
        string code = $"DUP-{Guid.NewGuid():N}";

        var first = await _client.PostAsJsonAsync("/api/employees", EmployeePayload(code), Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/employees", EmployeePayload(code), Json);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        using var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        string? error = body.RootElement.GetProperty("error").GetString();
        Assert.NotNull(error);
        Assert.Contains("already exists", error);
        Assert.Contains(code, error);
    }

    // Required test 3: run payroll for a non-existent employee -> 404.
    [Fact]
    public async Task RunPayroll_NonExistentEmployee_Returns404()
    {
        var response = await _client.PostAsync("/api/payroll/run/999999/1/2026", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("not found", body.RootElement.GetProperty("error").GetString());
    }
}
