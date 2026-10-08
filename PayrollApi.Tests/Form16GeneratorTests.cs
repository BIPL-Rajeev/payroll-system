using PayrollApi.Core.Entities;
using PayrollApi.Core.Services;
using Xunit;
using Employee = PayrollApi.Core.Entities.Employee;

namespace PayrollApi.Tests;

/// <summary>
/// Form 16 Part B unit tests: 12-month and part-year totals, regime rules.
/// Expected values are hand-computed in the assertions.
/// </summary>
public class Form16GeneratorTests
{
    // FY 2025-26 = April 2025 .. March 2026.
    private static List<PayrollRun> FullYearRuns(decimal basic = 50_000m, decimal hra = 20_000m)
    {
        var runs = new List<PayrollRun>();

        // Apr-Dec 2025.
        for (int month = 4; month <= 12; month++)
        {
            runs.Add(MakeRun(month, 2025, basic, hra));
        }
        // Jan-Mar 2026.
        for (int month = 1; month <= 3; month++)
        {
            runs.Add(MakeRun(month, 2026, basic, hra));
        }

        return runs;
    }

    private static PayrollRun MakeRun(int month, int year, decimal basic, decimal hra)
    {
        // Maharashtra monthly PT: 200 (300 in February for salaries above 10,000).
        decimal pt = month == 2 ? 300m : 200m;

        var run = new PayrollRun
        {
            EmployeeId = 1,
            MonthNumber = month,
            Year = year,
            GrossSalary = basic + hra,
            TotalDeductions = 1_800m + pt + 5_000m,
            NetPay = basic + hra - (1_800m + pt + 5_000m),
            TotalEmployerCost = 1_950m,
            TotalCtc = basic + hra + 1_950m,
            GeneratedAt = DateTime.UtcNow,
        };

        run.Details = new List<PayrollRunDetail>
        {
            new() { ComponentName = "Basic Salary", ComponentType = ComponentTypes.Earning, Amount = basic },
            new() { ComponentName = "House Rent Allowance", ComponentType = ComponentTypes.Earning, Amount = hra },
            new() { ComponentName = "Dearness Allowance", ComponentType = ComponentTypes.Earning, Amount = 0m },
            new() { ComponentName = "Special Allowance", ComponentType = ComponentTypes.Earning, Amount = 0m },
            new() { ComponentName = "Leave Travel Allowance", ComponentType = ComponentTypes.Earning, Amount = 0m },
            new() { ComponentName = "Other Allowances", ComponentType = ComponentTypes.Earning, Amount = 0m },
            new() { ComponentName = "Employee PF", ComponentType = ComponentTypes.Deduction, Amount = 1_800m },
            new() { ComponentName = "Employee ESI", ComponentType = ComponentTypes.Deduction, Amount = 0m },
            new() { ComponentName = "Professional Tax", ComponentType = ComponentTypes.Deduction, Amount = pt },
            new() { ComponentName = "TDS", ComponentType = ComponentTypes.Deduction, Amount = 5_000m },
            new() { ComponentName = "Employer EPS", ComponentType = ComponentTypes.EmployerContribution, Amount = 1_249.50m },
            new() { ComponentName = "Employer EPF", ComponentType = ComponentTypes.EmployerContribution, Amount = 550.50m },
            new() { ComponentName = "Employer ESI", ComponentType = ComponentTypes.EmployerContribution, Amount = 0m },
            new() { ComponentName = "EDLI", ComponentType = ComponentTypes.EmployerContribution, Amount = 75m },
            new() { ComponentName = "Admin Charges", ComponentType = ComponentTypes.EmployerContribution, Amount = 75m },
        };

        return run;
    }

    private static Employee MakeEmployee(string regime, decimal investment80C = 0m) => new()
    {
        EmployeeCode = "FT001",
        Name = "Form Test",
        Pan = "ABCDE1234F",
        State = "Maharashtra",
        IsMetro = false,
        TaxRegime = regime,
        AnnualRentPaid = 180_000m,
        Investment80C = investment80C,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    // Required test 1: 12 months of payroll -> correct annual totals.
    [Fact]
    public void TwelveMonths_CorrectAnnualTotals()
    {
        Employee employee = MakeEmployee("old", investment80C: 150_000m);
        List<PayrollRun> runs = FullYearRuns();

        Form16PartB form = Form16Generator.Generate(employee, runs, "2025-26");

        // Section 2: 12 x monthly.
        Assert.Equal(600_000m, form.GrossSalary.Basic);            // 50,000 x 12
        Assert.Equal(240_000m, form.GrossSalary.Hra);              // 20,000 x 12
        Assert.Equal(840_000m, form.GrossSalary.Total);

        // Section 3: HRA least-of-3 = min(240,000 ; 180,000 - 60,000 = 120,000 ; 40% x 600,000 = 240,000).
        Assert.Equal(120_000m, form.Exemptions.HraExemption);
        Assert.Equal(0m, form.Exemptions.LtaExemption);
        Assert.Equal(50_000m, form.Exemptions.StandardDeduction);   // old regime
        Assert.Equal(2_500m, form.Exemptions.ProfessionalTax);      // 11 x 200 + 300 (Feb)
        Assert.Equal(172_500m, form.Exemptions.Total);

        // Section 4.
        Assert.Equal(667_500m, form.IncomeChargeableUnderSalaries); // 840,000 - 172,500

        // Section 5: 80C capped at 1,50,000.
        Assert.Equal(150_000m, form.ChapterVIA.Section80C);
        Assert.Equal(150_000m, form.ChapterVIA.Total);

        // Section 6: taxable = 667,500 - 150,000 = 517,500 (already a multiple of 10).
        Assert.Equal(517_500m, form.TaxableIncome);

        // Section 7: old slabs -> 2,50,000-5,00,000 x 5% = 12,500 plus 5,00,000-5,17,500 x 20% = 3,500
        // -> 16,000; no 87A (> 5L); cess 4% = 640 -> total 16,640.
        Assert.Equal(16_000m, form.Tax.TaxBeforeRebate);
        Assert.Equal(0m, form.Tax.Rebate87A);
        Assert.Equal(640m, form.Tax.Cess);
        Assert.Equal(16_640m, form.Tax.TotalTaxPayable);

        // Section 8: TDS 12 x 5,000 = 60,000 -> refund 60,000 - 16,640 = 43,360.
        Assert.Equal(60_000m, form.Tds.TotalTdsDeducted);
        Assert.Equal(0m, form.Tds.BalanceTaxPayable);
        Assert.Equal(43_360m, form.Tds.Refund);
    }

    // Required test 2: only 6 months (Apr-Sep 2025) -> part-year still computes.
    [Fact]
    public void SixMonths_PartYear_Computes()
    {
        Employee employee = MakeEmployee("old", investment80C: 150_000m);
        List<PayrollRun> runs = FullYearRuns()
            .Where(r => r.Year == 2025 && r.MonthNumber <= 9)
            .ToList();
        Assert.Equal(6, runs.Count);

        Form16PartB form = Form16Generator.Generate(employee, runs, "2025-26");

        // Section 2: 6 x monthly.
        Assert.Equal(300_000m, form.GrossSalary.Basic);   // 50,000 x 6
        Assert.Equal(120_000m, form.GrossSalary.Hra);     // 20,000 x 6
        Assert.Equal(420_000m, form.GrossSalary.Total);

        // Section 3: HRA least-of-3 = min(120,000 ; 180,000 - 30,000 = 150,000 ; 40% x 300,000 = 120,000).
        Assert.Equal(120_000m, form.Exemptions.HraExemption);
        Assert.Equal(50_000m, form.Exemptions.StandardDeduction);
        Assert.Equal(1_200m, form.Exemptions.ProfessionalTax);   // 6 x 200 (no February)
        Assert.Equal(171_200m, form.Exemptions.Total);

        // Section 4/6: 420,000 - 171,200 = 248,800; minus 1,50,000 = 98,800.
        Assert.Equal(248_800m, form.IncomeChargeableUnderSalaries);
        Assert.Equal(98_800m, form.TaxableIncome);

        // Taxable below 2,50,000 -> nil tax.
        Assert.Equal(0m, form.Tax.TotalTaxPayable);

        // TDS 6 x 5,000 = 30,000 fully refundable.
        Assert.Equal(30_000m, form.Tds.TotalTdsDeducted);
        Assert.Equal(30_000m, form.Tds.Refund);
    }

    // Required test 3: old regime with 80C 1,50,000 -> tax reduces correctly.
    [Fact]
    public void OldRegime_80C150000_ReducesTax()
    {
        List<PayrollRun> runs = FullYearRuns();

        Form16PartB without = Form16Generator.Generate(MakeEmployee("old"), runs, "2025-26");
        Form16PartB with80C = Form16Generator.Generate(
            MakeEmployee("old", investment80C: 150_000m), runs, "2025-26");

        // Without 80C: taxable 6,67,500 -> tax 12,500 + 1,67,500 x 20% = 46,000; cess 1,840 = 47,840.
        Assert.Equal(0m, without.ChapterVIA.Total);
        Assert.Equal(667_500m, without.TaxableIncome);
        Assert.Equal(46_000m, without.Tax.TaxBeforeRebate);
        Assert.Equal(1_840m, without.Tax.Cess);
        Assert.Equal(47_840m, without.Tax.TotalTaxPayable);

        // With 80C: deductions 1,50,000, taxable 5,17,500; tax 12,500 + 3,500 = 16,000; cess 640 = 16,640.
        Assert.Equal(150_000m, with80C.ChapterVIA.Section80C);
        Assert.Equal(150_000m, with80C.ChapterVIA.Total);
        Assert.Equal(150_000m, without.TaxableIncome - with80C.TaxableIncome);
        Assert.Equal(16_640m, with80C.Tax.TotalTaxPayable);
        Assert.True(with80C.Tax.TotalTaxPayable < without.Tax.TotalTaxPayable);
        // Reduction: 47,840 - 16,640 = 31,200 (= 1,50,000 x 20% x 1.04 cess).
        Assert.Equal(31_200m, without.Tax.TotalTaxPayable - with80C.Tax.TotalTaxPayable);
    }

    // Required test 4: new regime -> 80C is NOT deducted.
    [Fact]
    public void NewRegime_80CNotDeducted()
    {
        Employee employee = MakeEmployee("new", investment80C: 150_000m);
        employee.Investment80CCD1B = 50_000m;
        employee.Investment80D = 25_000m;
        employee.Investment80TTA = 10_000m;

        Form16PartB form = Form16Generator.Generate(employee, FullYearRuns(), "2025-26");

        // No Chapter VI-A deductions under the new regime.
        Assert.Equal(0m, form.ChapterVIA.Section80C);
        Assert.Equal(0m, form.ChapterVIA.Section80CCD1B);
        Assert.Equal(0m, form.ChapterVIA.Section80D);
        Assert.Equal(0m, form.ChapterVIA.Section80TTA);
        Assert.Equal(0m, form.ChapterVIA.Total);

        // New regime: no HRA exemption, no PT; only 75,000 standard deduction.
        Assert.Equal(0m, form.Exemptions.HraExemption);
        Assert.Equal(0m, form.Exemptions.ProfessionalTax);
        Assert.Equal(75_000m, form.Exemptions.StandardDeduction);
        Assert.Equal(75_000m, form.Exemptions.Total);

        // 840,000 - 75,000 = 765,000 taxable -> slab tax 3,65,000 x 5% = 18,250,
        // fully rebated under 87A (<= 12L) -> nil tax.
        Assert.Equal(765_000m, form.IncomeChargeableUnderSalaries);
        Assert.Equal(765_000m, form.TaxableIncome);
        Assert.Equal(0m, form.Tax.TotalTaxPayable);
    }

    // Financial-year helpers.
    [Theory]
    [InlineData("2025-26", true)]
    [InlineData("2024-25", true)]
    [InlineData("2025/26", false)]
    [InlineData("2025-27", false)]
    [InlineData("25-26", false)]
    [InlineData("", false)]
    public void FinancialYear_Validation(string fy, bool expected) =>
        Assert.Equal(expected, Form16Generator.IsValidFinancialYear(fy));

    [Fact]
    public void AssessmentYear_IsNextYear()
    {
        Assert.Equal("2026-27", Form16Generator.AssessmentYear("2025-26"));
        Assert.Equal("2025-26", Form16Generator.AssessmentYear("2024-25"));
    }

    // PDF generation smoke test (magic bytes).
    [Fact]
    public void GeneratePdf_ReturnsPdfWithMagicBytes()
    {
        Form16PartB form = Form16Generator.Generate(
            MakeEmployee("old", investment80C: 150_000m), FullYearRuns(), "2025-26");

        byte[] pdf = Form16Generator.GeneratePdf(form);

        Assert.NotEmpty(pdf);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
