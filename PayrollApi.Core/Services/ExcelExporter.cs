using System.Globalization;
using ClosedXML.Excel;
using PayrollApi.Core.Entities;

namespace PayrollApi.Core.Services;

// Inside the namespace so this alias wins over PayrollApi.Core.Employee (the calculation model).
using Employee = PayrollApi.Core.Entities.Employee;

/// <summary>
/// Excel (xlsx) exporters built on ClosedXML. Salary columns use the Indian
/// currency number format "Rs #,##,##0.00" (en-IN grouping), every sheet has a
/// bold header row and auto-fit columns.
/// </summary>
public static class ExcelExporter
{
    private static readonly CultureInfo InrCulture = CultureInfo.GetCultureInfo("en-IN");

    /// <summary>Indian currency Excel number format: Rs 1,23,456.00.</summary>
    public const string InrCurrencyFormat = "\"Rs \"#,##,##0.00";

    private const string XlsxMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // Component names written by the payroll run endpoint (PayrollRunEndpoints.BuildDetails).
    private const string EmployeePf = "Employee PF";
    private const string EmployeeEsi = "Employee ESI";
    private const string ProfessionalTax = "Professional Tax";
    private const string Tds = "TDS";
    private const string EmployerEps = "Employer EPS";
    private const string EmployerEpf = "Employer EPF";
    private const string EmployerEsi = "Employer ESI";

    // ---------------- (a) Employee master export ----------------

    /// <summary>
    /// Employee master sheet: EmployeeCode, Name, State, TaxRegime, MonthlyBasic,
    /// MonthlyHRA, MonthlyDA, TotalMonthlyGross, IsActive.
    /// </summary>
    public static byte[] ExportEmployeesToExcel(List<Employee> employees)
    {
        ArgumentNullException.ThrowIfNull(employees);

        using var workbook = new XLWorkbook();
        IXLWorksheet ws = workbook.Worksheets.Add("Employees");

        string[] headers =
        [
            "EmployeeCode", "Name", "State", "TaxRegime",
            "MonthlyBasic", "MonthlyHRA", "MonthlyDA", "TotalMonthlyGross", "IsActive",
        ];
        WriteHeader(ws, headers);

        int row = 2;
        foreach (Employee e in employees)
        {
            ws.Cell(row, 1).Value = e.EmployeeCode;
            ws.Cell(row, 2).Value = e.Name;
            ws.Cell(row, 3).Value = e.State;
            ws.Cell(row, 4).Value = string.Equals(e.TaxRegime, "old", StringComparison.OrdinalIgnoreCase) ? "Old" : "New";
            SetCurrency(ws.Cell(row, 5), e.MonthlyBasic);
            SetCurrency(ws.Cell(row, 6), e.MonthlyHra);
            SetCurrency(ws.Cell(row, 7), e.MonthlyDa);
            // Full gross: Basic + HRA + DA + Special + LTA + Other allowances.
            SetCurrency(ws.Cell(row, 8), e.MonthlyBasic + e.MonthlyHra + e.MonthlyDa
                + e.MonthlySpecialAllowance + e.MonthlyLta + e.MonthlyOtherAllowances);
            ws.Cell(row, 9).Value = e.IsActive;
            row++;
        }

        return ToBytes(workbook, ws);
    }

    // ---------------- (b) Single payslip export ----------------

    /// <summary>One employee payslip as Excel with Earnings / Deductions / Employer Contributions sections.</summary>
    public static byte[] ExportPayrollRunToExcel(
        PayrollRun run, Employee employee, List<PayrollRunDetail> details)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(employee);
        ArgumentNullException.ThrowIfNull(details);

        using var workbook = new XLWorkbook();
        IXLWorksheet ws = workbook.Worksheets.Add("Payslip");

        // Header block.
        ws.Cell(1, 1).Value = "Payslip";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = "Employee Code";
        ws.Cell(2, 2).Value = employee.EmployeeCode;
        ws.Cell(3, 1).Value = "Employee Name";
        ws.Cell(3, 2).Value = employee.Name;
        ws.Cell(4, 1).Value = "Period";
        ws.Cell(4, 2).Value = $"{PayslipPdfGenerator.MonthName(run.MonthNumber)} {run.Year}";

        int row = 6;

        void Section(string title, string type, string totalLabel, decimal total)
        {
            ws.Cell(row, 1).Value = title;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            row++;

            ws.Cell(row, 1).Value = "Component";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = "Amount (Rs)";
            ws.Cell(row, 2).Style.Font.Bold = true;
            row++;

            foreach (PayrollRunDetail d in details.Where(d => d.ComponentType == type).OrderBy(d => d.Id))
            {
                ws.Cell(row, 1).Value = d.ComponentName;
                SetCurrency(ws.Cell(row, 2), d.Amount);
                row++;
            }

            ws.Cell(row, 1).Value = totalLabel;
            ws.Cell(row, 1).Style.Font.Bold = true;
            SetCurrency(ws.Cell(row, 2), total);
            ws.Cell(row, 2).Style.Font.Bold = true;
            row += 2;
        }

        Section("EARNINGS", ComponentTypes.Earning, "Gross Monthly Salary", run.GrossSalary);
        Section("DEDUCTIONS", ComponentTypes.Deduction, "Total Deductions", run.TotalDeductions);
        Section("EMPLOYER CONTRIBUTIONS", ComponentTypes.EmployerContribution, "Total Employer Cost", run.TotalEmployerCost);

        ws.Cell(row, 1).Value = "Net Monthly Take-Home";
        ws.Cell(row, 1).Style.Font.Bold = true;
        SetCurrency(ws.Cell(row, 2), run.NetPay);
        ws.Cell(row, 2).Style.Font.Bold = true;
        row++;

        ws.Cell(row, 1).Value = "Total CTC";
        ws.Cell(row, 1).Style.Font.Bold = true;
        SetCurrency(ws.Cell(row, 2), run.TotalCtc);
        ws.Cell(row, 2).Style.Font.Bold = true;

        return ToBytes(workbook, ws);
    }

    // ---------------- (c) Monthly payroll register ----------------

    /// <summary>
    /// Payroll register for all employees in one month: per-run rows plus a
    /// bold TOTAL row at the bottom.
    /// </summary>
    public static byte[] ExportMonthlyPayrollRegisterToExcel(int month, int year, List<PayrollRun> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);

        using var workbook = new XLWorkbook();
        IXLWorksheet ws = workbook.Worksheets.Add($"Register {month:00}-{year}");

        string[] headers =
        [
            "EmployeeCode", "Name", "Gross", "EmployeePF", "EmployeeESI", "PT", "TDS",
            "TotalDeductions", "NetPay", "EmployerPF", "EmployerESI", "TotalCTC",
        ];
        WriteHeader(ws, headers);

        int row = 2;
        decimal totGross = 0m, totEmpPf = 0m, totEmpEsi = 0m, totPt = 0m, totTds = 0m,
            totDed = 0m, totNet = 0m, totErPf = 0m, totErEsi = 0m, totCtc = 0m;

        foreach (PayrollRun run in runs)
        {
            List<PayrollRunDetail> details = [.. run.Details ?? []];
            decimal empPf = Sum(details, EmployeePf);
            decimal empEsi = Sum(details, EmployeeEsi);
            decimal pt = Sum(details, ProfessionalTax);
            decimal tds = Sum(details, Tds);
            decimal employerPf = Sum(details, EmployerEps) + Sum(details, EmployerEpf);
            decimal employerEsi = Sum(details, EmployerEsi);

            ws.Cell(row, 1).Value = run.Employee?.EmployeeCode ?? $"#{run.EmployeeId}";
            ws.Cell(row, 2).Value = run.Employee?.Name ?? string.Empty;
            SetCurrency(ws.Cell(row, 3), run.GrossSalary);
            SetCurrency(ws.Cell(row, 4), empPf);
            SetCurrency(ws.Cell(row, 5), empEsi);
            SetCurrency(ws.Cell(row, 6), pt);
            SetCurrency(ws.Cell(row, 7), tds);
            SetCurrency(ws.Cell(row, 8), run.TotalDeductions);
            SetCurrency(ws.Cell(row, 9), run.NetPay);
            SetCurrency(ws.Cell(row, 10), employerPf);
            SetCurrency(ws.Cell(row, 11), employerEsi);
            SetCurrency(ws.Cell(row, 12), run.TotalCtc);

            totGross += run.GrossSalary;
            totEmpPf += empPf;
            totEmpEsi += empEsi;
            totPt += pt;
            totTds += tds;
            totDed += run.TotalDeductions;
            totNet += run.NetPay;
            totErPf += employerPf;
            totErEsi += employerEsi;
            totCtc += run.TotalCtc;
            row++;
        }

        // TOTAL row.
        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 2).Value = runs.Count;
        ws.Cell(row, 3).Value = totGross;
        ws.Cell(row, 4).Value = totEmpPf;
        ws.Cell(row, 5).Value = totEmpEsi;
        ws.Cell(row, 6).Value = totPt;
        ws.Cell(row, 7).Value = totTds;
        ws.Cell(row, 8).Value = totDed;
        ws.Cell(row, 9).Value = totNet;
        ws.Cell(row, 10).Value = totErPf;
        ws.Cell(row, 11).Value = totErEsi;
        ws.Cell(row, 12).Value = totCtc;
        IXLRange totalRange = ws.Range(row, 1, row, 12);
        totalRange.Style.Font.Bold = true;
        totalRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        return ToBytes(workbook, ws);
    }

    // ---------------- (c2) Per-employee run history ----------------

    /// <summary>One employee's full payroll run history with a TOTAL row (backs the history page export).</summary>
    public static byte[] ExportEmployeeHistoryToExcel(Employee employee, List<PayrollRun> runs)
    {
        ArgumentNullException.ThrowIfNull(employee);
        ArgumentNullException.ThrowIfNull(runs);

        using var workbook = new XLWorkbook();
        IXLWorksheet ws = workbook.Worksheets.Add("History");

        string[] headers = ["Period", "Gross", "TotalDeductions", "NetPay", "TotalCTC"];
        WriteHeader(ws, headers);

        int row = 2;
        decimal totGross = 0m, totDed = 0m, totNet = 0m, totCtc = 0m;

        foreach (PayrollRun run in runs)
        {
            ws.Cell(row, 1).Value = $"{PayslipPdfGenerator.MonthName(run.MonthNumber)} {run.Year}";
            SetCurrency(ws.Cell(row, 2), run.GrossSalary);
            SetCurrency(ws.Cell(row, 3), run.TotalDeductions);
            SetCurrency(ws.Cell(row, 4), run.NetPay);
            SetCurrency(ws.Cell(row, 5), run.TotalCtc);

            totGross += run.GrossSalary;
            totDed += run.TotalDeductions;
            totNet += run.NetPay;
            totCtc += run.TotalCtc;
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 2).Value = totGross;
        ws.Cell(row, 3).Value = totDed;
        ws.Cell(row, 4).Value = totNet;
        ws.Cell(row, 5).Value = totCtc;
        ws.Range(row, 1, row, 5).Style.Font.Bold = true;
        ws.Range(row, 1, row, 5).Style.Fill.BackgroundColor = XLColor.LightGray;

        return ToBytes(workbook, ws);
    }

    // ---------------- (d) Form 16 Part B export ----------------

    /// <summary>All Form 16 Part B sections in one Excel sheet with proper formatting.</summary>
    public static byte[] ExportForm16DataToExcel(Employee employee, string financialYear, Form16PartB form)
    {
        ArgumentNullException.ThrowIfNull(employee);
        ArgumentNullException.ThrowIfNull(form);

        using var workbook = new XLWorkbook();
        IXLWorksheet ws = workbook.Worksheets.Add("Form16 Part B");

        ws.Cell(1, 1).Value = "FORM 16 - PART B";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        int row = 3;

        void Label(string label, string value)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = value;
            row++;
        }

        void Amount(string label, decimal amount, bool bold = false)
        {
            ws.Cell(row, 1).Value = label;
            SetCurrency(ws.Cell(row, 2), amount);
            if (bold)
            {
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 2).Style.Font.Bold = true;
            }
            row++;
        }

        void SectionTitle(string title)
        {
            row++;
            ws.Cell(row, 1).Value = title;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
            row++;
        }

        // Section 1: employee & employer details.
        SectionTitle("SECTION 1: Employee & Employer Details");
        Label("Employee Name", form.EmployeeDetails.EmployeeName);
        Label("PAN", form.EmployeeDetails.EmployeePan);
        Label("Employee Code", employee.EmployeeCode);
        Label("Tax Regime", form.EmployeeDetails.TaxRegime);
        Label("Employer Name", form.EmployeeDetails.Employer.EmployerName);
        Label("Employer PAN", form.EmployeeDetails.Employer.EmployerPan);
        Label("Employer TAN", form.EmployeeDetails.Employer.EmployerTan);
        Label("Financial Year", form.EmployeeDetails.FinancialYear);
        Label("Assessment Year", form.EmployeeDetails.AssessmentYear);

        // Section 2: gross salary.
        SectionTitle("SECTION 2: Gross Salary (Annual)");
        Amount("Basic Salary", form.GrossSalary.Basic);
        Amount("HRA Received", form.GrossSalary.Hra);
        Amount("DA", form.GrossSalary.Da);
        Amount("Special Allowance", form.GrossSalary.SpecialAllowance);
        Amount("LTA", form.GrossSalary.Lta);
        Amount("Other Allowances", form.GrossSalary.OtherAllowances);
        Amount("Total Gross Salary", form.GrossSalary.Total, bold: true);

        // Section 3: exemptions.
        SectionTitle("SECTION 3: Exemptions & Deductions (Section 10)");
        Amount("HRA Exemption u/s 10(13A)", form.Exemptions.HraExemption);
        Amount("LTA Exemption", form.Exemptions.LtaExemption);
        Amount("Standard Deduction u/s 16(ii)", form.Exemptions.StandardDeduction);
        Amount("Professional Tax u/s 16(iii)", form.Exemptions.ProfessionalTax);
        Amount("Total Exemptions", form.Exemptions.Total, bold: true);

        // Section 4.
        SectionTitle("SECTION 4: Income Chargeable under Salaries");
        Amount("Gross Salary - Total Exemptions", form.IncomeChargeableUnderSalaries, bold: true);

        // Section 5: Chapter VI-A.
        SectionTitle("SECTION 5: Deductions under Chapter VI-A");
        Amount("80C", form.ChapterVIA.Section80C);
        Amount("80CCD(1B)", form.ChapterVIA.Section80CCD1B);
        Amount("80D", form.ChapterVIA.Section80D);
        Amount("80TTA", form.ChapterVIA.Section80TTA);
        Amount("80CCD(2) - Employer NPS", form.ChapterVIA.Section80CCD2);
        Amount("Total Chapter VI-A", form.ChapterVIA.Total, bold: true);

        // Section 6.
        SectionTitle("SECTION 6: Taxable Income (rounded to nearest Rs 10)");
        Amount("Income under Salaries - Chapter VI-A", form.TaxableIncome, bold: true);

        // Section 7: tax computation.
        SectionTitle($"SECTION 7: Tax Computation ({form.Tax.TaxRegime} Regime)");
        Amount("Tax on Taxable Income", form.Tax.TaxBeforeRebate);
        Amount("Section 87A Rebate", -form.Tax.Rebate87A);
        Amount("Surcharge", form.Tax.Surcharge);
        Amount("Marginal Relief", -form.Tax.MarginalRelief);
        Amount("Health & Education Cess (4%)", form.Tax.Cess);
        Amount("Total Tax Payable", form.Tax.TotalTaxPayable, bold: true);

        // Section 8: TDS summary.
        SectionTitle("SECTION 8: TDS Summary");
        Amount("Total TDS deducted during the year", form.Tds.TotalTdsDeducted);
        Amount("Balance Tax Payable", form.Tds.BalanceTaxPayable, bold: true);
        Amount("Refund", form.Tds.Refund, bold: true);

        Label("Generated On", form.GeneratedOn);

        return ToBytes(workbook, ws);
    }

    // ---------------- Helpers ----------------

    private static void WriteHeader(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
        }

        ws.Row(1).Style.Font.Bold = true;
        ws.Row(1).Style.Fill.BackgroundColor = XLColor.LightGray;
    }

    private static void SetCurrency(IXLCell cell, decimal amount)
    {
        cell.Value = amount;
        cell.Style.NumberFormat.Format = InrCurrencyFormat;
    }

    private static decimal Sum(List<PayrollRunDetail> details, string componentName) =>
        details.Where(d => d.ComponentName == componentName).Sum(d => d.Amount);

    /// <summary>Auto-fits columns (from row 2 on to keep the data readable), saves to a byte[].</summary>
    private static byte[] ToBytes(XLWorkbook workbook, IXLWorksheet ws)
    {
        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Content type for xlsx downloads.</summary>
    public static string XlsxContentType => XlsxMimeType;
}
