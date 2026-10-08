namespace PayrollApi;

/// <summary>
/// JSON body accepted by POST /api/payroll/calculate.
/// Same fields as PayrollCalculator's Employee, but State and TaxRegime are
/// strings so the API can reject invalid values with a 400 + message.
/// </summary>
public sealed record EmployeeInput(
    string? EmployeeId,
    string? Name,
    string? State,
    bool IsMetro,
    string? TaxRegime,
    decimal MonthlyBasic,
    decimal MonthlyHra,
    decimal MonthlyDa,
    decimal MonthlySpecialAllowance,
    decimal MonthlyLta,
    decimal MonthlyOtherAllowances,
    decimal AnnualRentPaid,
    decimal Section80C,
    decimal Section80CCD1B,
    decimal Section80D,
    decimal Section80TTA,
    decimal HomeLoanInterest,
    bool IsFirstYearEmployee,
    bool HasDisability,
    int MonthNumber);
