using ClosedXML.Excel;
using PayrollApi.Core.Entities;
using PayrollApi.Core.Services;
using Xunit;
using Employee = PayrollApi.Core.Entities.Employee;

namespace PayrollApi.Tests;

public class ExcelExporterTests
{
    // Every xlsx is a ZIP archive: must start with the "PK" magic bytes.
    private static void AssertPkMagic(byte[] xlsx)
    {
        Assert.NotEmpty(xlsx);
        Assert.True(xlsx.Length > 100, $"xlsx suspiciously small: {xlsx.Length} bytes");
        Assert.Equal((byte)'P', xlsx[0]);
        Assert.Equal((byte)'K', xlsx[1]);
    }

    private static Employee SampleEmployee(string code = "EMP001", string name = "Asha Patil") => new()
    {
        Id = 1,
        EmployeeCode = code,
        Name = name,
        State = "Maharashtra",
        IsMetro = true,
        TaxRegime = "old",
        Pan = "ABCDE1234F",
        MonthlyBasic = 50_000m,
        MonthlyHra = 20_000m,
        MonthlyDa = 5_000m,
        MonthlySpecialAllowance = 5_000m,
        IsActive = true,
    };

    private static PayrollRun SampleRun(int id, int employeeId, string code, string name, int month = 1)
    {
        var run = new PayrollRun
        {
            Id = id,
            EmployeeId = employeeId,
            MonthNumber = month,
            Year = 2026,
            GrossSalary = 80_000m,
            TotalDeductions = 5_467m,
            NetPay = 74_533m,
            TotalEmployerCost = 1_950m,
            TotalCtc = 81_950m,
            GeneratedAt = DateTime.UtcNow,
            Employee = SampleEmployee(code, name),
        };

        run.Details = new List<PayrollRunDetail>
        {
            new() { ComponentName = "Basic Salary", ComponentType = ComponentTypes.Earning, Amount = 50_000m },
            new() { ComponentName = "House Rent Allowance", ComponentType = ComponentTypes.Earning, Amount = 20_000m },
            new() { ComponentName = "Dearness Allowance", ComponentType = ComponentTypes.Earning, Amount = 5_000m },
            new() { ComponentName = "Special Allowance", ComponentType = ComponentTypes.Earning, Amount = 5_000m },
            new() { ComponentName = "Employee PF", ComponentType = ComponentTypes.Deduction, Amount = 1_800m },
            new() { ComponentName = "Employee ESI", ComponentType = ComponentTypes.Deduction, Amount = 0m },
            new() { ComponentName = "Professional Tax", ComponentType = ComponentTypes.Deduction, Amount = 200m },
            new() { ComponentName = "TDS", ComponentType = ComponentTypes.Deduction, Amount = 3_467m },
            new() { ComponentName = "Employer EPS", ComponentType = ComponentTypes.EmployerContribution, Amount = 1_249.50m },
            new() { ComponentName = "Employer EPF", ComponentType = ComponentTypes.EmployerContribution, Amount = 550.50m },
            new() { ComponentName = "Employer ESI", ComponentType = ComponentTypes.EmployerContribution, Amount = 0m },
        };

        return run;
    }

    // Required: export employees -> not empty and starts with "PK" (ZIP magic bytes).
    [Fact]
    public void ExportEmployees_ReturnsNonEmptyZipFile()
    {
        byte[] xlsx = ExcelExporter.ExportEmployeesToExcel([SampleEmployee()]);

        AssertPkMagic(xlsx);
    }

    // Required: export with empty list -> valid file with just headers.
    [Fact]
    public void ExportEmployees_EmptyList_ProducesHeadersOnly()
    {
        byte[] xlsx = ExcelExporter.ExportEmployeesToExcel([]);

        AssertPkMagic(xlsx);

        using var stream = new MemoryStream(xlsx, writable: false);
        using var wb = new XLWorkbook(stream);
        IXLWorksheet ws = wb.Worksheets.First();

        // Header row present...
        Assert.Equal("EmployeeCode", ws.Cell(1, 1).GetString());
        Assert.Equal("TotalMonthlyGross", ws.Cell(1, 8).GetString());
        Assert.Equal("IsActive", ws.Cell(1, 9).GetString());
        // ...and no data rows.
        Assert.Equal(1, ws.LastRowUsed().RowNumber());
        Assert.True(ws.Row(1).Style.Font.Bold, "Header row should be bold.");
    }

    // Employees sheet: values, currency format, auto-fit (column width set).
    [Fact]
    public void ExportEmployees_ContainsData_WithIndianCurrencyFormat()
    {
        Employee e = SampleEmployee();
        byte[] xlsx = ExcelExporter.ExportEmployeesToExcel([e]);

        using var stream = new MemoryStream(xlsx, writable: false);
        using var wb = new XLWorkbook(stream);
        IXLWorksheet ws = wb.Worksheets.First();

        Assert.Equal("EMP001", ws.Cell(2, 1).GetString());
        Assert.Equal("Asha Patil", ws.Cell(2, 2).GetString());
        Assert.Equal(50_000m, ws.Cell(2, 5).GetValue<decimal>());
        // TotalMonthlyGross = Basic + HRA + DA + Special (50,000 + 20,000 + 5,000 + 5,000).
        Assert.Equal(80_000m, ws.Cell(2, 8).GetValue<decimal>());
        Assert.True(ws.Cell(2, 9).GetValue<bool>());

        // Indian currency number format "Rs #,##,##0.00" on salary columns.
        Assert.Contains("#,##,##0.00", ws.Cell(2, 5).Style.NumberFormat.Format);
        Assert.Contains("Rs", ws.Cell(2, 5).Style.NumberFormat.Format);
    }

    // Required: export payroll register -> contains all employees and the TOTAL row.
    [Fact]
    public void ExportPayrollRegister_ContainsAllEmployees_AndTotalRow()
    {
        var runs = new List<PayrollRun>
        {
            SampleRun(1, 1, "EMP001", "Asha Patil"),
            SampleRun(2, 2, "EMP002", "Rahul Verma"),
        };

        byte[] xlsx = ExcelExporter.ExportMonthlyPayrollRegisterToExcel(1, 2026, runs);
        AssertPkMagic(xlsx);

        using var stream = new MemoryStream(xlsx, writable: false);
        using var wb = new XLWorkbook(stream);
        IXLWorksheet ws = wb.Worksheets.First();

        // Header row.
        Assert.Equal("EmployeeCode", ws.Cell(1, 1).GetString());
        Assert.Equal("TotalCTC", ws.Cell(1, 12).GetString());

        // All employees present (codes and names).
        Assert.Equal("EMP001", ws.Cell(2, 1).GetString());
        Assert.Equal("Asha Patil", ws.Cell(2, 2).GetString());
        Assert.Equal("EMP002", ws.Cell(3, 1).GetString());
        Assert.Equal("Rahul Verma", ws.Cell(3, 2).GetString());

        // Per-employee component amounts come from the run details.
        Assert.Equal(1_800m, ws.Cell(2, 4).GetValue<decimal>());   // EmployeePF
        Assert.Equal(200m, ws.Cell(2, 6).GetValue<decimal>());     // PT
        Assert.Equal(3_467m, ws.Cell(2, 7).GetValue<decimal>());   // TDS
        Assert.Equal(1_800m, ws.Cell(2, 10).GetValue<decimal>());  // EmployerPF = EPS + EPF
        Assert.Equal(74_533m, ws.Cell(2, 9).GetValue<decimal>());  // NetPay

        // TOTAL row at the bottom with summed values.
        IXLRow lastRow = ws.LastRowUsed();
        Assert.Equal("TOTAL", lastRow.Cell(1).GetString());
        Assert.Equal(2, lastRow.Cell(2).GetValue<int>());                          // run count
        Assert.Equal(2 * 80_000m, lastRow.Cell(3).GetValue<decimal>());            // Gross
        Assert.Equal(2 * 74_533m, lastRow.Cell(9).GetValue<decimal>());            // NetPay
        Assert.Equal(2 * 81_950m, lastRow.Cell(12).GetValue<decimal>());           // TotalCTC
        Assert.True(lastRow.Cell(1).Style.Font.Bold, "TOTAL row should be bold.");
    }

    // Register with an empty list -> valid file, header + TOTAL row only.
    [Fact]
    public void ExportPayrollRegister_EmptyList_ProducesHeadersAndTotalOnly()
    {
        byte[] xlsx = ExcelExporter.ExportMonthlyPayrollRegisterToExcel(2, 2026, []);
        AssertPkMagic(xlsx);

        using var stream = new MemoryStream(xlsx, writable: false);
        using var wb = new XLWorkbook(stream);
        IXLWorksheet ws = wb.Worksheets.First();

        Assert.Equal("EmployeeCode", ws.Cell(1, 1).GetString());
        IXLRow lastRow = ws.LastRowUsed();
        Assert.Equal("TOTAL", lastRow.Cell(1).GetString());
        Assert.Equal(0m, lastRow.Cell(9).GetValue<decimal>());
    }

    // Payslip export: valid xlsx with the three sections and totals.
    [Fact]
    public void ExportPayrollRun_ProducesValidXlsx_WithSections()
    {
        PayrollRun run = SampleRun(1, 1, "EMP001", "Asha Patil");
        Employee employee = run.Employee!;

        byte[] xlsx = ExcelExporter.ExportPayrollRunToExcel(
            run, employee, run.Details.OrderBy(d => d.Id).ToList());
        AssertPkMagic(xlsx);

        using var stream = new MemoryStream(xlsx, writable: false);
        using var wb = new XLWorkbook(stream);
        IXLWorksheet ws = wb.Worksheets.First();

        Assert.Equal("EMP001", ws.Cell(2, 2).GetString());

        // All three sections and the closing totals are present (find by title, not fixed rows).
        List<string> colA = ws.RowsUsed().Select(r => r.Cell(1).GetString()).ToList();
        Assert.Contains("EARNINGS", colA);
        Assert.Contains("DEDUCTIONS", colA);
        Assert.Contains("EMPLOYER CONTRIBUTIONS", colA);
        Assert.Contains("Gross Monthly Salary", colA);
        Assert.Contains("Total Deductions", colA);
        Assert.Contains("Total Employer Cost", colA);
        Assert.Contains("Net Monthly Take-Home", colA);
        Assert.Contains("Total CTC", colA);

        // Deduction section shows the component amounts (numeric, currency-formatted).
        var pfRow = ws.RowsUsed()
            .First(r => r.Cell(1).GetString() == "Employee PF");
        Assert.Equal(1_800m, pfRow.Cell(2).GetValue<decimal>());
        Assert.Contains("#,##,##0.00", pfRow.Cell(2).Style.NumberFormat.Format);
    }

    // Form 16 export: valid xlsx with all eight sections present.
    [Fact]
    public void ExportForm16Data_ProducesValidXlsx_WithAllSections()
    {
        Employee employee = SampleEmployee();
        PayrollRun run = SampleRun(1, 1, employee.EmployeeCode, employee.Name);
        Form16PartB form = Form16Generator.Generate(employee, [run], "2025-26");

        byte[] xlsx = ExcelExporter.ExportForm16DataToExcel(employee, "2025-26", form);
        AssertPkMagic(xlsx);

        using var stream = new MemoryStream(xlsx, writable: false);
        using var wb = new XLWorkbook(stream);
        IXLWorksheet ws = wb.Worksheets.First();

        string sheetText = string.Concat(
            ws.RowsUsed().Select(r => r.Cell(1).GetString() + "|"));

        Assert.Contains("FORM 16 - PART B", sheetText);
        Assert.Contains("SECTION 1: Employee & Employer Details", sheetText);
        Assert.Contains("SECTION 2: Gross Salary (Annual)", sheetText);
        Assert.Contains("SECTION 3: Exemptions & Deductions (Section 10)", sheetText);
        Assert.Contains("SECTION 4: Income Chargeable under Salaries", sheetText);
        Assert.Contains("SECTION 5: Deductions under Chapter VI-A", sheetText);
        Assert.Contains("SECTION 6: Taxable Income", sheetText);
        Assert.Contains("SECTION 7: Tax Computation", sheetText);
        Assert.Contains("SECTION 8: TDS Summary", sheetText);
        Assert.Contains("Total Tax Payable", sheetText);
    }

    // History export: per-employee rows + TOTAL.
    [Fact]
    public void ExportEmployeeHistory_ProducesRows_AndTotal()
    {
        Employee employee = SampleEmployee();
        var runs = new List<PayrollRun>
        {
            SampleRun(1, 1, "EMP001", "Asha Patil", month: 1),
            SampleRun(2, 1, "EMP001", "Asha Patil", month: 2),
        };

        byte[] xlsx = ExcelExporter.ExportEmployeeHistoryToExcel(employee, runs);
        AssertPkMagic(xlsx);

        using var stream = new MemoryStream(xlsx, writable: false);
        using var wb = new XLWorkbook(stream);
        IXLWorksheet ws = wb.Worksheets.First();

        Assert.Equal("NetPay", ws.Cell(1, 4).GetString());
        IXLRow lastRow = ws.LastRowUsed();
        Assert.Equal("TOTAL", lastRow.Cell(1).GetString());
        Assert.Equal(2 * 74_533m, lastRow.Cell(4).GetValue<decimal>());
    }

    // Null arguments are rejected at the calculation boundary.
    [Fact]
    public void Exporters_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => ExcelExporter.ExportEmployeesToExcel(null!));
        Assert.Throws<ArgumentNullException>(() =>
            ExcelExporter.ExportPayrollRunToExcel(null!, SampleEmployee(), []));
        Assert.Throws<ArgumentNullException>(() =>
            ExcelExporter.ExportPayrollRunToExcel(SampleRun(1, 1, "E", "N"), null!, []));
        Assert.Throws<ArgumentNullException>(() =>
            ExcelExporter.ExportMonthlyPayrollRegisterToExcel(1, 2026, null!));
        Assert.Throws<ArgumentNullException>(() =>
            ExcelExporter.ExportForm16DataToExcel(null!, "2025-26",
                Form16Generator.Generate(SampleEmployee(), [SampleRun(1, 1, "E", "N")], "2025-26")));
        Assert.Throws<ArgumentNullException>(() =>
            ExcelExporter.ExportEmployeeHistoryToExcel(null!, []));
    }
}
