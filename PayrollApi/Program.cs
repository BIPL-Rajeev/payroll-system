using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PayrollApi;
using PayrollApi.Core;
using PayrollApi.Core.Persistence;
using PayrollApi.Services;
using QuestPDF.Infrastructure;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.OpenApi;

// QuestPDF Community license (required before generating any document).
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// System.Text.Json with camelCase (and enums as readable strings).
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// OpenAPI document + Swagger UI.
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Payroll API",
        Version = "v1",
        Description = "Indian payroll calculator API (FY 2025-26): PF + ESI + Professional Tax + TDS.",
    });

    // Bearer token support in Swagger UI ("Authorize" button).
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the JWT token from POST /api/auth/login (no \"Bearer \" prefix needed).",
    });
    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer"),
            new List<string>()
        },
    });
});

// CORS allowing all origins - for development only.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllDevelopment", policy => policy
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
});

// JWT bearer authentication + role authorization policies:
//   Writer     -> Admin or HR    (POST/PUT/DELETE)
//   AdminOnly  -> Admin          (register)
//   (no policy)-> any authenticated user (GET)
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "PayrollApi",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "PayrollWeb",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:SecretKey"]
                    ?? "PayrollApi-super-secret-key-change-me-0123456789-abcdefghijklmno")),
            ValidateLifetime = true,
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(UserRoles.WritePolicy, policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole(UserRoles.Admin, UserRoles.HR));
    options.AddPolicy(UserRoles.AdminOnlyPolicy, policy =>
        policy.RequireAuthenticatedUser()
              .RequireRole(UserRoles.Admin));
});

builder.Services.AddSingleton<TokenService>();

// SQLite persistence (connection string in appsettings.json).
string connectionString = builder.Configuration.GetConnectionString("PayrollDb")
    ?? "Data Source=payroll.db";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

var app = builder.Build();

// Apply EF Core migrations at startup (creates payroll.db on first run) and
// seed the default admin user when no users exist.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Users.Any())
    {
        db.Users.Add(new PayrollApi.Core.Entities.User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = UserRoles.Admin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        scope.ServiceProvider.GetRequiredService<ILogger<Program>>()
            .LogInformation("Seeded default admin user (username: admin). Change the password on first login.");
    }
}

// Log every request with a simple ILogger.
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("HTTP {Method} {Path}", context.Request.Method, context.Request.Path);
    await next();
});

// Swagger UI at /swagger, OpenAPI document at /swagger/v1/swagger.json.
app.UseSwagger();
app.UseSwaggerUI(options =>
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Payroll API v1"));

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("AllowAllDevelopment");
}

// ------------------------------ Endpoints ------------------------------

// Health + stateless calculation: any authenticated user (GET / POST-calc read-only).
app.MapGet("/api/payroll/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health")
    .RequireAuthorization();

app.MapPost("/api/payroll/calculate", (EmployeeInput? input, ILogger<Program> logger) =>
{
    if (input is null)
    {
        return Results.BadRequest(new { error = "Request body is required." });
    }

    logger.LogInformation(
        "Calculate requested: employeeId={EmployeeId} state={State} regime={Regime} month={Month}",
        input.EmployeeId, input.State, input.TaxRegime, input.MonthNumber);

    // Validation: invalid state -> 400.
    if (!PtCalculatorCore.TryParseState(input.State ?? string.Empty, out PtState state))
    {
        logger.LogWarning("Rejected: unsupported state {State}", input.State);
        return Results.BadRequest(new
        {
            error = $"Unsupported state \"{input.State}\". Supported states: {PtCalculatorCore.SupportedStates}.",
        });
    }

    // Validation: month outside 1-12 -> 400.
    if (input.MonthNumber < 1 || input.MonthNumber > PtCalculatorCore.MonthsInYear)
    {
        logger.LogWarning("Rejected: month {Month} out of range", input.MonthNumber);
        return Results.BadRequest(new
        {
            error = $"Month must be between 1 and 12 (got {input.MonthNumber}).",
        });
    }

    // Validation: any negative amount -> 400.
    string? negativeField = FindNegativeField(input);
    if (negativeField is not null)
    {
        logger.LogWarning("Rejected: negative field {Field}", negativeField);
        return Results.BadRequest(new
        {
            error = $"Field \"{negativeField}\" must be non-negative.",
        });
    }

    // Validation: tax regime must be old or new -> 400.
    if (!TryParseRegime(input.TaxRegime, out TaxRegime regime))
    {
        logger.LogWarning("Rejected: invalid tax regime {Regime}", input.TaxRegime);
        return Results.BadRequest(new
        {
            error = $"Tax regime must be \"old\" or \"new\" (got \"{input.TaxRegime}\").",
        });
    }

    Employee employee = new(
        EmployeeId: input.EmployeeId ?? string.Empty,
        Name: input.Name ?? string.Empty,
        State: state,
        IsMetro: input.IsMetro,
        TaxRegime: regime,
        MonthlyBasic: input.MonthlyBasic,
        MonthlyHra: input.MonthlyHra,
        MonthlyDa: input.MonthlyDa,
        MonthlySpecialAllowance: input.MonthlySpecialAllowance,
        MonthlyLta: input.MonthlyLta,
        MonthlyOtherAllowances: input.MonthlyOtherAllowances,
        AnnualRentPaid: input.AnnualRentPaid,
        Section80C: input.Section80C,
        Section80CCD1B: input.Section80CCD1B,
        Section80D: input.Section80D,
        Section80TTA: input.Section80TTA,
        HomeLoanInterest: input.HomeLoanInterest,
        IsFirstYearEmployee: input.IsFirstYearEmployee,
        HasDisability: input.HasDisability,
        MonthNumber: input.MonthNumber);

    try
    {
        PayrollSlip slip = PayrollCalculatorCore.Calculate(employee);
        logger.LogInformation(
            "Slip computed: employeeId={EmployeeId} gross={Gross} deductions={Deductions} netPay={Net} ctc={Ctc}",
            slip.EmployeeId, slip.GrossMonthlySalary, slip.TotalDeductions, slip.NetMonthlyTakeHome, slip.TotalCtc);
        return Results.Ok(slip);
    }
    catch (ArgumentException ex)
    {
        logger.LogWarning("Rejected: {Error}", ex.Message);
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("CalculatePayroll")
.WithSummary("Computes a complete monthly salary slip (PF + ESI + PT + TDS).")
.RequireAuthorization();

// Persistence endpoints (employee CRUD, payroll runs, history, slips).
app.MapEmployeeEndpoints();
app.MapPayrollRunEndpoints();
app.MapForm16Endpoints();
app.MapAuthEndpoints();
app.MapExportEndpoints();

app.Run();

// ------------------------------ Helpers ------------------------------

static bool TryParseRegime(string? text, out TaxRegime regime)
{
    switch (text?.Trim().ToLowerInvariant())
    {
        case "old":
            regime = TaxRegime.Old;
            return true;
        case "new":
            regime = TaxRegime.New;
            return true;
        default:
            regime = TaxRegime.New;
            return false;
    }
}

static string? FindNegativeField(EmployeeInput input)
{
    (string Name, decimal Value)[] fields =
    [
        (nameof(input.MonthlyBasic), input.MonthlyBasic),
        (nameof(input.MonthlyHra), input.MonthlyHra),
        (nameof(input.MonthlyDa), input.MonthlyDa),
        (nameof(input.MonthlySpecialAllowance), input.MonthlySpecialAllowance),
        (nameof(input.MonthlyLta), input.MonthlyLta),
        (nameof(input.MonthlyOtherAllowances), input.MonthlyOtherAllowances),
        (nameof(input.AnnualRentPaid), input.AnnualRentPaid),
        (nameof(input.Section80C), input.Section80C),
        (nameof(input.Section80CCD1B), input.Section80CCD1B),
        (nameof(input.Section80D), input.Section80D),
        (nameof(input.Section80TTA), input.Section80TTA),
        (nameof(input.HomeLoanInterest), input.HomeLoanInterest),
    ];

    foreach ((string name, decimal value) in fields)
    {
        if (value < 0)
        {
            return name;
        }
    }

    return null;
}

// Exposes the entry point to WebApplicationFactory<Program> in PayrollApi.Tests.
public partial class Program;
