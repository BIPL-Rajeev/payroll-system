using PtCalculator;
using Xunit;

namespace PayrollCalculator.Tests;

public class PayrollCalculatorTests
{
    // Scenario 1 helper: Maharashtra, Rs 50,000 basic, old regime, January.
    // Gross = 50,000 + 20,000 HRA + 5,000 DA + 5,000 special = 80,000.
    private static Employee MaharashtraOldRegimeEmployee(int month = 1) => new(
        EmployeeId: "EMP001",
        Name: "Asha Patil",
        State: PtState.Maharashtra,
        IsMetro: true,
        TaxRegime: TaxRegime.Old,
        MonthlyBasic: 50_000m,
        MonthlyHra: 20_000m,
        MonthlyDa: 5_000m,
        MonthlySpecialAllowance: 5_000m,
        MonthlyLta: 0m,
        MonthlyOtherAllowances: 0m,
        AnnualRentPaid: 180_000m,
        Section80C: 150_000m,
        Section80CCD1B: 0m,
        Section80D: 0m,
        Section80TTA: 0m,
        HomeLoanInterest: 0m,
        IsFirstYearEmployee: false,
        HasDisability: false,
        MonthNumber: month);

    // Scenario 2 helper: Karnataka, Rs 15,000 gross, first-year employee, new regime.
    private static Employee KarnatakaFirstYearEmployee(bool hasDisability = false) => new(
        EmployeeId: "EMP002",
        Name: "Ravi Kumar",
        State: PtState.Karnataka,
        IsMetro: false,
        TaxRegime: TaxRegime.New,
        MonthlyBasic: 15_000m,
        MonthlyHra: 0m,
        MonthlyDa: 0m,
        MonthlySpecialAllowance: 0m,
        MonthlyLta: 0m,
        MonthlyOtherAllowances: 0m,
        AnnualRentPaid: 0m,
        Section80C: 0m,
        Section80CCD1B: 0m,
        Section80D: 0m,
        Section80TTA: 0m,
        HomeLoanInterest: 0m,
        IsFirstYearEmployee: !hasDisability,
        HasDisability: hasDisability,
        MonthNumber: 1);

    // Scenario 3 helper: Tamil Nadu, Rs 80,000 gross, old regime.
    private static Employee TamilNaduEmployee(TaxRegime regime = TaxRegime.Old, int month = 1) => new(
        EmployeeId: "EMP003",
        Name: "Meena Iyer",
        State: PtState.TamilNadu,
        IsMetro: false,
        TaxRegime: regime,
        MonthlyBasic: 80_000m,
        MonthlyHra: 0m,
        MonthlyDa: 0m,
        MonthlySpecialAllowance: 0m,
        MonthlyLta: 0m,
        MonthlyOtherAllowances: 0m,
        AnnualRentPaid: 0m,
        Section80C: 0m,
        Section80CCD1B: 0m,
        Section80D: 0m,
        Section80TTA: 0m,
        HomeLoanInterest: 0m,
        IsFirstYearEmployee: false,
        HasDisability: false,
        MonthNumber: month);

    // ---- Scenario 1: Maharashtra, Rs 50,000 basic, old regime, January ----

    [Fact]
    public void Maharashtra50000_OldRegime_AllFourDeductionsAndNetCorrect()
    {
        var s = PayrollCalculatorCore.Calculate(MaharashtraOldRegimeEmployee());

        // Earnings.
        Assert.Equal(80_000m, s.GrossMonthlySalary);

        // PF: wage base capped at 15,000 -> 12% = 1,800.
        Assert.True(s.PfOnCeiling);
        Assert.Equal(15_000m, s.PfContributionBase);
        Assert.Equal(1_800m, s.EmployeePf);

        // ESI: gross 80,000 exceeds the 21,000 ceiling -> 0.
        Assert.False(s.EsiApplicable);
        Assert.Equal(0m, s.EmployeeEsi);

        // PT: Maharashtra, above 10,000, January -> 200.
        Assert.Equal(200m, s.ProfessionalTax);

        // TDS (old regime, annual gross 960,000): HRA exemption
        // min(240,000; 180,000 - 60,000; 300,000) = 120,000. Deductions
        // 50,000 SD + 120,000 HRA + 150,000 80C + 2,500 PT = 322,500;
        // taxable 637,500 -> tax 40,000 + 4% cess = 41,600 -> /12 = 3,466.67 -> 3,467.
        Assert.Equal(120_000m, s.HraExemptionAnnual);
        Assert.Equal(3_467m, s.Tds);

        // Total deductions 1,800 + 0 + 200 + 3,467 = 5,467; net = 74,533.
        Assert.Equal(5_467m, s.TotalDeductions);
        Assert.Equal(74_533m, s.NetMonthlyTakeHome);

        // Employer cost on the 15,000 base: 1,249.50 + 550.50 + 0 + 75 + 75 = 1,950.
        Assert.Equal(1_249.50m, s.EmployerEps);
        Assert.Equal(550.50m, s.EmployerEpf);
        Assert.Equal(0m, s.EmployerEsi);
        Assert.Equal(75m, s.Edli);
        Assert.Equal(75m, s.AdminCharges);
        Assert.Equal(1_950m, s.TotalEmployerCost);
        Assert.Equal(81_950m, s.TotalCtc);
    }

    // ---- Scenario 2: Karnataka, Rs 15,000, first-year, new regime ----

    [Fact]
    public void Karnataka15000_FirstYear_EsiEmployeeIsZero()
    {
        var s = PayrollCalculatorCore.Calculate(KarnatakaFirstYearEmployee());

        Assert.True(s.EsiApplicable);          // 15,000 <= 21,000 ceiling
        Assert.Equal(0m, s.EmployeeEsi);       // first-year exemption
        Assert.Equal(488m, s.EmployerEsi);     // employer still pays 3.25% (487.5 -> 488)

        // PF on 15,000 (at, not above, the ceiling): employee 1,800.
        Assert.False(s.PfOnCeiling);
        Assert.Equal(1_800m, s.EmployeePf);

        // PT: Karnataka nil up to 15,000.
        Assert.Equal(0m, s.ProfessionalTax);

        // TDS new regime: 180,000 - 75,000 SD = 105,000 taxable -> nil slab -> 0.
        Assert.Equal(0m, s.Tds);

        Assert.Equal(1_800m, s.TotalDeductions);
        Assert.Equal(13_200m, s.NetMonthlyTakeHome);
        Assert.Equal(2_438m, s.TotalEmployerCost);   // 1,249.50 + 550.50 + 488 + 75 + 75
        Assert.Equal(17_438m, s.TotalCtc);
    }

    // Disability waives the ESI employee share exactly like the first-year flag.
    [Fact]
    public void Karnataka15000_Disability_EsiEmployeeIsZero()
    {
        var s = PayrollCalculatorCore.Calculate(KarnatakaFirstYearEmployee(hasDisability: true));

        Assert.Equal(0m, s.EmployeeEsi);
        Assert.Equal(488m, s.EmployerEsi);
    }

    // ---- Scenario 3: Tamil Nadu, Rs 80,000 -> all four calculators fire ----

    [Fact]
    public void TamilNadu80000_AllFourCalculatorsFire()
    {
        var s = PayrollCalculatorCore.Calculate(TamilNaduEmployee());

        // PF fires: 15,000 base -> 1,800.
        Assert.Equal(1_800m, s.EmployeePf);

        // ESI fires (evaluated) but is not applicable above the ceiling -> 0.
        Assert.False(s.EsiApplicable);
        Assert.Equal(0m, s.EmployeeEsi);

        // PT fires: TN top slab above 75,000 -> 1,250.
        Assert.Equal(1_250m, s.ProfessionalTax);

        // TDS fires (old regime): annual 960,000 - 50,000 SD - 15,000 annual PT
        // = 895,000 taxable -> 12,500 + 79,000 = 91,500 + 4% cess = 95,160 -> /12 = 7,930.
        Assert.Equal(7_930m, s.Tds);

        Assert.Equal(10_980m, s.TotalDeductions);
        Assert.Equal(69_020m, s.NetMonthlyTakeHome);
        Assert.Equal(1_950m, s.TotalEmployerCost);
        Assert.Equal(81_950m, s.TotalCtc);
    }

    // ---- February PT rule (Maharashtra only) ----

    [Fact]
    public void Maharashtra_February_PtIs300()
    {
        var jan = PayrollCalculatorCore.Calculate(MaharashtraOldRegimeEmployee(month: 1));
        var feb = PayrollCalculatorCore.Calculate(MaharashtraOldRegimeEmployee(month: 2));

        Assert.Equal(200m, jan.ProfessionalTax);
        Assert.False(jan.IsFebruaryHigherPtSlab);

        Assert.Equal(300m, feb.ProfessionalTax);
        Assert.True(feb.IsFebruaryHigherPtSlab);
        // Deductions grow by exactly the extra PT: 5,467 + 100 = 5,567.
        Assert.Equal(5_567m, feb.TotalDeductions);
        Assert.Equal(74_433m, feb.NetMonthlyTakeHome);
    }

    // ---- Regime rules ----

    // New regime ignores HRA exemption and Chapter VI-A investments entirely.
    [Fact]
    public void NewRegime_IgnoresHraAndInvestments()
    {
        var old = PayrollCalculatorCore.Calculate(TamilNaduEmployee(TaxRegime.Old));
        var @new = PayrollCalculatorCore.Calculate(TamilNaduEmployee(TaxRegime.New));

        // Old: taxable 895,000 -> TDS 7,930.
        Assert.Equal(7_930m, old.Tds);

        // New: 960,000 - 75,000 SD = 885,000 taxable -> slab tax 60,000,
        // 87A rebate 60,000 (taxable <= 12L) -> 0 TDS.
        Assert.Equal(0m, @new.Tds);
        Assert.Equal(0m, @new.HraExemptionAnnual);
        // Net differs by exactly the TDS difference: 69,020 + 7,930 = 76,950.
        Assert.Equal(76_950m, @new.NetMonthlyTakeHome);
    }

    // HRA exemption is 0 when no rent is paid, even with HRA received.
    [Fact]
    public void OldRegime_NoRent_NoHraExemption()
    {
        Employee e = MaharashtraOldRegimeEmployee() with { AnnualRentPaid = 0m };
        var s = PayrollCalculatorCore.Calculate(e);

        Assert.Equal(0m, s.HraExemptionAnnual);
    }

    // ---- Validation ----

    [Fact]
    public void NegativeComponent_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PayrollCalculatorCore.Calculate(MaharashtraOldRegimeEmployee() with { MonthlyBasic = -1m }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PayrollCalculatorCore.Calculate(MaharashtraOldRegimeEmployee() with { Section80C = -1m }));
    }

    [Fact]
    public void MonthOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PayrollCalculatorCore.Calculate(MaharashtraOldRegimeEmployee(month: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PayrollCalculatorCore.Calculate(MaharashtraOldRegimeEmployee(month: 13)));
    }

    [Fact]
    public void BlankEmployeeId_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => PayrollCalculatorCore.Calculate(MaharashtraOldRegimeEmployee() with { EmployeeId = "  " }));
    }
}
