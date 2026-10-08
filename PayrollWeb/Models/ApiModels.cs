using System.ComponentModel.DataAnnotations;

namespace PayrollWeb.Models;

/// <summary>Employee as returned by the API.</summary>
public sealed record ApiEmployee
{
    public int Id { get; init; }
    public string EmployeeCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Pan { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public bool IsMetro { get; init; }
    public string TaxRegime { get; init; } = "new";
    public decimal MonthlyBasic { get; init; }
    public decimal MonthlyHra { get; init; }
    public decimal MonthlyDa { get; init; }
    public decimal MonthlySpecialAllowance { get; init; }
    public decimal MonthlyLta { get; init; }
    public decimal MonthlyOtherAllowances { get; init; }
    public decimal AnnualRentPaid { get; init; }
    public decimal Investment80C { get; init; }
    public decimal Investment80CCD1B { get; init; }
    public decimal Investment80D { get; init; }
    public decimal Investment80TTA { get; init; }
    public decimal HomeLoanInterest { get; init; }
    public bool IsFirstYearEmployee { get; init; }
    public bool HasDisability { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Form model for add/edit employee with client-side validation.
/// Serialized to the API's camelCase EmployeeRequest JSON.
/// </summary>
public sealed class EmployeeFormModel
{
    [Required(ErrorMessage = "Employee code is required.")]
    [StringLength(64)]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Name is required.")]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>PAN (optional; 10 characters when provided).</summary>
    [StringLength(10, MinimumLength = 0, ErrorMessage = "PAN must be 10 characters.")]
    public string Pan { get; set; } = string.Empty;

    [Required(ErrorMessage = "State is required.")]
    public string State { get; set; } = string.Empty;

    public bool IsMetro { get; set; }

    [Required]
    public string TaxRegime { get; set; } = "new";

    [Range(0, 100_000_000, ErrorMessage = "Amount must be non-negative.")]
    public decimal MonthlyBasic { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Amount must be non-negative.")]
    public decimal MonthlyHra { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Amount must be non-negative.")]
    public decimal MonthlyDa { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Amount must be non-negative.")]
    public decimal MonthlySpecialAllowance { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Amount must be non-negative.")]
    public decimal MonthlyLta { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Amount must be non-negative.")]
    public decimal MonthlyOtherAllowances { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Amount must be non-negative.")]
    public decimal AnnualRentPaid { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Amount must be non-negative.")]
    public decimal Investment80C { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Amount must be non-negative.")]
    public decimal Investment80CCD1B { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Amount must be non-negative.")]
    public decimal Investment80D { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Amount must be non-negative.")]
    public decimal Investment80TTA { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Amount must be non-negative.")]
    public decimal HomeLoanInterest { get; set; }

    public bool IsFirstYearEmployee { get; set; }
    public bool HasDisability { get; set; }

    public static EmployeeFormModel FromApi(ApiEmployee e) => new()
    {
        EmployeeCode = e.EmployeeCode,
        Name = e.Name,
        Pan = e.Pan,
        State = e.State,
        IsMetro = e.IsMetro,
        TaxRegime = e.TaxRegime,
        MonthlyBasic = e.MonthlyBasic,
        MonthlyHra = e.MonthlyHra,
        MonthlyDa = e.MonthlyDa,
        MonthlySpecialAllowance = e.MonthlySpecialAllowance,
        MonthlyLta = e.MonthlyLta,
        MonthlyOtherAllowances = e.MonthlyOtherAllowances,
        AnnualRentPaid = e.AnnualRentPaid,
        Investment80C = e.Investment80C,
        Investment80CCD1B = e.Investment80CCD1B,
        Investment80D = e.Investment80D,
        Investment80TTA = e.Investment80TTA,
        HomeLoanInterest = e.HomeLoanInterest,
        IsFirstYearEmployee = e.IsFirstYearEmployee,
        HasDisability = e.HasDisability,
    };
}

/// <summary>One payroll run summary (history row).</summary>
public sealed record PayrollRunSummary
{
    public int PayrollRunId { get; init; }
    public int EmployeeId { get; init; }
    public int MonthNumber { get; init; }
    public int Year { get; init; }
    public decimal GrossSalary { get; init; }
    public decimal TotalDeductions { get; init; }
    public decimal NetPay { get; init; }
    public decimal TotalEmployerCost { get; init; }
    public decimal TotalCtc { get; init; }
    public DateTime GeneratedAt { get; init; }
}

/// <summary>Component line of a payroll run.</summary>
public sealed record PayrollRunDetailLine
{
    public int Id { get; init; }
    public string ComponentName { get; init; } = string.Empty;
    public string ComponentType { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

/// <summary>Full payroll run with details (run result / slip view).</summary>
public sealed record PayrollRunResult
{
    public int PayrollRunId { get; init; }
    public int EmployeeId { get; init; }
    public int MonthNumber { get; init; }
    public int Year { get; init; }
    public decimal GrossSalary { get; init; }
    public decimal TotalDeductions { get; init; }
    public decimal NetPay { get; init; }
    public decimal TotalEmployerCost { get; init; }
    public decimal TotalCtc { get; init; }
    public DateTime GeneratedAt { get; init; }
    public List<PayrollRunDetailLine> Details { get; init; } = [];
}

/// <summary>Month number helpers for dropdowns and labels.</summary>
public static class MonthNames
{
    public static readonly string[] All =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    ];

    public static string Get(int month) =>
        month is >= 1 and <= 12 ? All[month - 1] : $"Month {month}";
}

/// <summary>Indian-format currency helpers: Rs 1,23,456.00.</summary>
public static class Inr
{
    private static readonly System.Globalization.CultureInfo Culture =
        System.Globalization.CultureInfo.GetCultureInfo("en-IN");

    public static string Format(decimal value) => $"Rs {value.ToString("N2", Culture)}";

    public static string Format(DateTime value) =>
        value.ToLocalTime().ToString("dd MMM yyyy HH:mm", Culture);
}
