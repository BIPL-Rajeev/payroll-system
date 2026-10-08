using Xunit;

namespace TdsCalculator.Tests;

public class OldRegimeCalculatorTests
{
    private static OldTaxInput Input(
        decimal gross,
        decimal basic = 0m,
        decimal hra = 0m,
        decimal rent = 0m,
        bool metro = false,
        decimal s80c = 0m,
        decimal s80ccd = 0m,
        decimal s80d = 0m,
        bool senior = false,
        decimal s80tta = 0m,
        decimal s24b = 0m,
        decimal pt = 0m) =>
        new(gross, basic, hra, rent, metro, s80c, s80ccd, s80d, senior, s80tta, s24b, pt);

    // Case 1: Income Rs 5,00,000 with 80C Rs 1,50,000 -> zero tax after rebate.
    [Fact]
    public void Gross500000_80C150000_ZeroTaxAfterRebate()
    {
        var r = OldRegimeCalculator.Calculate(Input(gross: 5_00_000m, s80c: 1_50_000m));

        Assert.Equal(2_00_000m, r.TotalDeductions);      // 50,000 SD + 1,50,000 80C
        Assert.Equal(3_00_000m, r.TaxableIncome);
        Assert.Equal(2_500m, r.TaxBeforeRebate);         // 50,000 @ 5%
        Assert.Equal(2_500m, r.Rebate);                  // 87A wipes it out
        Assert.Equal(0m, r.TaxAfterRebate);
        Assert.Equal(0m, r.TotalAnnualTax);
        Assert.Equal(0m, r.MonthlyTds);
    }

    // Case 1b: taxable income lands exactly on Rs 5,00,000 -> rebate capped at 12,500 -> zero tax.
    [Fact]
    public void TaxableExactly500000_Rebate12500_ZeroTax()
    {
        var r = OldRegimeCalculator.Calculate(Input(gross: 7_00_000m, s80c: 1_50_000m));

        Assert.Equal(5_00_000m, r.TaxableIncome);
        Assert.Equal(12_500m, r.TaxBeforeRebate);        // 2,50,000 nil + 2,50,000 @ 5%
        Assert.Equal(12_500m, r.Rebate);                 // capped at max 12,500
        Assert.Equal(0m, r.TaxAfterRebate);
        Assert.Equal(0m, r.TotalAnnualTax);
    }

    // Case 2: Income Rs 10,00,000 with HRA exemption in metro.
    [Fact]
    public void Gross1000000_HraExemptionMetro()
    {
        // least of: actual HRA 3,00,000 | rent 4,00,000 - 10% basic 50,000 = 3,50,000 | 50% basic = 2,50,000
        var r = OldRegimeCalculator.Calculate(Input(
            gross: 10_00_000m, basic: 5_00_000m, hra: 3_00_000m, rent: 4_00_000m, metro: true));

        Assert.Equal(2_50_000m, r.HraExemption);
        Assert.Equal(3_00_000m, r.TotalDeductions);       // 50,000 SD + 2,50,000 HRA
        Assert.Equal(7_00_000m, r.TaxableIncome);
        Assert.Equal(52_500m, r.TaxBeforeRebate);        // 12,500 + 2,00,000 @ 20%
        Assert.Equal(0m, r.Rebate);
        Assert.Equal(0m, r.Surcharge);
        Assert.Equal(2_100m, r.Cess);                    // 4% of 52,500
        Assert.Equal(54_600m, r.TotalAnnualTax);
        Assert.Equal(4_550m, r.MonthlyTds);              // 54,600 / 12
    }

    // Case 2b: same figures, non-metro -> 40% of basic caps the exemption lower.
    [Fact]
    public void Gross1000000_HraExemptionNonMetro_IsLower()
    {
        var r = OldRegimeCalculator.Calculate(Input(
            gross: 10_00_000m, basic: 5_00_000m, hra: 3_00_000m, rent: 4_00_000m, metro: false));

        Assert.Equal(2_00_000m, r.HraExemption);          // 40% of basic = 2,00,000 is least
        Assert.Equal(7_50_000m, r.TaxableIncome);
        Assert.Equal(62_500m, r.TaxBeforeRebate);        // 12,500 + 2,50,000 @ 20%
        Assert.Equal(65_000m, r.TotalAnnualTax);
    }

    // Case 3: Income Rs 55,00,000 -> surcharge applies (statutory marginal relief does NOT
    // kick in here: tax+surcharge 16,08,750 is below the cap of 18,12,500).
    [Fact]
    public void Gross5500000_SurchargeApplies_NoMarginalRelief()
    {
        var r = OldRegimeCalculator.Calculate(Input(gross: 55_50_000m));

        Assert.Equal(55_00_000m, r.TaxableIncome);
        Assert.Equal(14_62_500m, r.TaxBeforeRebate);      // 12,500 + 1,00,000 + 45,00,000 @ 30%
        Assert.Equal(0m, r.Rebate);
        Assert.Equal(0.10m, r.SurchargeRate);
        Assert.Equal(1_46_250m, r.Surcharge);            // 10% of 14,62,500
        Assert.Equal(0m, r.MarginalRelief);
        Assert.Equal(64_350m, r.Cess);                   // 4% of 16,08,750
        Assert.Equal(16_73_100m, r.TotalAnnualTax);
        Assert.Equal(1_39_425m, r.MonthlyTds);
    }

    // Case 3b: just above Rs 50,00,000 -> statutory surcharge marginal relief applies.
    [Fact]
    public void Gross5100000_SurchargeMarginalReliefApplies()
    {
        var r = OldRegimeCalculator.Calculate(Input(gross: 51_50_000m));

        Assert.Equal(51_00_000m, r.TaxableIncome);
        Assert.Equal(13_42_500m, r.TaxBeforeRebate);      // 13,12,500 + 1,00,000 @ 30%
        Assert.Equal(1_34_250m, r.Surcharge);            // 10%
        // Cap = tax at 50L (13,12,500) + excess income (1,00,000) = 14,12,500
        Assert.Equal(64_250m, r.MarginalRelief);         // 14,76,750 - 14,12,500
        Assert.Equal(14_12_500m, r.TaxAfterRebate + r.Surcharge - r.MarginalRelief);
        Assert.Equal(56_500m, r.Cess);                   // 4% of 14,12,500
        Assert.Equal(14_69_000m, r.TotalAnnualTax);
        Assert.Equal(1_22_417m, r.MonthlyTds);
    }

    // Case 3c: marginal relief also applies at the Rs 1,00,00,000 threshold (15% tier).
    [Fact]
    public void Gross10100000_Surcharge15PercentWithMarginalRelief()
    {
        var r = OldRegimeCalculator.Calculate(Input(gross: 1_01_50_000m));

        Assert.Equal(1_01_00_000m, r.TaxableIncome);
        Assert.Equal(0.15m, r.SurchargeRate);
        Assert.Equal(28_42_500m, r.TaxBeforeRebate);
        Assert.Equal(4_26_375m, r.Surcharge);            // 15%
        // Cap = tax at 1Cr (28,12,500) x 1.10 + excess (1,00,000) = 31,93,750
        Assert.Equal(75_125m, r.MarginalRelief);         // 32,68,875 - 31,93,750
        Assert.Equal(1_27_750m, r.Cess);                 // 4% of 31,93,750
        Assert.Equal(33_21_500m, r.TotalAnnualTax);
    }

    // Deduction caps are enforced.
    [Fact]
    public void DeductionCaps_AreEnforced()
    {
        var r = OldRegimeCalculator.Calculate(Input(
            gross: 30_00_000m,
            basic: 10_00_000m,
            hra: 3_00_000m,
            rent: 4_00_000m,
            metro: true,
            s80c: 5_00_000m,      // capped at 1,50,000
            s80ccd: 1_00_000m,    // capped at 50,000
            s80d: 2_00_000m,      // capped at 75,000 (senior parents)
            senior: true,
            s80tta: 50_000m,      // capped at 10,000
            s24b: 5_00_000m,      // capped at 2,00,000
            pt: 2_400m));

        Assert.Equal(1_50_000m, r.Section80C);
        Assert.Equal(50_000m, r.Section80CCD1B);
        Assert.Equal(75_000m, r.Section80D);
        Assert.Equal(10_000m, r.Section80TTA);
        Assert.Equal(2_00_000m, r.Section24B);
        // 50,000 SD + 3,00,000 HRA + 1,50,000 + 50,000 + 75,000 + 10,000 + 2,00,000 + 2,400
        Assert.Equal(8_37_400m, r.TotalDeductions);
    }

    // 80D cap without senior citizen parents is 25,000.
    [Fact]
    public void Section80D_WithoutSeniorParents_CappedAt25000()
    {
        var r = OldRegimeCalculator.Calculate(Input(gross: 5_00_000m, s80d: 1_00_000m, senior: false));

        Assert.Equal(25_000m, r.Section80D);
    }

    // Professional tax is fully deductible (no cap).
    [Fact]
    public void ProfessionalTax_FullyDeductible()
    {
        var r = OldRegimeCalculator.Calculate(Input(gross: 5_00_000m, pt: 30_000m));

        Assert.Equal(30_000m, r.ProfessionalTax);
        Assert.Equal(50_000m + 30_000m, r.TotalDeductions);
        Assert.Equal(4_20_000m, r.TaxableIncome);
    }

    // HRA exemption edge cases: zero rent or zero HRA -> no exemption.
    [Fact]
    public void HraExemption_ZeroRentOrZeroHra_IsZero()
    {
        Assert.Equal(0m, OldRegimeCalculator.HraExemption(3_00_000m, 5_00_000m, 0m, true));
        Assert.Equal(0m, OldRegimeCalculator.HraExemption(0m, 5_00_000m, 4_00_000m, true));
    }

    // Negative values are rejected at the calculation boundary.
    [Fact]
    public void Calculate_NegativeGross_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OldRegimeCalculator.Calculate(Input(gross: -1m)));
    }

    [Fact]
    public void Calculate_NegativeDeduction_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OldRegimeCalculator.Calculate(Input(gross: 1m, s80c: -1m)));
    }

    [Fact]
    public void HraExemption_NegativeRent_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OldRegimeCalculator.HraExemption(1m, 1m, -1m, true));
    }
}
