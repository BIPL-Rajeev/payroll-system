
namespace PayrollApi.Core;

/// <summary>Tax regime selected for the employee.</summary>
public enum TaxRegime
{
    Old,
    New,
}

/// <summary>All inputs needed to produce one monthly salary slip.</summary>
/// <param name="EmployeeId">Employee identifier shown on the slip.</param>
/// <param name="Name">Employee name shown on the slip.</param>
/// <param name="State">State for Professional Tax (Maharashtra / Karnataka / Tamil Nadu).</param>
/// <param name="IsMetro">Metro city flag (affects old-regime HRA exemption: 50% vs 40%).</param>
/// <param name="TaxRegime">Old or New tax regime for TDS.</param>
/// <param name="MonthlyBasic">Monthly basic salary (also the PF wage base together with DA).</param>
/// <param name="MonthlyHra">Monthly house rent allowance.</param>
/// <param name="MonthlyDa">Monthly dearness allowance (counts toward PF wages).</param>
/// <param name="MonthlySpecialAllowance">Monthly special allowance.</param>
/// <param name="MonthlyLta">Monthly leave travel allowance.</param>
/// <param name="MonthlyOtherAllowances">Monthly other allowances.</param>
/// <param name="AnnualRentPaid">Annual rent paid (old-regime HRA exemption).</param>
/// <param name="Section80C">Annual 80C investments (old regime only, capped at 1,50,000).</param>
/// <param name="Section80CCD1B">Annual 80CCD(1B) NPS (old regime only, capped at 50,000).</param>
/// <param name="Section80D">Annual 80D medical insurance (old regime only, capped at 25,000).</param>
/// <param name="Section80TTA">Annual 80TTA savings interest (old regime only, capped at 10,000).</param>
/// <param name="HomeLoanInterest">Annual home loan interest u/s 24(b) (old regime only, capped at 2,00,000).</param>
/// <param name="IsFirstYearEmployee">First-time employee: ESI employee share waived for 2 years.</param>
/// <param name="HasDisability">Person with disability: ESI employee share waived.</param>
/// <param name="MonthNumber">Slip month 1-12 (February triggers Maharashtra's Rs 300 PT slab).</param>
public sealed record Employee(
    string EmployeeId,
    string Name,
    PtState State,
    bool IsMetro,
    TaxRegime TaxRegime,
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

/// <summary>A complete monthly salary slip combining PF, ESI, PT and TDS.</summary>
public sealed record PayrollSlip(
    string EmployeeId,
    string Name,
    string StateName,
    TaxRegime TaxRegime,
    int MonthNumber,
    string MonthName,
    // Earnings.
    decimal Basic,
    decimal Hra,
    decimal Da,
    decimal SpecialAllowance,
    decimal Lta,
    decimal OtherAllowances,
    decimal GrossMonthlySalary,
    // Deductions.
    decimal EmployeePf,
    decimal EmployeeEsi,
    decimal ProfessionalTax,
    decimal Tds,
    decimal TotalDeductions,
    // Employer contributions (cost to company, not deducted).
    decimal EmployerEps,
    decimal EmployerEpf,
    decimal EmployerEsi,
    decimal Edli,
    decimal AdminCharges,
    decimal TotalEmployerCost,
    // Net.
    decimal NetMonthlyTakeHome,
    decimal TotalCtc,
    // Context.
    bool EsiApplicable,
    decimal PfContributionBase,
    bool PfOnCeiling,
    decimal HraExemptionAnnual,
    bool IsFebruaryHigherPtSlab);

/// <summary>
/// Produces a complete monthly salary slip by combining the four calculators:
/// PF (12% employee; employer EPS 8.33% + EPF 3.67% + EDLI 0.5% + admin 0.5% on a
/// 15,000 ceiling, mirroring PfCalculator), ESI (employee 0.75% / employer 3.25% on
/// gross up to 21,000), Professional Tax (state slabs, February-aware) and TDS
/// (annual projected income taxed under the chosen regime, divided by 12).
/// </summary>
public static class PayrollCalculatorCore
{
    // PF rates, mirroring PfCalculator (which has no reusable class - inline in its Program.cs).
    public const decimal EmployeePfRate = 0.12m;    // employee 12% -> EPF
    public const decimal EmployerEpsRate = 0.0833m; // employer 8.33% -> EPS
    public const decimal EmployerEpfRate = 0.0367m; // employer 3.67% -> EPF
    public const decimal EdliRate = 0.005m;         // employer-only EDLI 0.5%
    public const decimal AdminRate = 0.005m;        // employer-only admin charges 0.5%
    public const decimal PfWageCeiling = 15_000m;   // statutory wage ceiling

    /// <summary>Computes the complete salary slip for one employee-month.</summary>
    public static PayrollSlip Calculate(Employee e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (string.IsNullOrWhiteSpace(e.EmployeeId))
        {
            throw new ArgumentException("Employee ID is required.", nameof(e));
        }
        if (e.MonthNumber < 1 || e.MonthNumber > PtCalculatorCore.MonthsInYear)
        {
            throw new ArgumentOutOfRangeException(nameof(e), e.MonthNumber, "Month must be between 1 and 12.");
        }
        if (e.MonthlyBasic < 0 || e.MonthlyHra < 0 || e.MonthlyDa < 0 ||
            e.MonthlySpecialAllowance < 0 || e.MonthlyLta < 0 || e.MonthlyOtherAllowances < 0 ||
            e.AnnualRentPaid < 0 || e.Section80C < 0 || e.Section80CCD1B < 0 ||
            e.Section80D < 0 || e.Section80TTA < 0 || e.HomeLoanInterest < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(e), "All salary and investment amounts must be non-negative.");
        }

        // ---------- Earnings ----------
        decimal basic = Round2(e.MonthlyBasic);
        decimal hra = Round2(e.MonthlyHra);
        decimal da = Round2(e.MonthlyDa);
        decimal special = Round2(e.MonthlySpecialAllowance);
        decimal lta = Round2(e.MonthlyLta);
        decimal other = Round2(e.MonthlyOtherAllowances);
        decimal gross = Round2(basic + hra + da + special + lta + other);

        // ---------- PF (wage base = Basic + DA, capped at the statutory ceiling) ----------
        decimal pfWage = Math.Min(basic + da, PfWageCeiling);
        bool pfOnCeiling = basic + da > PfWageCeiling;
        decimal employeePf = Round2(pfWage * EmployeePfRate);
        decimal employerEps = Round2(pfWage * EmployerEpsRate);
        decimal employerEpf = Round2(pfWage * EmployerEpfRate);
        decimal edli = Round2(pfWage * EdliRate);
        decimal adminCharges = Round2(pfWage * AdminRate);

        // ---------- ESI (on full monthly gross; 0 when above the ceiling or exempt) ----------
        EsiResult esi = EsiCalculatorCore.Calculate(gross, e.HasDisability, e.IsFirstYearEmployee);
        decimal employeeEsi = esi.IsApplicable ? esi.EmployeeContribution : 0m;
        decimal employerEsi = esi.IsApplicable ? esi.EmployerContribution : 0m;

        // ---------- Professional Tax (state slabs, month-aware) ----------
        PtResult pt = PtCalculatorCore.Calculate(gross, e.State, e.MonthNumber);

        // ---------- TDS (annual projected income = gross x 12, divided by 12) ----------
        decimal annualGross = Round2(gross * 12m);
        decimal monthlyTds;
        decimal hraExemptionAnnual = 0m;

        if (e.TaxRegime == TaxRegime.New)
        {
            // New regime: flat standard deduction only; HRA exemption does not apply.
            TaxResult tax = TaxCalculator.Calculate(annualGross);
            monthlyTds = tax.MonthlyTds;
        }
        else
        {
            // Old regime: HRA exemption u/s 10(13A), Chapter VI-A deductions and
            // the annual professional tax computed above are all allowed.
            OldTaxResult tax = OldRegimeCalculator.Calculate(new OldTaxInput(
                GrossSalary: annualGross,
                BasicSalary: Round2(basic * 12m),
                HraReceived: Round2(hra * 12m),
                RentPaid: e.AnnualRentPaid,
                IsMetro: e.IsMetro,
                Section80C: e.Section80C,
                Section80CCD1B: e.Section80CCD1B,
                Section80D: e.Section80D,
                SeniorCitizenParents: false,
                Section80TTA: e.Section80TTA,
                HomeLoanInterest: e.HomeLoanInterest,
                ProfessionalTax: pt.AnnualPt));
            monthlyTds = tax.MonthlyTds;
            hraExemptionAnnual = tax.HraExemption;
        }

        // ---------- Totals ----------
        decimal totalDeductions = Round2(employeePf + employeeEsi + pt.MonthlyPt + monthlyTds);
        decimal totalEmployerCost = Round2(employerEps + employerEpf + employerEsi + edli + adminCharges);
        decimal netTakeHome = Round2(gross - totalDeductions);
        decimal totalCtc = Round2(gross + totalEmployerCost);

        return new PayrollSlip(
            EmployeeId: e.EmployeeId,
            Name: e.Name,
            StateName: PtCalculatorCore.StateDisplayName(e.State),
            TaxRegime: e.TaxRegime,
            MonthNumber: e.MonthNumber,
            MonthName: MonthName(e.MonthNumber),
            Basic: basic,
            Hra: hra,
            Da: da,
            SpecialAllowance: special,
            Lta: lta,
            OtherAllowances: other,
            GrossMonthlySalary: gross,
            EmployeePf: employeePf,
            EmployeeEsi: employeeEsi,
            ProfessionalTax: pt.MonthlyPt,
            Tds: monthlyTds,
            TotalDeductions: totalDeductions,
            EmployerEps: employerEps,
            EmployerEpf: employerEpf,
            EmployerEsi: employerEsi,
            Edli: edli,
            AdminCharges: adminCharges,
            TotalEmployerCost: totalEmployerCost,
            NetMonthlyTakeHome: netTakeHome,
            TotalCtc: totalCtc,
            EsiApplicable: esi.IsApplicable,
            PfContributionBase: pfWage,
            PfOnCeiling: pfOnCeiling,
            HraExemptionAnnual: hraExemptionAnnual,
            IsFebruaryHigherPtSlab: pt.IsFebruaryHigherSlab);
    }

    private static string MonthName(int month) =>
        new DateTime(2025, month, 1).ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture);

    private static decimal Round2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
