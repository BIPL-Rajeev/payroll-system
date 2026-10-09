using Microsoft.EntityFrameworkCore;
using PayrollApi.Core;
using PayrollApi.Core.Persistence;
using PayrollApi.Core.Services;

namespace PayrollApi;

/// <summary>
/// Excel export endpoints (Admin or HR role - the Writer policy):
///   GET /api/export/employees                        -> employees.xlsx
///   GET /api/export/payroll/{payrollRunId}           -> payslip_{code}_{month}_{year}.xlsx
///   GET /api/export/payroll-register/{month}/{year}  -> payroll_register_{month}_{year}.xlsx
///   GET /api/export/form16/{employeeId}/{fy}         -> form16_{code}_{FY}.xlsx
/// </summary>
public static class ExportEndpoints
{
    public static void MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/export");

        // GET /api/export/employees - all active employees as xlsx.
        group.MapGet("/employees", async (AppDbContext db, ILogger<Program> logger) =>
        {
            var employees = await db.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.EmployeeCode)
                .ToListAsync();

            byte[] xlsx = ExcelExporter.ExportEmployeesToExcel(employees);
            logger.LogInformation("Excel export: employees, rows={Rows}", employees.Count);

            return Results.File(xlsx, ExcelExporter.XlsxContentType, "employees.xlsx");
        })
        .WithName("ExportEmployees")
        .RequireAuthorization(UserRoles.WritePolicy);

        // GET /api/export/payroll/{payrollRunId} - one payslip as xlsx (404 if missing).
        group.MapGet("/payroll/{payrollRunId:int}", async (
            int payrollRunId, AppDbContext db, ILogger<Program> logger) =>
        {
            var run = await db.PayrollRuns
                .Include(r => r.Details)
                .Include(r => r.Employee)
                .FirstOrDefaultAsync(r => r.Id == payrollRunId);
            if (run is null)
            {
                logger.LogWarning("Excel payslip rejected: payroll run {RunId} not found", payrollRunId);
                return Results.NotFound(new { error = $"Payroll run {payrollRunId} not found." });
            }
            if (run.Employee is null)
            {
                return Results.NotFound(new { error = $"Employee {run.EmployeeId} not found." });
            }

            byte[] xlsx = ExcelExporter.ExportPayrollRunToExcel(
                run, run.Employee, run.Details.OrderBy(d => d.Id).ToList());
            string filename =
                $"payslip_{run.Employee.EmployeeCode}_{PayslipPdfGenerator.MonthName(run.MonthNumber)}_{run.Year}.xlsx";
            logger.LogInformation("Excel export: payslip runId={RunId} file={File}", payrollRunId, filename);

            return Results.File(xlsx, ExcelExporter.XlsxContentType, filename);
        })
        .WithName("ExportPayrollRun")
        .RequireAuthorization(UserRoles.WritePolicy);

        // GET /api/export/payroll-register/{month}/{year} - full register for one month (404 if no runs).
        group.MapGet("/payroll-register/{month:int}/{year:int}", async (
            int month, int year, AppDbContext db, ILogger<Program> logger) =>
        {
            if (month < 1 || month > PtCalculatorCore.MonthsInYear)
            {
                return Results.BadRequest(new { error = $"Month must be between 1 and 12 (got {month})." });
            }

            var runs = await db.PayrollRuns
                .Include(r => r.Details)
                .Include(r => r.Employee)
                .Where(r => r.MonthNumber == month && r.Year == year)
                .OrderBy(r => r.Id)
                .ToListAsync();

            if (runs.Count == 0)
            {
                logger.LogWarning("Excel register rejected: no runs for {Month}/{Year}", month, year);
                return Results.NotFound(new
                {
                    error = $"No payroll runs found for month {month} of {year}.",
                });
            }

            byte[] xlsx = ExcelExporter.ExportMonthlyPayrollRegisterToExcel(month, year, runs);
            string filename = $"payroll_register_{month}_{year}.xlsx";
            logger.LogInformation("Excel export: register {Month}/{Year}, rows={Rows}", month, year, runs.Count);

            return Results.File(xlsx, ExcelExporter.XlsxContentType, filename);
        })
        .WithName("ExportPayrollRegister")
        .RequireAuthorization(UserRoles.WritePolicy);

        // GET /api/export/form16/{employeeId}/{financialYear} - Form 16 Part B as xlsx.
        group.MapGet("/form16/{employeeId:int}/{financialYear}", async (
            int employeeId, string financialYear, AppDbContext db, ILogger<Program> logger) =>
        {
            if (!Form16Generator.IsValidFinancialYear(financialYear))
            {
                logger.LogWarning("Excel Form16 rejected: invalid financial year {FY}", financialYear);
                return Results.BadRequest(new
                {
                    error = $"Invalid financial year \"{financialYear}\". Expected format: 2025-26.",
                });
            }

            var employee = await db.Employees
                .FirstOrDefaultAsync(e => e.Id == employeeId && e.IsActive);
            if (employee is null)
            {
                logger.LogWarning("Excel Form16 rejected: employee {Id} not found", employeeId);
                return Results.NotFound(new { error = $"Employee {employeeId} not found." });
            }

            var runs = await db.PayrollRuns
                .Include(r => r.Details)
                .Where(r => r.EmployeeId == employeeId)
                .ToListAsync();

            if (!Form16Generator.RunsForFinancialYear(runs, financialYear).Any())
            {
                logger.LogWarning(
                    "Excel Form16 rejected: no runs for employee {Id} in FY {FY}", employeeId, financialYear);
                return Results.BadRequest(new
                {
                    error = $"No payroll runs found for employee {employeeId} for FY {financialYear}.",
                });
            }

            Form16PartB form = Form16Generator.Generate(employee, runs, financialYear);
            byte[] xlsx = ExcelExporter.ExportForm16DataToExcel(employee, financialYear, form);
            string filename = $"form16_{employee.EmployeeCode}_{financialYear}.xlsx";
            logger.LogInformation("Excel export: Form16 employeeId={Id} fy={FY}", employeeId, financialYear);

            return Results.File(xlsx, ExcelExporter.XlsxContentType, filename);
        })
        .WithName("ExportForm16")
        .RequireAuthorization(UserRoles.WritePolicy);

        // GET /api/export/history/{employeeId} - one employee's full run history as xlsx
        // (backs the "Export History" button on the history page).
        group.MapGet("/history/{employeeId:int}", async (
            int employeeId, AppDbContext db, ILogger<Program> logger) =>
        {
            var employee = await db.Employees
                .FirstOrDefaultAsync(e => e.Id == employeeId && e.IsActive);
            if (employee is null)
            {
                logger.LogWarning("Excel history rejected: employee {Id} not found", employeeId);
                return Results.NotFound(new { error = $"Employee {employeeId} not found." });
            }

            var runs = await db.PayrollRuns
                .Include(r => r.Details)
                .Where(r => r.EmployeeId == employeeId)
                .OrderBy(r => r.Year)
                .ThenBy(r => r.MonthNumber)
                .ToListAsync();

            byte[] xlsx = ExcelExporter.ExportEmployeeHistoryToExcel(employee, runs);
            string filename = $"payroll_history_{employee.EmployeeCode}.xlsx";
            logger.LogInformation(
                "Excel export: history employeeId={Id}, rows={Rows}", employeeId, runs.Count);

            return Results.File(xlsx, ExcelExporter.XlsxContentType, filename);
        })
        .WithName("ExportEmployeeHistory")
        .RequireAuthorization(UserRoles.WritePolicy);
    }
}
