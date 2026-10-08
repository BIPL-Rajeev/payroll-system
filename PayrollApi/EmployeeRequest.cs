namespace PayrollApi;

/// <summary>JSON body for POST/PUT /api/employees.</summary>
public sealed record EmployeeRequest(
    string? EmployeeCode,
    string? Name,
    string? Pan,
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
    decimal Investment80C,
    decimal Investment80CCD1B,
    decimal Investment80D,
    decimal Investment80TTA,
    decimal HomeLoanInterest,
    bool IsFirstYearEmployee,
    bool HasDisability);
