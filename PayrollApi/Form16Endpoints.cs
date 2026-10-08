using Microsoft.EntityFrameworkCore;
using PayrollApi.Core.Persistence;
using PayrollApi.Core.Services;

namespace PayrollApi;

/// <summary>Form 16 (Part B) endpoints.</summary>
public static class Form16Endpoints
{
    public static void MapForm16Endpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/form16/{employeeId}/{financialYear} - Form 16 Part B as JSON.
        app.MapGet("/api/form16/{employeeId:int}/{financialYear}", async (
            int employeeId, string financialYear, AppDbContext db, ILogger<Program> logger) =>
        {
            if (!Form16Generator.IsValidFinancialYear(financialYear))
            {
                logger.LogWarning("Form16 rejected: invalid financial year {FY}", financialYear);
                return Results.BadRequest(new
                {
                    error = $"Invalid financial year \"{financialYear}\". Expected format: 2025-26.",
                });
            }

            var employee = await db.Employees
                .FirstOrDefaultAsync(e => e.Id == employeeId && e.IsActive);
            if (employee is null)
            {
                logger.LogWarning("Form16 rejected: employee {Id} not found", employeeId);
                return Results.NotFound(new { error = $"Employee {employeeId} not found." });
            }

            var runs = await db.PayrollRuns
                .Include(r => r.Details)
                .Where(r => r.EmployeeId == employeeId)
                .ToListAsync();

            if (!Form16Generator.RunsForFinancialYear(runs, financialYear).Any())
            {
                logger.LogWarning(
                    "Form16 rejected: no runs for employee {Id} in FY {FY}", employeeId, financialYear);
                return Results.BadRequest(new
                {
                    error = $"No payroll runs found for employee {employeeId} for FY {financialYear}.",
                });
            }

            Form16PartB form = Form16Generator.Generate(employee, runs, financialYear);
            logger.LogInformation(
                "Form16 generated: employeeId={Id} fy={FY} tax={Tax} tds={Tds}",
                employeeId, financialYear, form.Tax.TotalTaxPayable, form.Tds.TotalTdsDeducted);
            return Results.Ok(form);
        })
        .WithName("Form16");

        // GET /api/form16/{employeeId}/{financialYear}/pdf - downloadable PDF.
        app.MapGet("/api/form16/{employeeId:int}/{financialYear}/pdf", async (
            int employeeId, string financialYear, AppDbContext db, ILogger<Program> logger) =>
        {
            if (!Form16Generator.IsValidFinancialYear(financialYear))
            {
                logger.LogWarning("Form16 PDF rejected: invalid financial year {FY}", financialYear);
                return Results.BadRequest(new
                {
                    error = $"Invalid financial year \"{financialYear}\". Expected format: 2025-26.",
                });
            }

            var employee = await db.Employees
                .FirstOrDefaultAsync(e => e.Id == employeeId && e.IsActive);
            if (employee is null)
            {
                logger.LogWarning("Form16 PDF rejected: employee {Id} not found", employeeId);
                return Results.NotFound(new { error = $"Employee {employeeId} not found." });
            }

            var runs = await db.PayrollRuns
                .Include(r => r.Details)
                .Where(r => r.EmployeeId == employeeId)
                .ToListAsync();

            if (!Form16Generator.RunsForFinancialYear(runs, financialYear).Any())
            {
                logger.LogWarning(
                    "Form16 PDF rejected: no runs for employee {Id} in FY {FY}", employeeId, financialYear);
                return Results.BadRequest(new
                {
                    error = $"No payroll runs found for employee {employeeId} for FY {financialYear}.",
                });
            }

            Form16PartB form = Form16Generator.Generate(employee, runs, financialYear);
            byte[] pdf = Form16Generator.GeneratePdf(form);

            string filename = $"Form16PartB_{employee.EmployeeCode}_{financialYear}.pdf";
            logger.LogInformation("Form16 PDF generated: employeeId={Id} file={File}", employeeId, filename);
            return Results.File(pdf, "application/pdf", filename);
        })
        .WithName("Form16Pdf");
    }
}
