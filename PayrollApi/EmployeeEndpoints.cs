using Microsoft.EntityFrameworkCore;
using PayrollApi.Core;
using PayrollApi.Core.Entities;
using PayrollApi.Core.Persistence;
using Employee = PayrollApi.Core.Entities.Employee;

namespace PayrollApi;

/// <summary>CRUD endpoints for the employee master (soft delete via IsActive).</summary>
public static class EmployeeEndpoints
{
    public static void MapEmployeeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/employees");

        // POST /api/employees - create employee (400 on duplicate code / invalid input).
        group.MapPost("/", async (EmployeeRequest request, AppDbContext db, ILogger<Program> logger) =>
        {
            string? error = Validate(request);
            if (error is not null)
            {
                logger.LogWarning("Employee create rejected: {Error}", error);
                return Results.BadRequest(new { error });
            }

            string code = request.EmployeeCode!.Trim();
            if (await db.AnyAsync(code))
            {
                logger.LogWarning("Employee create rejected: duplicate code {Code}", code);
                return Results.BadRequest(new { error = $"Employee code \"{code}\" already exists." });
            }

            Employee employee = FromRequest(request);
            db.Employees.Add(employee);
            await db.SaveChangesAsync();
            logger.LogInformation("Employee created: id={Id} code={Code}", employee.Id, employee.EmployeeCode);

            return Results.Created($"/api/employees/{employee.Id}", employee);
        })
        .WithName("CreateEmployee");

        // GET /api/employees - list all active employees.
        group.MapGet("/", async (AppDbContext db) =>
        {
            var employees = await db.Employees
                .Where(e => e.IsActive)
                .OrderBy(e => e.EmployeeCode)
                .ToListAsync();
            return Results.Ok(employees);
        })
        .WithName("ListEmployees");

        // GET /api/employees/{id} - get one (404 if missing or soft-deleted).
        group.MapGet("/{id:int}", async (int id, AppDbContext db) =>
        {
            var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.IsActive);
            return employee is null
                ? Results.NotFound(new { error = $"Employee {id} not found." })
                : Results.Ok(employee);
        })
        .WithName("GetEmployee");

        // PUT /api/employees/{id} - update.
        group.MapPut("/{id:int}", async (int id, EmployeeRequest request, AppDbContext db, ILogger<Program> logger) =>
        {
            var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.IsActive);
            if (employee is null)
            {
                return Results.NotFound(new { error = $"Employee {id} not found." });
            }

            string? error = Validate(request);
            if (error is not null)
            {
                logger.LogWarning("Employee {Id} update rejected: {Error}", id, error);
                return Results.BadRequest(new { error });
            }

            string code = request.EmployeeCode!.Trim();
            if (await db.Employees.AnyAsync(e => e.EmployeeCode == code && e.Id != id))
            {
                logger.LogWarning("Employee {Id} update rejected: duplicate code {Code}", id, code);
                return Results.BadRequest(new { error = $"Employee code \"{code}\" already exists." });
            }

            ApplyRequest(employee, request);
            employee.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            logger.LogInformation("Employee updated: id={Id} code={Code}", id, employee.EmployeeCode);

            return Results.Ok(employee);
        })
        .WithName("UpdateEmployee");

        // DELETE /api/employees/{id} - soft delete (IsActive = false).
        group.MapDelete("/{id:int}", async (int id, AppDbContext db, ILogger<Program> logger) =>
        {
            var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == id && e.IsActive);
            if (employee is null)
            {
                return Results.NotFound(new { error = $"Employee {id} not found." });
            }

            employee.IsActive = false;
            employee.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            logger.LogInformation("Employee soft-deleted: id={Id} code={Code}", id, employee.EmployeeCode);

            return Results.NoContent();
        })
        .WithName("DeleteEmployee");
    }

    /// <summary>Shared validation: returns an error message or null when valid.</summary>
    internal static string? Validate(EmployeeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeCode))
        {
            return "Employee code is required.";
        }
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Name is required.";
        }
        if (!PtCalculatorCore.TryParseState(request.State ?? string.Empty, out _))
        {
            return $"Unsupported state \"{request.State}\". Supported states: {PtCalculatorCore.SupportedStates}.";
        }

        string regime = request.TaxRegime?.Trim().ToLowerInvariant() ?? string.Empty;
        if (regime is not ("old" or "new"))
        {
            return $"Tax regime must be \"old\" or \"new\" (got \"{request.TaxRegime}\").";
        }

        (string Name, decimal Value)[] fields =
        [
            (nameof(request.MonthlyBasic), request.MonthlyBasic),
            (nameof(request.MonthlyHra), request.MonthlyHra),
            (nameof(request.MonthlyDa), request.MonthlyDa),
            (nameof(request.MonthlySpecialAllowance), request.MonthlySpecialAllowance),
            (nameof(request.MonthlyLta), request.MonthlyLta),
            (nameof(request.MonthlyOtherAllowances), request.MonthlyOtherAllowances),
            (nameof(request.AnnualRentPaid), request.AnnualRentPaid),
            (nameof(request.Investment80C), request.Investment80C),
            (nameof(request.Investment80CCD1B), request.Investment80CCD1B),
            (nameof(request.Investment80D), request.Investment80D),
            (nameof(request.Investment80TTA), request.Investment80TTA),
            (nameof(request.HomeLoanInterest), request.HomeLoanInterest),
        ];

        foreach ((string name, decimal value) in fields)
        {
            if (value < 0)
            {
                return $"Field \"{name}\" must be non-negative.";
            }
        }

        return null;
    }

    private static Employee FromRequest(EmployeeRequest request) => new()
    {
        EmployeeCode = request.EmployeeCode!.Trim(),
        Name = request.Name!.Trim(),
        Pan = request.Pan?.Trim() ?? string.Empty,
        State = request.State!.Trim(),
        IsMetro = request.IsMetro,
        TaxRegime = request.TaxRegime!.Trim().ToLowerInvariant(),
        MonthlyBasic = request.MonthlyBasic,
        MonthlyHra = request.MonthlyHra,
        MonthlyDa = request.MonthlyDa,
        MonthlySpecialAllowance = request.MonthlySpecialAllowance,
        MonthlyLta = request.MonthlyLta,
        MonthlyOtherAllowances = request.MonthlyOtherAllowances,
        AnnualRentPaid = request.AnnualRentPaid,
        Investment80C = request.Investment80C,
        Investment80CCD1B = request.Investment80CCD1B,
        Investment80D = request.Investment80D,
        Investment80TTA = request.Investment80TTA,
        HomeLoanInterest = request.HomeLoanInterest,
        IsFirstYearEmployee = request.IsFirstYearEmployee,
        HasDisability = request.HasDisability,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static void ApplyRequest(Employee employee, EmployeeRequest request)
    {
        employee.EmployeeCode = request.EmployeeCode!.Trim();
        employee.Name = request.Name!.Trim();
        employee.Pan = request.Pan?.Trim() ?? string.Empty;
        employee.State = request.State!.Trim();
        employee.IsMetro = request.IsMetro;
        employee.TaxRegime = request.TaxRegime!.Trim().ToLowerInvariant();
        employee.MonthlyBasic = request.MonthlyBasic;
        employee.MonthlyHra = request.MonthlyHra;
        employee.MonthlyDa = request.MonthlyDa;
        employee.MonthlySpecialAllowance = request.MonthlySpecialAllowance;
        employee.MonthlyLta = request.MonthlyLta;
        employee.MonthlyOtherAllowances = request.MonthlyOtherAllowances;
        employee.AnnualRentPaid = request.AnnualRentPaid;
        employee.Investment80C = request.Investment80C;
        employee.Investment80CCD1B = request.Investment80CCD1B;
        employee.Investment80D = request.Investment80D;
        employee.Investment80TTA = request.Investment80TTA;
        employee.HomeLoanInterest = request.HomeLoanInterest;
        employee.IsFirstYearEmployee = request.IsFirstYearEmployee;
        employee.HasDisability = request.HasDisability;
    }
}

/// <summary>Small helpers shared by the endpoints.</summary>
internal static class DbExtensions
{
    /// <summary>Async check for a duplicate employee code.</summary>
    public static Task<bool> AnyAsync(this AppDbContext db, string employeeCode) =>
        db.Employees.AnyAsync(e => e.EmployeeCode == employeeCode);
}
