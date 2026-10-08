using System.Text;
using PayrollApi.Core.Entities;
using PayrollApi.Core.Services;
using Xunit;

namespace PayrollApi.Tests;

public class PayslipPdfGeneratorTests
{
    static PayslipPdfGeneratorTests()
    {
        // QuestPDF requires a license to be set before generating documents.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    private static Employee SampleEmployee() => new()
    {
        Id = 1,
        EmployeeCode = "EMP001",
        Name = "Asha Patil",
        State = "Maharashtra",
        IsMetro = true,
        TaxRegime = "old",
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static PayrollRun SampleRun()
    {
        var run = new PayrollRun
        {
            Id = 1,
            EmployeeId = 1,
            MonthNumber = 1,
            Year = 2026,
            GrossSalary = 80_000m,
            TotalDeductions = 5_467m,
            NetPay = 74_533m,
            TotalEmployerCost = 1_950m,
            TotalCtc = 81_950m,
            GeneratedAt = DateTime.UtcNow,
        };

        // The same 15 component lines the API persists for a run.
        run.Details = new List<PayrollRunDetail>
        {
            new() { ComponentName = "Basic Salary", ComponentType = ComponentTypes.Earning, Amount = 50_000m },
            new() { ComponentName = "House Rent Allowance", ComponentType = ComponentTypes.Earning, Amount = 20_000m },
            new() { ComponentName = "Dearness Allowance", ComponentType = ComponentTypes.Earning, Amount = 5_000m },
            new() { ComponentName = "Special Allowance", ComponentType = ComponentTypes.Earning, Amount = 5_000m },
            new() { ComponentName = "Leave Travel Allowance", ComponentType = ComponentTypes.Earning, Amount = 0m },
            new() { ComponentName = "Other Allowances", ComponentType = ComponentTypes.Earning, Amount = 0m },
            new() { ComponentName = "Employee PF", ComponentType = ComponentTypes.Deduction, Amount = 1_800m },
            new() { ComponentName = "Employee ESI", ComponentType = ComponentTypes.Deduction, Amount = 0m },
            new() { ComponentName = "Professional Tax", ComponentType = ComponentTypes.Deduction, Amount = 200m },
            new() { ComponentName = "TDS", ComponentType = ComponentTypes.Deduction, Amount = 3_467m },
            new() { ComponentName = "Employer EPS", ComponentType = ComponentTypes.EmployerContribution, Amount = 1_249.50m },
            new() { ComponentName = "Employer EPF", ComponentType = ComponentTypes.EmployerContribution, Amount = 550.50m },
            new() { ComponentName = "Employer ESI", ComponentType = ComponentTypes.EmployerContribution, Amount = 0m },
            new() { ComponentName = "EDLI", ComponentType = ComponentTypes.EmployerContribution, Amount = 75m },
            new() { ComponentName = "Admin Charges", ComponentType = ComponentTypes.EmployerContribution, Amount = 75m },
        };

        return run;
    }

    // Generate PDF for a sample PayrollRun -> byte array is not empty and starts with "%PDF".
    [Fact]
    public void GeneratePdf_SampleRun_ReturnsNonEmptyPdfWithMagicBytes()
    {
        PayrollRun run = SampleRun();
        Employee employee = SampleEmployee();

        byte[] pdf = PayslipPdfGenerator.GeneratePdf(run, employee, run.Details.ToList());

        Assert.NotEmpty(pdf);
        Assert.True(pdf.Length > 100, $"PDF suspiciously small: {pdf.Length} bytes");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    // Generate PDF with missing employee -> throws exception.
    [Fact]
    public void GeneratePdf_MissingEmployee_Throws()
    {
        PayrollRun run = SampleRun();

        Assert.Throws<ArgumentNullException>(() =>
            PayslipPdfGenerator.GeneratePdf(run, null, run.Details.ToList()));
    }

    // Null run is rejected too (defensive).
    [Fact]
    public void GeneratePdf_NullRun_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PayslipPdfGenerator.GeneratePdf(null!, SampleEmployee(), new List<PayrollRunDetail>()));
    }
}
