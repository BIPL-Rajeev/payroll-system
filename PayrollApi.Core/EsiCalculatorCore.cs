namespace PayrollApi.Core;

/// <summary>Result of an ESI computation for one employee-month.</summary>
/// <param name="IsApplicable">False when gross salary exceeds the statutory ceiling.</param>
/// <param name="EmployeeContribution">Rounded to the nearest rupee (0 when exempt).</param>
/// <param name="EmployerContribution">Rounded to the nearest rupee.</param>
/// <param name="TotalEsi">Sum of the two rounded contributions.</param>
/// <param name="EmployeeRate">Rate applied to the employee share (0 for exempt employees).</param>
/// <param name="EmployerRate">Employer share rate (always 3.25%).</param>
public sealed record EsiResult(
    bool IsApplicable,
    decimal MonthlyGrossSalary,
    bool IsDisabled,
    bool IsFirstYearExempt,
    decimal EmployeeContribution,
    decimal EmployerContribution,
    decimal TotalEsi,
    decimal EmployeeRate,
    decimal EmployerRate)
{
    /// <summary>Statutory wage ceiling for ESI applicability (monthly gross).</summary>
    public const decimal WageCeiling = 21_000m;

    /// <summary>Creates a not-applicable result for salaries above the ceiling.</summary>
    public static EsiResult NotApplicable(decimal monthlyGrossSalary, bool isDisabled, bool isFirstYearExempt) =>
        new(false, monthlyGrossSalary, isDisabled, isFirstYearExempt, 0m, 0m, 0m, 0m, 0m);
}

/// <summary>
/// Indian ESI (Employee State Insurance) computation for FY 2025-26.
/// Applicable only when monthly gross salary is at most Rs 21,000.
/// </summary>
public static class EsiCalculatorCore
{
    public const decimal EmployeeRate = 0.0075m;  // 0.75%
    public const decimal EmployerRate = 0.0325m;  // 3.25%

    /// <summary>
    /// Computes ESI for one employee-month.
    /// </summary>
    /// <param name="monthlyGrossSalary">Monthly gross salary including all allowances, excluding annual bonus.</param>
    /// <param name="isDisabled">
    /// Person with disability: employer still contributes 3.25%, employee share is 0%.
    /// </param>
    /// <param name="isFirstYearExempt">
    /// First-time employee (newly joined): 2-year exemption from the employee contribution.
    /// </param>
    public static EsiResult Calculate(
        decimal monthlyGrossSalary,
        bool isDisabled = false,
        bool isFirstYearExempt = false)
    {
        if (monthlyGrossSalary < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyGrossSalary), "Gross salary cannot be negative.");
        }

        // Rule 1/2: not applicable above the ceiling.
        if (monthlyGrossSalary > EsiResult.WageCeiling)
        {
            return EsiResult.NotApplicable(monthlyGrossSalary, isDisabled, isFirstYearExempt);
        }

        // Rule 8: first-time employees are exempt from the employee share for 2 years.
        bool employeeShareWaived = isDisabled || isFirstYearExempt;
        decimal employeeRate = employeeShareWaived ? 0m : EmployeeRate;

        decimal employeeContribution = RoundToRupee(monthlyGrossSalary * employeeRate);
        decimal employerContribution = RoundToRupee(monthlyGrossSalary * EmployerRate);
        decimal totalEsi = employeeContribution + employerContribution;

        return new EsiResult(
            IsApplicable: true,
            MonthlyGrossSalary: monthlyGrossSalary,
            IsDisabled: isDisabled,
            IsFirstYearExempt: isFirstYearExempt,
            EmployeeContribution: employeeContribution,
            EmployerContribution: employerContribution,
            TotalEsi: totalEsi,
            EmployeeRate: employeeRate,
            EmployerRate: EmployerRate);
    }

    private static decimal RoundToRupee(decimal value) =>
        Math.Round(value, 0, MidpointRounding.AwayFromZero);
}
