namespace PayrollApi.Core.Entities;

/// <summary>
/// Component type values for <see cref="PayrollRunDetail.ComponentType"/>.
/// </summary>
public static class ComponentTypes
{
    public const string Earning = "Earning";
    public const string Deduction = "Deduction";
    public const string EmployerContribution = "EmployerContribution";
}

/// <summary>One line item of a payroll run (earnings, deductions or employer contributions).</summary>
public class PayrollRunDetail
{
    public int Id { get; set; }

    public int PayrollRunId { get; set; }

    /// <summary>Display name, e.g. "Basic Salary", "Employee PF (12%)", "Professional Tax".</summary>
    public string ComponentName { get; set; } = string.Empty;

    /// <summary><see cref="ComponentTypes.Earning"/>, <see cref="ComponentTypes.Deduction"/> or <see cref="ComponentTypes.EmployerContribution"/>.</summary>
    public string ComponentType { get; set; } = ComponentTypes.Earning;

    public decimal Amount { get; set; }

    public PayrollRun? PayrollRun { get; set; }
}
