using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PayrollWeb.Components.Pages;
using PayrollWeb.Services;
using Xunit;

namespace PayrollWeb.Tests;

/// <summary>Tests for the /employees list page.</summary>
public class EmployeesPageTests : BunitContext
{
    // Minimal API employee payload (camelCase, as returned by PayrollApi).
    private const string EmployeesJson =
        """
        [
          { "id": 1, "employeeCode": "EMP001", "name": "Asha Patil", "state": "Maharashtra",
            "isMetro": true, "taxRegime": "old", "monthlyBasic": 50000.0,
            "monthlyHra": 20000.0, "monthlyDa": 0.0, "monthlySpecialAllowance": 0.0,
            "monthlyLta": 0.0, "monthlyOtherAllowances": 0.0, "annualRentPaid": 180000.0,
            "investment80C": 150000.0, "investment80CCD1B": 0.0, "investment80D": 0.0,
            "investment80TTA": 0.0, "homeLoanInterest": 0.0,
            "isFirstYearEmployee": false, "hasDisability": false, "isActive": true },
          { "id": 2, "employeeCode": "EMP002", "name": "Ravi Kumar", "state": "Karnataka",
            "isMetro": false, "taxRegime": "new", "monthlyBasic": 15000.0,
            "monthlyHra": 0.0, "monthlyDa": 0.0, "monthlySpecialAllowance": 0.0,
            "monthlyLta": 0.0, "monthlyOtherAllowances": 0.0, "annualRentPaid": 0.0,
            "investment80C": 0.0, "investment80CCD1B": 0.0, "investment80D": 0.0,
            "investment80TTA": 0.0, "homeLoanInterest": 0.0,
            "isFirstYearEmployee": true, "hasDisability": false, "isActive": true }
        ]
        """;

    private StubHttpHandler RegisterApi(params (string Method, string Path, string Json)[] routes)
    {
        var handler = new StubHttpHandler(routes);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.test") };
        Services.AddSingleton(http);
        Services.AddScoped<ApiClient>();
        Services.AddScoped<ToastService>();
        // Excel export buttons resolve AuthService / AuthState at render time.
        Services.AddScoped<AuthState>();
        Services.AddScoped<AuthService>();
        return handler;
    }

    // Required test: navigate to /employees -> page loads.
    [Fact]
    public void EmployeesPage_Loads_AndShowsEmployees()
    {
        RegisterApi(("GET", "/api/employees", EmployeesJson));

        // The page is routed at /employees; render it (its @page directive targets that route).
        var cut = Render<Employees>();

        // Async load starts in the loading state; wait for data to arrive.
        cut.WaitForState(() => !cut.Markup.Contains("Loading employees..."));

        Assert.Contains("Employees", cut.Markup);
        Assert.Contains("EMP001", cut.Markup);
        Assert.Contains("Asha Patil", cut.Markup);
        Assert.Contains("EMP002", cut.Markup);
        Assert.Contains("Ravi Kumar", cut.Markup);

        // Add New button + row actions present.
        Assert.Contains("Add New", cut.Markup);
        Assert.Contains("Run", cut.Markup);
        Assert.Contains("History", cut.Markup);
        Assert.Contains("Edit", cut.Markup);
        Assert.Contains("Delete", cut.Markup);
    }

    // Search box filters the list client-side.
    [Fact]
    public void EmployeesPage_Search_FiltersList()
    {
        RegisterApi(("GET", "/api/employees", EmployeesJson));

        var cut = Render<Employees>();
        cut.WaitForState(() => !cut.Markup.Contains("Loading employees..."));

        var search = cut.Find("input[placeholder*='Search']");
        search.Input("ravi"); // bound with @bind:event="oninput"

        cut.WaitForAssertion(() => Assert.Contains("Ravi Kumar", cut.Markup));
        Assert.DoesNotContain("Asha Patil", cut.Markup);
    }
}

/// <summary>Routes canned JSON responses and records requests.</summary>
public sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> _routes;

    public List<(string Method, string Path)> Requests { get; } = [];

    public StubHttpHandler(params (string Method, string Path, string Json)[] routes)
    {
        _routes = routes.ToDictionary(r => $"{r.Method} {r.Path}", r => r.Json);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string method = request.Method.Method;
        string path = request.RequestUri!.PathAndQuery;
        Requests.Add((method, path));

        if (_routes.TryGetValue($"{method} {path}", out string? json))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{ "error": "Not found." }""", Encoding.UTF8, "application/json"),
        });
    }
}
