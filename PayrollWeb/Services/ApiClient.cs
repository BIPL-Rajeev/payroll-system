using System.Net.Http.Json;
using System.Text.Json;
using PayrollApi.Core.Services;
using PayrollWeb.Models;

namespace PayrollWeb.Services;

/// <summary>API error carrying the server's { "error": "..." } message.</summary>
public sealed class ApiException(string message) : Exception(message);

/// <summary>Typed client for the PayrollApi endpoints.</summary>
public sealed class ApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---------- Employees ----------

    public async Task<List<ApiEmployee>> GetEmployeesAsync() =>
        await http.GetFromJsonAsync<List<ApiEmployee>>("/api/employees", Json) ?? [];

    /// <summary>Returns null when the employee does not exist (404).</summary>
    public async Task<ApiEmployee?> GetEmployeeAsync(int id)
    {
        var response = await http.GetAsync($"/api/employees/{id}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        return await ReadAsync<ApiEmployee>(response);
    }

    public async Task<ApiEmployee> CreateEmployeeAsync(EmployeeFormModel form) =>
        await ReadAsync<ApiEmployee>(
            await http.PostAsJsonAsync("/api/employees", form, Json));

    public async Task<ApiEmployee> UpdateEmployeeAsync(int id, EmployeeFormModel form) =>
        await ReadAsync<ApiEmployee>(
            await http.PutAsJsonAsync($"/api/employees/{id}", form, Json));

    public async Task DeleteEmployeeAsync(int id)
    {
        var response = await http.DeleteAsync($"/api/employees/{id}");
        await EnsureSuccessAsync(response);
    }

    // ---------- Payroll runs ----------

    public async Task<PayrollRunResult> RunPayrollAsync(int employeeId, int month, int year) =>
        await ReadAsync<PayrollRunResult>(
            await http.PostAsync($"/api/payroll/run/{employeeId}/{month}/{year}", null));

    public async Task<List<PayrollRunSummary>> GetHistoryAsync(int employeeId) =>
        await http.GetFromJsonAsync<List<PayrollRunSummary>>($"/api/payroll/history/{employeeId}", Json) ?? [];

    public async Task<PayrollRunResult> GetSlipAsync(int payrollRunId) =>
        await ReadAsync<PayrollRunResult>(
            await http.GetAsync($"/api/payroll/slip/{payrollRunId}"));

    // ---------- Form 16 ----------

    public async Task<Form16PartB> GetForm16Async(int employeeId, string financialYear) =>
        await ReadAsync<Form16PartB>(
            await http.GetAsync($"/api/form16/{employeeId}/{financialYear}"));

    // ---------- Helpers ----------

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<T>(Json);
        return result ?? throw new ApiException("Empty response from API.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string message;
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            message = doc.RootElement.TryGetProperty("error", out var error)
                ? error.GetString() ?? $"HTTP {(int)response.StatusCode}"
                : $"HTTP {(int)response.StatusCode}";
        }
        catch (JsonException)
        {
            message = $"HTTP {(int)response.StatusCode}";
        }

        throw new ApiException(message);
    }
}
