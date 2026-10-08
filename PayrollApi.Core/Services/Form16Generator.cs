using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PayrollApi.Core.Entities;

namespace PayrollApi.Core.Services;

// Inside the namespace so this alias wins over PayrollApi.Core.Employee (the calculation model).
using Employee = PayrollApi.Core.Entities.Employee;

// ---------------- Form 16 Part B model (JSON-serialisable) ----------------

public sealed record Form16EmployerDetails(string EmployerName, string EmployerPan, string EmployerTan);

public sealed record Form16EmployeeDetails(
    string EmployeeName,
    string EmployeePan,
    string EmployeeCode,
    string TaxRegime,
    string FinancialYear,
    string AssessmentYear,
    Form16EmployerDetails Employer);

/// <summary>Section 2: annual gross salary components (sum of payroll runs in the FY).</summary>
public sealed record Form16GrossSection(
    decimal Basic,
    decimal Hra,
    decimal Da,
    decimal SpecialAllowance,
    decimal Lta,
    decimal OtherAllowances,
    decimal Total);

/// <summary>Section 3: exemptions and deductions (Section 10 + 16).</summary>
public sealed record Form16ExemptionSection(
    decimal HraExemption,
    decimal LtaExemption,
    decimal StandardDeduction,
    decimal ProfessionalTax,
    decimal Total);

/// <summary>Section 5: Chapter VI-A deductions (regime-aware, capped).</summary>
public sealed record Form16ChapterVIASection(
    decimal Section80C,
    decimal Section80CCD1B,
    decimal Section80D,
    decimal Section80TTA,
    decimal Section80CCD2,
    decimal Total);

/// <summary>Section 7: tax computation.</summary>
public sealed record Form16TaxSection(
    string TaxRegime,
    decimal TaxBeforeRebate,
    decimal Rebate87A,
    decimal Surcharge,
    decimal MarginalRelief,
    decimal Cess,
    decimal TotalTaxPayable);

/// <summary>Section 8: TDS summary.</summary>
public sealed record Form16TdsSection(
    decimal TotalTdsDeducted,
    decimal BalanceTaxPayable,
    decimal Refund);

/// <summary>Form 16 Part B for one employee and one financial year.</summary>
public sealed record Form16PartB(
    Form16EmployeeDetails EmployeeDetails,
    Form16GrossSection GrossSalary,
    Form16ExemptionSection Exemptions,
    decimal IncomeChargeableUnderSalaries,
    Form16ChapterVIASection ChapterVIA,
    decimal TaxableIncome,
    Form16TaxSection Tax,
    Form16TdsSection Tds,
    string GeneratedOn);

// ---------------- Generator ----------------

/// <summary>
/// Builds Form 16 (Part B) for FY 2025-26 style financial years (April to March)
/// from an Employee and that employee's PayrollRun records, and renders it as PDF.
/// </summary>
public static class Form16Generator
{
    private static readonly CultureInfo InrCulture = CultureInfo.GetCultureInfo("en-IN");

    // Old regime Chapter VI-A caps.
    public const decimal Cap80C = 1_50_000m;
    public const decimal Cap80CCD1B = 50_000m;
    public const decimal Cap80D = 25_000m;      // + 50,000 for senior-citizen parents (not tracked per employee yet)
    public const decimal Cap80TTA = 10_000m;

    // Standard deductions (Section 16(ii)).
    public const decimal StandardDeductionOld = 50_000m;
    public const decimal StandardDeductionNew = 75_000m;

    // ---------------- Financial year helpers ----------------

    /// <summary>Validates "2025-26" format: 4 digits, dash, 2 digits where yy == (yyyy % 100) + 1.</summary>
    public static bool IsValidFinancialYear(string? financialYear)
    {
        if (financialYear is not { Length: 7 } || financialYear[4] != '-')
        {
            return false;
        }
        if (!int.TryParse(financialYear[..4], NumberStyles.None, CultureInfo.InvariantCulture, out int startYear) ||
            !int.TryParse(financialYear[5..], NumberStyles.None, CultureInfo.InvariantCulture, out int endPart))
        {
            return false;
        }
        return (startYear % 100 + 1) % 100 == endPart;
    }

    /// <summary>Assessment year for a financial year: FY 2025-26 -> AY 2026-27.</summary>
    public static string AssessmentYear(string financialYear)
    {
        int startYear = int.Parse(financialYear[..4], CultureInfo.InvariantCulture);
        int ayStart = startYear + 1;
        return $"{ayStart}-{(ayStart + 1) % 100:00}";
    }

    /// <summary>Filters runs belonging to the FY: "2025-26" = Apr 2025 .. Mar 2026.</summary>
    public static List<PayrollRun> RunsForFinancialYear(IEnumerable<PayrollRun> runs, string financialYear)
    {
        if (!IsValidFinancialYear(financialYear))
        {
            throw new ArgumentException(
                $"Invalid financial year \"{financialYear}\". Expected format: 2025-26.", nameof(financialYear));
        }

        int startYear = int.Parse(financialYear[..4], CultureInfo.InvariantCulture);
        int endYear = startYear + 1;

        return runs
            .Where(r => (r.MonthNumber >= 4 && r.Year == startYear) ||
                        (r.MonthNumber <= 3 && r.Year == endYear))
            .OrderBy(r => r.Year)
            .ThenBy(r => r.MonthNumber)
            .ToList();
    }

    // ---------------- JSON model ----------------

    /// <summary>
    /// Computes Form 16 Part B. Throws ArgumentException for an invalid FY and
    /// InvalidOperationException when the employee has no runs in that FY.
    /// </summary>
    public static Form16PartB Generate(
        Employee employee,
        IEnumerable<PayrollRun> allRuns,
        string financialYear,
        PayslipOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(employee);

        List<PayrollRun> runs = RunsForFinancialYear(allRuns, financialYear);
        if (runs.Count == 0)
        {
            throw new InvalidOperationException(
                $"No payroll runs exist for employee {employee.EmployeeCode} in FY {financialYear}.");
        }

        options ??= new PayslipOptions();
        bool isOld = string.Equals(employee.TaxRegime, "old", StringComparison.OrdinalIgnoreCase);
        string regimeLabel = isOld ? "Old" : "New";
        string assessmentYear = AssessmentYear(financialYear);

        // ---------- Section 2: gross salary (annual sums from run components) ----------
        decimal basic = Sum(runs, "Basic Salary");
        decimal hra = Sum(runs, "House Rent Allowance");
        decimal da = Sum(runs, "Dearness Allowance");
        decimal special = Sum(runs, "Special Allowance");
        decimal lta = Sum(runs, "Leave Travel Allowance");
        decimal other = Sum(runs, "Other Allowances");
        decimal grossTotal = Round2(basic + hra + da + special + lta + other);
        decimal annualPt = Sum(runs, "Professional Tax");
        decimal totalTds = Sum(runs, "TDS");

        // ---------- Section 3: exemptions ----------
        // HRA exemption u/s 10(13A): three-rule least-of method (old regime only).
        decimal hraExemption = isOld
            ? OldRegimeCalculator.HraExemption(hra, basic, employee.AnnualRentPaid, employee.IsMetro)
            : 0m;
        // LTA exemption: requires travel proofs; none tracked by the system yet.
        decimal ltaExemption = 0m;
        decimal standardDeduction = isOld ? StandardDeductionOld : StandardDeductionNew;
        // Professional tax u/s 16(iii): deductible in the old regime only (new regime has only 16(ii)).
        decimal professionalTax = isOld ? annualPt : 0m;
        decimal totalExemptions = Round2(hraExemption + ltaExemption + standardDeduction + professionalTax);

        // ---------- Section 4 ----------
        decimal incomeChargeable = Round2(Math.Max(0m, grossTotal - totalExemptions));

        // ---------- Section 5: Chapter VI-A ----------
        decimal s80C = isOld ? Math.Min(employee.Investment80C, Cap80C) : 0m;
        decimal s80CCD1B = isOld ? Math.Min(employee.Investment80CCD1B, Cap80CCD1B) : 0m;
        decimal s80D = isOld ? Math.Min(employee.Investment80D, Cap80D) : 0m;
        decimal s80TTA = isOld ? Math.Min(employee.Investment80TTA, Cap80TTA) : 0m;
        // 80CCD(2) employer NPS is allowed in BOTH regimes; employer NPS is not
        // tracked in payroll yet, so it stays 0 until that component exists.
        decimal s80CCD2 = 0m;
        decimal totalChapterVIA = Round2(s80C + s80CCD1B + s80D + s80TTA + s80CCD2);

        // ---------- Section 6: taxable income, rounded to nearest Rs 10 ----------
        decimal taxableRaw = Math.Max(0m, incomeChargeable - totalChapterVIA);
        decimal taxableIncome = Math.Round(taxableRaw / 10m, 0, MidpointRounding.AwayFromZero) * 10m;

        // ---------- Section 7: tax ----------
        Form16TaxSection tax = isOld
            ? OldRegimeTax(taxableIncome, regimeLabel)
            : NewRegimeTax(taxableIncome, grossTotal);

        // ---------- Section 8: TDS summary ----------
        decimal balance = Round2(tax.TotalTaxPayable - totalTds);
        decimal balancePayable = Math.Max(0m, balance);
        decimal refund = Math.Max(0m, -balance);

        return new Form16PartB(
            EmployeeDetails: new Form16EmployeeDetails(
                EmployeeName: employee.Name,
                EmployeePan: string.IsNullOrWhiteSpace(employee.Pan) ? "-" : employee.Pan,
                EmployeeCode: employee.EmployeeCode,
                TaxRegime: regimeLabel,
                FinancialYear: financialYear,
                AssessmentYear: assessmentYear,
                Employer: new Form16EmployerDetails(
                    EmployerName: options.CompanyName,
                    EmployerPan: options.EmployerPan,
                    EmployerTan: options.EmployerTAN)),
            GrossSalary: new Form16GrossSection(basic, hra, da, special, lta, other, grossTotal),
            Exemptions: new Form16ExemptionSection(
                hraExemption, ltaExemption, standardDeduction, professionalTax, totalExemptions),
            IncomeChargeableUnderSalaries: incomeChargeable,
            ChapterVIA: new Form16ChapterVIASection(
                s80C, s80CCD1B, s80D, s80TTA, s80CCD2, totalChapterVIA),
            TaxableIncome: taxableIncome,
            Tax: tax,
            Tds: new Form16TdsSection(totalTds, balancePayable, refund),
            GeneratedOn: DateTime.Now.ToString("dd MMM yyyy HH:mm", InrCulture));
    }

    // ---------------- Tax computations ----------------

    /// <summary>Old regime: slabs 2.5L/5L/10L, 87A up to 12,500, surcharge with statutory marginal relief, 4% cess.</summary>
    private static Form16TaxSection OldRegimeTax(decimal taxableIncome, string regimeLabel)
    {
        decimal taxBeforeRebate = Round2(OldSlabTax(taxableIncome));

        decimal rebate = taxableIncome <= 5_00_000m
            ? Round2(Math.Min(taxBeforeRebate, 12_500m))
            : 0m;
        decimal taxAfterRebate = Round2(taxBeforeRebate - rebate);

        // Surcharge tiers: threshold, rate above it, rate at the threshold (baseline for relief).
        (decimal threshold, decimal rate, decimal rateAtThreshold) =
            taxableIncome switch
            {
                > 5_00_00_000m => (5_00_00_000m, 0.37m, 0.25m),
                > 2_00_00_000m => (2_00_00_000m, 0.25m, 0.15m),
                > 1_00_00_000m => (1_00_00_000m, 0.15m, 0.10m),
                > 50_00_000m => (50_00_000m, 0.10m, 0.00m),
                _ => (0m, 0m, 0m),
            };

        decimal surcharge = Round2(taxAfterRebate * rate);
        decimal marginalRelief = 0m;
        if (rate > 0m)
        {
            decimal cap = Round2(OldSlabTax(threshold) * (1m + rateAtThreshold) + (taxableIncome - threshold));
            decimal actual = taxAfterRebate + surcharge;
            if (actual > cap)
            {
                marginalRelief = Round2(actual - cap);
            }
        }

        decimal taxPlusSurcharge = Round2(taxAfterRebate + surcharge - marginalRelief);
        decimal cess = Round2(taxPlusSurcharge * 0.04m);
        decimal totalTax = Round2(taxPlusSurcharge + cess);

        return new Form16TaxSection(regimeLabel, taxBeforeRebate, rebate, surcharge, marginalRelief, cess, totalTax);
    }

    private static decimal OldSlabTax(decimal taxableIncome)
    {
        decimal tax = 0m;
        if (taxableIncome > 2_50_000m)
        {
            tax += (Math.Min(taxableIncome, 5_00_000m) - 2_50_000m) * 0.05m;
        }
        if (taxableIncome > 5_00_000m)
        {
            tax += (Math.Min(taxableIncome, 10_00_000m) - 5_00_000m) * 0.20m;
        }
        if (taxableIncome > 10_00_000m)
        {
            tax += (taxableIncome - 10_00_000m) * 0.30m;
        }
        return tax;
    }

    /// <summary>New regime (FY 2025-26): reuses TaxCalculator (slabs, 87A, marginal relief, surcharge, cess).</summary>
    private static Form16TaxSection NewRegimeTax(decimal taxableIncome, decimal grossSalary)
    {
        TaxResult tax = TaxCalculator.FromTaxableIncome(taxableIncome, grossSalary);
        return new Form16TaxSection(
            TaxRegime: "New",
            TaxBeforeRebate: tax.TaxBeforeRebate,
            Rebate87A: tax.Rebate,
            Surcharge: tax.Surcharge,
            MarginalRelief: tax.MarginalRelief,
            Cess: tax.Cess,
            TotalTaxPayable: tax.TotalAnnualTax);
    }

    // ---------------- PDF ----------------

    /// <summary>Renders Form 16 Part B as a PDF (QuestPDF, same style as the payslip).</summary>
    public static byte[] GeneratePdf(Form16PartB form, PayslipOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(form);
        options ??= new PayslipOptions();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(t => t.FontSize(9));

                // Header.
                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(options.CompanyName).FontSize(13).Bold();
                    col.Item().AlignCenter().Text(options.CompanyAddress).FontSize(8).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).AlignCenter()
                        .Text("FORM 16 - PART B").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().AlignCenter().Text($"For Financial Year {form.EmployeeDetails.FinancialYear}");
                    col.Item().AlignCenter().Text($"Assessment Year {form.EmployeeDetails.AssessmentYear}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                });

                page.Content().PaddingTop(6).Column(col =>
                {
                    // Section 1.
                    col.Item().Text("SECTION 1: Employee & Employer Details").Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().PaddingTop(2).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });
                        DetailRow("Employee Name", form.EmployeeDetails.EmployeeName,
                            "PAN", form.EmployeeDetails.EmployeePan);
                        DetailRow("Employee Code", form.EmployeeDetails.EmployeeCode,
                            "Tax Regime", form.EmployeeDetails.TaxRegime);
                        DetailRow("Employer Name", form.EmployerNameSafe(options),
                            "Employer PAN", form.EmployeeDetails.Employer.EmployerPan);
                        DetailRow("Employer TAN", form.EmployeeDetails.Employer.EmployerTan,
                            "Financial Year", form.EmployeeDetails.FinancialYear);

                        void DetailRow(string l1, string v1, string l2, string v2)
                        {
                            t.Cell().PaddingBottom(1).Text(l1).FontSize(8).FontColor(Colors.Grey.Darken1);
                            t.Cell().PaddingBottom(1).Text(v1);
                            t.Cell().PaddingBottom(1).Text(l2).FontSize(8).FontColor(Colors.Grey.Darken1);
                            t.Cell().PaddingBottom(1).Text(v2);
                        }
                    });

                    // Section 2.
                    col.Item().PaddingTop(8).Text("SECTION 2: Gross Salary (Annual)").Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().PaddingTop(2).Table(t => Fill(t,
                        ("Basic Salary", form.GrossSalary.Basic, false),
                        ("HRA Received", form.GrossSalary.Hra, false),
                        ("DA", form.GrossSalary.Da, false),
                        ("Special Allowance", form.GrossSalary.SpecialAllowance, false),
                        ("LTA", form.GrossSalary.Lta, false),
                        ("Other Allowances", form.GrossSalary.OtherAllowances, false),
                        ("Total Gross Salary", form.GrossSalary.Total, true)));

                    // Section 3.
                    col.Item().PaddingTop(8).Text("SECTION 3: Exemptions & Deductions (Section 10)").Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().PaddingTop(2).Table(t => Fill(t,
                        ("HRA Exemption u/s 10(13A)", form.Exemptions.HraExemption, false),
                        ("LTA Exemption", form.Exemptions.LtaExemption, false),
                        ("Standard Deduction u/s 16(ii)", form.Exemptions.StandardDeduction, false),
                        ("Professional Tax u/s 16(iii)", form.Exemptions.ProfessionalTax, false),
                        ("Total Exemptions", form.Exemptions.Total, true)));

                    // Section 4.
                    col.Item().PaddingTop(8).Text("SECTION 4: Income Chargeable under Salaries").Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().PaddingTop(2).Background(Colors.Grey.Lighten3).Padding(4)
                        .AlignRight().Text(Rs(form.IncomeChargeableUnderSalaries)).Bold();

                    // Section 5.
                    col.Item().PaddingTop(8).Text("SECTION 5: Deductions under Chapter VI-A").Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().PaddingTop(2).Table(t => Fill(t,
                        ("80C (max 1,50,000)", form.ChapterVIA.Section80C, false),
                        ("80CCD(1B) (max 50,000)", form.ChapterVIA.Section80CCD1B, false),
                        ("80D (max 25,000)", form.ChapterVIA.Section80D, false),
                        ("80TTA (max 10,000)", form.ChapterVIA.Section80TTA, false),
                        ("80CCD(2) - Employer NPS (both regimes)", form.ChapterVIA.Section80CCD2, false),
                        ("Total Chapter VI-A", form.ChapterVIA.Total, true)));

                    // Section 6.
                    col.Item().PaddingTop(8).Text("SECTION 6: Taxable Income (rounded to nearest Rs 10)").Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().PaddingTop(2).Background(Colors.Grey.Lighten3).Padding(4)
                        .AlignRight().Text(Rs(form.TaxableIncome)).Bold();

                    // Section 7.
                    col.Item().PaddingTop(8).Text($"SECTION 7: Tax Computation ({form.Tax.TaxRegime} Regime)").Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().PaddingTop(2).Table(t => Fill(t,
                        ("Tax on Taxable Income", form.Tax.TaxBeforeRebate, false),
                        ("Section 87A Rebate", -form.Tax.Rebate87A, false),
                        ("Surcharge", form.Tax.Surcharge, false),
                        ("Marginal Relief", -form.Tax.MarginalRelief, false),
                        ("Health & Education Cess (4%)", form.Tax.Cess, false),
                        ("Total Tax Payable", form.Tax.TotalTaxPayable, true)));

                    // Section 8.
                    col.Item().PaddingTop(8).Text("SECTION 8: TDS Summary").Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().PaddingTop(2).Table(t => Fill(t,
                        ("Total TDS deducted during the year", form.Tds.TotalTdsDeducted, false),
                        ("Balance Tax Payable", form.Tds.BalanceTaxPayable, false),
                        ("Refund", form.Tds.Refund, true)));
                });

                page.Footer().PaddingTop(6).Column(foot =>
                {
                    foot.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    foot.Item().PaddingTop(2).Row(r =>
                    {
                        r.RelativeItem().Text(
                            "This is a computer-generated Form 16 (Part B) and does not require signature.")
                            .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                        r.ConstantItem(140).AlignRight().Text($"Generated on: {form.GeneratedOn}  |  ")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        r.ConstantItem(70).AlignRight().Text(t =>
                        {
                            t.Span("Page ").FontSize(8).FontColor(Colors.Grey.Darken1);
                            t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                            t.Span(" of ").FontSize(8).FontColor(Colors.Grey.Darken1);
                            t.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    private static string EmployerNameSafe(this Form16PartB form, PayslipOptions options) =>
        string.IsNullOrWhiteSpace(form.EmployeeDetails.Employer.EmployerName)
            ? options.CompanyName
            : form.EmployeeDetails.Employer.EmployerName;

    // ---------------- Helpers ----------------

    private static decimal Sum(IEnumerable<PayrollRun> runs, string componentName) =>
        Round2(runs.SelectMany(r => r.Details ?? [])
            .Where(d => d.ComponentName == componentName)
            .Sum(d => d.Amount));

    private static string Rs(decimal value) => $"Rs {value.ToString("N2", InrCulture)}";

    private static decimal Round2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static void Fill(TableDescriptor table, params (string Label, decimal Amount, bool Bold)[] rows)
    {
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn();
            columns.ConstantColumn(120);
        });

        foreach ((string label, decimal amount, bool bold) in rows)
        {
            TextSpanDescriptor left = table.Cell().PaddingBottom(1).Text(label);
            TextSpanDescriptor right = table.Cell().PaddingBottom(1).AlignRight().Text(Rs(amount));
            if (bold)
            {
                left.Bold();
                right.Bold();
            }
        }
    }
}
