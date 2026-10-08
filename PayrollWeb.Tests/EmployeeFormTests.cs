using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PayrollWeb.Components.Pages;
using PayrollWeb.Services;
using Xunit;

namespace PayrollWeb.Tests;

/// <summary>Tests for the add-employee form validation.</summary>
public class EmployeeFormTests : BunitContext
{
    private StubHttpHandler RegisterApi()
    {
        var handler = new StubHttpHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://api.test") };
        Services.AddSingleton(http);
        Services.AddScoped<ApiClient>();
        Services.AddScoped<ToastService>();
        return handler;
    }

    // Required test: add employee form -> validates required fields.
    [Fact]
    public void AddEmployeeForm_EmptySubmit_ShowsValidationErrors_AndDoesNotCallApi()
    {
        var handler = RegisterApi();

        var cut = Render<EmployeeAdd>();

        // Submit the (empty) form.
        var submit = cut.Find("button[type=submit]");
        submit.Click();

        // Required-field validation messages appear.
        cut.WaitForAssertion(() =>
            Assert.Contains("Employee code is required.", cut.Markup));
        Assert.Contains("Name is required.", cut.Markup);
        Assert.Contains("State is required.", cut.Markup);

        Assert.Equal(3, cut.FindAll(".validation-message").Count);

        // Nothing was sent to the API.
        Assert.DoesNotContain(handler.Requests, r => r.Method == "POST");
    }

    // Numeric fields reject negative amounts client-side.
    [Fact]
    public void AddEmployeeForm_NegativeSalary_ShowsValidation()
    {
        RegisterApi();

        var cut = Render<EmployeeAdd>();

        // Fill required fields so only the range rule fails.
        cut.Find("#f-code").Change("EMP010");
        cut.Find("#f-name").Change("Negative Salary");
        cut.Find("#f-state").Change("Karnataka");
        cut.Find("#f-basic").Change("-100");

        cut.Find("button[type=submit]").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains("Amount must be non-negative.", cut.Markup));
    }
}
