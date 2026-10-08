using Microsoft.EntityFrameworkCore;
using PayrollApi.Core;
using PayrollApi.Core.Entities;
using PayrollApi.Core.Persistence;
using PayrollApi.Core.Services;
using CalcEmployee = PayrollApi.Core.Employee;
using Employee = PayrollApi.Core.Entities.Employee;

namespace PayrollApi;

/// <summary>Persistence endpoints: generate a payroll run, list history, fetch a slip.</summary>
public static class PayrollRunEndpoints
{
    public static void MapPayrollRunEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/payroll/run/{employeeId}/{month}/{year} - calculate and SAVE the run + details.
        app.MapPost("/api/payroll/run/{employeeId:int}/{month:int}/{year:int}", async (
            int employeeId, int month, int year, AppDbContext db, ILogger<Program> logger) =>
        {
            if (month < 1 || month > PtCalculatorCore.MonthsInYear)
            {
                return Results.BadRequest(new { error = $"Month must be between 1 and 12 (got {month})." });
            }

            var employee = await db.Employees
                .FirstOrDefaultAsync(e => e.Id == employeeId && e.IsActive);
            if (employee is null)
            {
                logger.LogWarning("Payroll run rejected: employee {Id} not found", employeeId);
                return Results.NotFound(new { error = $"Employee {employeeId} not found." });
            }

            PayrollSlip slip = PayrollCalculatorCore.Calculate(ToCalculationInput(employee, month));

            PayrollRun run = new()
            {
                EmployeeId = employee.Id,
                MonthNumber = month,
                Year = year,
                GrossSalary = slip.GrossMonthlySalary,
                TotalDeductions = slip.TotalDeductions,
                NetPay = slip.NetMonthlyTakeHome,
                TotalEmployerCost = slip.TotalEmployerCost,
                TotalCtc = slip.TotalCtc,
                GeneratedAt = DateTime.UtcNow,
                Details = BuildDetails(slip),
            };

            db.PayrollRuns.Add(run);
            await db.SaveChangesAsync();

            logger.LogInformation(
                "Payroll run saved: runId={RunId} employeeId={EmployeeId} {Month}/{Year} netPay={Net}",
                run.Id, employeeId, month, year, run.NetPay);

            return Results.Ok(ToRunResponse(run));
        })
        .WithName("RunPayroll");

        // GET /api/payroll/history/{employeeId} - all past payroll runs for an employee.
        app.MapGet("/api/payroll/history/{employeeId:int}", async (
            int employeeId, AppDbContext db, ILogger<Program> logger) =>
        {
            bool employeeExists = await db.Employees.AnyAsync(e => e.Id == employeeId);
            if (!employeeExists)
            {
                logger.LogWarning("History rejected: employee {Id} not found", employeeId);
                return Results.NotFound(new { error = $"Employee {employeeId} not found." });
            }

            var runs = await db.PayrollRuns
                .Where(r => r.EmployeeId == employeeId)
                .OrderBy(r => r.Year)
                .ThenBy(r => r.MonthNumber)
                .Select(r => new
                {
                    payrollRunId = r.Id,
                    r.EmployeeId,
                    r.MonthNumber,
                    r.Year,
                    r.GrossSalary,
                    r.TotalDeductions,
                    r.NetPay,
                    r.TotalEmployerCost,
                    r.TotalCtc,
                    r.GeneratedAt,
                })
                .ToListAsync();

            return Results.Ok(runs);
        })
        .WithName("PayrollHistory");

        // GET /api/payroll/slip/{payrollRunId} - one slip with all details.
        app.MapGet("/api/payroll/slip/{payrollRunId:int}", async (
            int payrollRunId, AppDbContext db) =>
        {
            var run = await db.PayrollRuns
                .Include(r => r.Details)
                .FirstOrDefaultAsync(r => r.Id == payrollRunId);

            return run is null
                ? Results.NotFound(new { error = $"Payroll run {payrollRunId} not found." })
                : Results.Ok(ToRunResponse(run));
        })
        .WithName("PayrollSlip");

        // GET /api/payroll/slip/{payrollRunId}/pdf - downloadable PDF payslip.
        app.MapGet("/api/payroll/slip/{payrollRunId:int}/pdf", async (
            int payrollRunId, AppDbContext db, ILogger<Program> logger) =>
        {
            var run = await db.PayrollRuns
                .Include(r => r.Details)
                .FirstOrDefaultAsync(r => r.Id == payrollRunId);
            if (run is null)
            {
                logger.LogWarning("PDF rejected: payroll run {RunId} not found", payrollRunId);
                return Results.NotFound(new { error = $"Payroll run {payrollRunId} not found." });
            }

            var employee = await db.Employees.FindAsync(run.EmployeeId);
            if (employee is null)
            {
                logger.LogWarning("PDF rejected: employee {Id} missing for run {RunId}", run.EmployeeId, payrollRunId);
                return Results.NotFound(new { error = $"Employee {run.EmployeeId} not found." });
            }

            byte[] pdf = PayslipPdfGenerator.GeneratePdf(
                run, employee, run.Details.OrderBy(d => d.Id).ToList());

            string filename =
                $"Payslip_{employee.EmployeeCode}_{PayslipPdfGenerator.MonthName(run.MonthNumber)}_{run.Year}.pdf";
            logger.LogInformation("PDF payslip generated: runId={RunId} file={File}", run.Id, filename);

            return Results.File(pdf, "application/pdf", filename);
        })
        .WithName("PayrollSlipPdf");
    }

    /// <summary>Maps a stored employee onto the calculator input.</summary>
    internal static CalcEmployee ToCalculationInput(Employee employee, int month)
    {
        if (!PtCalculatorCore.TryParseState(employee.State, out PtState state))
        {
            throw new ArgumentException($"Employee {employee.EmployeeCode} has an unsupported state \"{employee.State}\".");
        }

        return new CalcEmployee(
            EmployeeId: employee.EmployeeCode,
            Name: employee.Name,
            State: state,
            IsMetro: employee.IsMetro,
            TaxRegime: string.Equals(employee.TaxRegime, "old", StringComparison.OrdinalIgnoreCase)
                ? TaxRegime.Old
                : TaxRegime.New,
            MonthlyBasic: employee.MonthlyBasic,
            MonthlyHra: employee.MonthlyHra,
            MonthlyDa: employee.MonthlyDa,
            MonthlySpecialAllowance: employee.MonthlySpecialAllowance,
            MonthlyLta: employee.MonthlyLta,
            MonthlyOtherAllowances: employee.MonthlyOtherAllowances,
            AnnualRentPaid: employee.AnnualRentPaid,
            Section80C: employee.Investment80C,
            Section80CCD1B: employee.Investment80CCD1B,
            Section80D: employee.Investment80D,
            Section80TTA: employee.Investment80TTA,
            HomeLoanInterest: employee.HomeLoanInterest,
            IsFirstYearEmployee: employee.IsFirstYearEmployee,
            HasDisability: employee.HasDisability,
            MonthNumber: month);
    }

    private static List<PayrollRunDetail> BuildDetails(PayrollSlip slip) =>
    [
        new() { ComponentName = "Basic Salary", ComponentType = ComponentTypes.Earning, Amount = slip.Basic },
        new() { ComponentName = "House Rent Allowance", ComponentType = ComponentTypes.Earning, Amount = slip.Hra },
        new() { ComponentName = "Dearness Allowance", ComponentType = ComponentTypes.Earning, Amount = slip.Da },
        new() { ComponentName = "Special Allowance", ComponentType = ComponentTypes.Earning, Amount = slip.SpecialAllowance },
        new() { ComponentName = "Leave Travel Allowance", ComponentType = ComponentTypes.Earning, Amount = slip.Lta },
        new() { ComponentName = "Other Allowances", ComponentType = ComponentTypes.Earning, Amount = slip.OtherAllowances },
        new() { ComponentName = "Employee PF", ComponentType = ComponentTypes.Deduction, Amount = slip.EmployeePf },
        new() { ComponentName = "Employee ESI", ComponentType = ComponentTypes.Deduction, Amount = slip.EmployeeEsi },
        new() { ComponentName = "Professional Tax", ComponentType = ComponentTypes.Deduction, Amount = slip.ProfessionalTax },
        new() { ComponentName = "TDS", ComponentType = ComponentTypes.Deduction, Amount = slip.Tds },
        new() { ComponentName = "Employer EPS", ComponentType = ComponentTypes.EmployerContribution, Amount = slip.EmployerEps },
        new() { ComponentName = "Employer EPF", ComponentType = ComponentTypes.EmployerContribution, Amount = slip.EmployerEpf },
        new() { ComponentName = "Employer ESI", ComponentType = ComponentTypes.EmployerContribution, Amount = slip.EmployerEsi },
        new() { ComponentName = "EDLI", ComponentType = ComponentTypes.EmployerContribution, Amount = slip.Edli },
        new() { ComponentName = "Admin Charges", ComponentType = ComponentTypes.EmployerContribution, Amount = slip.AdminCharges },
    ];

    private static object ToRunResponse(PayrollRun run) => new
    {
        payrollRunId = run.Id,
        run.EmployeeId,
        run.MonthNumber,
        run.Year,
        run.GrossSalary,
        run.TotalDeductions,
        run.NetPay,
        run.TotalEmployerCost,
        run.TotalCtc,
        run.GeneratedAt,
        details = run.Details
            .OrderBy(d => d.Id)
            .Select(d => new
            {
                d.Id,
                d.ComponentName,
                d.ComponentType,
                d.Amount,
            }),
    };
}
