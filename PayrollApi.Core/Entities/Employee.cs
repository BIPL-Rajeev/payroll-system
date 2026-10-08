namespace PayrollApi.Core.Entities;

/// <summary>
/// Persistent employee master record (soft-deleted via <see cref="IsActive"/>).
/// Carries the same payroll inputs as the calculation model, stored per employee.
/// </summary>
public class Employee
{
    public int Id { get; set; }

    /// <summary>Business code, unique across the table.</summary>
    public string EmployeeCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>PAN (e.g. ABCDE1234F); shown on Form 16 Part B.</summary>
    public string Pan { get; set; } = string.Empty;

    /// <summary>State name: Maharashtra / Karnataka / Tamil Nadu.</summary>
    public string State { get; set; } = string.Empty;

    public bool IsMetro { get; set; }

    /// <summary>"old" or "new" tax regime.</summary>
    public string TaxRegime { get; set; } = "new";

    public decimal MonthlyBasic { get; set; }
    public decimal MonthlyHra { get; set; }
    public decimal MonthlyDa { get; set; }
    public decimal MonthlySpecialAllowance { get; set; }
    public decimal MonthlyLta { get; set; }
    public decimal MonthlyOtherAllowances { get; set; }
    public decimal AnnualRentPaid { get; set; }
    public decimal Investment80C { get; set; }
    public decimal Investment80CCD1B { get; set; }
    public decimal Investment80D { get; set; }
    public decimal Investment80TTA { get; set; }
    public decimal HomeLoanInterest { get; set; }

    public bool IsFirstYearEmployee { get; set; }
    public bool HasDisability { get; set; }

    /// <summary>Soft-delete flag: false once deleted.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<PayrollRun> PayrollRuns { get; set; } = new List<PayrollRun>();
}
