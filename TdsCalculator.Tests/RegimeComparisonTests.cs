using Xunit;

namespace TdsCalculator.Tests;

public class RegimeComparisonTests
{
    // Case 4: same income under both regimes - heavy deductions favour the Old Regime.
    [Fact]
    public void SameIncome_HeavyDeductions_OldRegimeWins()
    {
        // Gross 15,00,000. Old regime deductions: SD 50,000 + HRA (metro) + 80C 1,50,000
        // + 80CCD(1B) 50,000 + 80D 25,000 + 80TTA 10,000 + 24(b) 2,00,000 + PT 2,400.
        var input = new OldTaxInput(
            GrossSalary: 15_00_000m,
            BasicSalary: 5_00_000m,
            HraReceived: 2_40_000m,
            RentPaid: 3_60_000m,
            IsMetro: true,
            Section80C: 1_50_000m,
            Section80CCD1B: 50_000m,
            Section80D: 25_000m,
            SeniorCitizenParents: false,
            Section80TTA: 10_000m,
            HomeLoanInterest: 2_00_000m,
            ProfessionalTax: 2_400m);

        RegimeComparisonResult c = RegimeComparison.Compare(input);

        // New regime: taxable = 15,00,000 - 75,000 = 14,25,000
        //   0-4L nil, 4-8L 20,000, 8-12L 40,000, 12-14.25L 33,750 -> 93,750 + 4% cess = 97,500
        Assert.Equal(14_25_000m, c.NewRegime.TaxableIncome);
        Assert.Equal(93_750m, c.NewRegime.TaxBeforeRebate);
        Assert.Equal(3_750m, c.NewRegime.Cess);
        Assert.Equal(97_500m, c.NewRegime.TotalAnnualTax);
        Assert.Equal(8_125m, c.NewRegime.MonthlyTds);

        // Old regime: HRA exemption = least of 2,40,000 | 3,60,000-50,000=3,10,000 | 50% of 5,00,000=2,50,000
        //   Total deductions = 50,000 + 2,40,000 + 1,50,000 + 50,000 + 25,000 + 10,000 + 2,00,000 + 2,400
        //                   = 7,27,400 -> taxable = 7,72,600
        Assert.Equal(2_40_000m, c.OldRegime.HraExemption);
        Assert.Equal(7_27_400m, c.OldRegime.TotalDeductions);
        Assert.Equal(7_72_600m, c.OldRegime.TaxableIncome);

        // Old total: 12,500 + 2,72,600 x 20% = 67,020 + 4% cess 2,680.80 = 69,700.80
        Assert.Equal(67_020m, c.OldRegime.TaxBeforeRebate);
        Assert.Equal(69_700.80m, c.OldRegime.TotalAnnualTax);

        Assert.Equal(BetterRegime.Old, c.Better);
        Assert.Equal(27_799.20m, c.TaxSaving);
    }

    // Same income, no meaningful deductions - the New Regime wins.
    [Fact]
    public void SameIncome_NoDeductions_NewRegimeWins()
    {
        var input = new OldTaxInput(
            GrossSalary: 20_00_000m,
            BasicSalary: 8_00_000m,
            HraReceived: 0m,
            RentPaid: 0m,
            IsMetro: false,
            Section80C: 0m,
            Section80CCD1B: 0m,
            Section80D: 0m,
            SeniorCitizenParents: false,
            Section80TTA: 0m,
            HomeLoanInterest: 0m,
            ProfessionalTax: 0m);

        RegimeComparisonResult c = RegimeComparison.Compare(input);

        // New: taxable 19,25,000 -> 20k + 40k + 60k + 20k(5% of 3.25L) ... 
        //   0-4L nil; 4-8L 20,000; 8-12L 40,000; 12-16L 60,000; 16-19.25L 65,000 -> 1,85,000
        Assert.Equal(19_25_000m, c.NewRegime.TaxableIncome);
        Assert.Equal(1_85_000m, c.NewRegime.TaxBeforeRebate);
        Assert.Equal(7_400m, c.NewRegime.Cess);
        Assert.Equal(1_92_400m, c.NewRegime.TotalAnnualTax);

        // Old: taxable = 20,00,000 - 50,000 = 19,50,000 -> 12,500 + 1,00,000 + 9,50,000 x 30% = 3,97,500
        //   + 4% cess 15,900 = 4,13,400
        Assert.Equal(19_50_000m, c.OldRegime.TaxableIncome);
        Assert.Equal(3_97_500m, c.OldRegime.TaxBeforeRebate);
        Assert.Equal(15_900m, c.OldRegime.Cess);
        Assert.Equal(4_13_400m, c.OldRegime.TotalAnnualTax);

        Assert.Equal(BetterRegime.New, c.Better);
        Assert.Equal(2_21_000m, c.TaxSaving);
    }

    // Equal totals -> tie.
    [Fact]
    public void SameTax_BothRegimes_Tie()
    {
        var zeroNew = TaxCalculator.Calculate(50_000m);
        Assert.Equal(0m, zeroNew.TotalAnnualTax);

        var zeroOld = OldRegimeCalculator.Calculate(new OldTaxInput(
            GrossSalary: 50_000m, BasicSalary: 0m, HraReceived: 0m, RentPaid: 0m,
            IsMetro: false, Section80C: 0m, Section80CCD1B: 0m, Section80D: 0m,
            SeniorCitizenParents: false, Section80TTA: 0m, HomeLoanInterest: 0m,
            ProfessionalTax: 0m));
        Assert.Equal(0m, zeroOld.TotalAnnualTax);
    }

    // Comparison reuses the same gross salary for both regimes.
    [Fact]
    public void Comparison_UsesSameGrossForBothRegimes()
    {
        var input = new OldTaxInput(
            GrossSalary: 12_00_000m, BasicSalary: 5_00_000m, HraReceived: 0m, RentPaid: 0m,
            IsMetro: false, Section80C: 1_50_000m, Section80CCD1B: 0m, Section80D: 0m,
            SeniorCitizenParents: false, Section80TTA: 0m, HomeLoanInterest: 0m,
            ProfessionalTax: 0m);

        RegimeComparisonResult c = RegimeComparison.Compare(input);

        Assert.Equal(input.GrossSalary, c.NewRegime.GrossSalary);
        Assert.Equal(input.GrossSalary, c.OldRegime.GrossSalary);
        // New regime: taxable 11,25,000 <= 12,00,000 -> full rebate -> zero tax
        Assert.Equal(0m, c.NewRegime.TotalAnnualTax);
        // Old regime: taxable 12,00,000 - 50,000 - 1,50,000 = 10,00,000
        //   tax = 12,500 + 5,00,000 x 20% = 1,12,500 + 4% cess 4,500 = 1,17,000
        Assert.Equal(10_00_000m, c.OldRegime.TaxableIncome);
        Assert.Equal(1_12_500m, c.OldRegime.TaxBeforeRebate);
        Assert.Equal(1_17_000m, c.OldRegime.TotalAnnualTax);
        Assert.Equal(BetterRegime.New, c.Better);
    }
}
