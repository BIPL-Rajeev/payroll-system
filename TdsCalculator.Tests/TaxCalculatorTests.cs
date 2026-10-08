using Xunit;

namespace TdsCalculator.Tests;

public class TaxCalculatorTests
{
    // Case 1: Income Rs 5,00,000 -> zero tax (rebate).
    [Fact]
    public void TaxableIncome_500000_ZeroTaxAfterRebate()
    {
        var r = TaxCalculator.FromTaxableIncome(5_00_000m, grossSalary: 5_75_000m);

        Assert.Equal(5_00_000m, r.TaxableIncome);
        Assert.Equal(5_000m, r.TaxBeforeRebate);          // 1,00,000 @ 5%
        Assert.Equal(5_000m, r.Rebate);                   // 87A rebate wipes it out
        Assert.Equal(0m, r.TaxAfterRebate);
        Assert.Equal(0m, r.Surcharge);
        Assert.Equal(0m, r.Cess);
        Assert.Equal(0m, r.TotalAnnualTax);
        Assert.Equal(0m, r.MonthlyTds);
    }

    // Case 2: Income Rs 12,00,000 -> zero tax (rebate up to 60,000).
    [Fact]
    public void TaxableIncome_1200000_ZeroTaxAfterRebate()
    {
        var r = TaxCalculator.FromTaxableIncome(12_00_000m, grossSalary: 12_75_000m);

        Assert.Equal(12_00_000m, r.TaxableIncome);
        Assert.Equal(60_000m, r.TaxBeforeRebate);         // 20k@5% + 40k@10%
        Assert.Equal(60_000m, r.Rebate);                  // capped exactly at 60,000
        Assert.Equal(0m, r.TaxAfterRebate);
        Assert.Equal(0m, r.TotalAnnualTax);
        Assert.Equal(0m, r.MonthlyTds);
    }

    // Case 3: Income Rs 12,50,000 -> marginal relief applies.
    [Fact]
    public void TaxableIncome_1250000_MarginalReliefCapsTax()
    {
        var r = TaxCalculator.FromTaxableIncome(12_50_000m, grossSalary: 13_25_000m);

        Assert.Equal(67_500m, r.TaxBeforeRebate);         // 60,000 + 50,000@15%
        Assert.Equal(0m, r.Rebate);                       // above 12,00,000 -> no rebate
        Assert.Equal(67_500m, r.TaxAfterRebate);
        // Tax payable capped at income - 12,00,000 = 50,000
        Assert.Equal(17_500m, r.MarginalRelief);
        Assert.Equal(50_000m, r.TaxAfterRelief);
        Assert.Equal(0m, r.Surcharge);
        Assert.Equal(2_000m, r.Cess);                     // 4% of 50,000
        Assert.Equal(52_000m, r.TotalAnnualTax);
        Assert.Equal(4_333m, r.MonthlyTds);               // 52,000 / 12 = 4,333.33
    }

    // Case 3 boundary: relief stops once normal tax falls below (income - 12L).
    [Fact]
    public void TaxableIncome_1300000_NoMarginalRelief()
    {
        var r = TaxCalculator.FromTaxableIncome(13_00_000m, grossSalary: 13_75_000m);

        Assert.Equal(75_000m, r.TaxBeforeRebate);         // 60,000 + 1,00,000@15%
        Assert.Equal(0m, r.MarginalRelief);               // 75,000 < 1,00,000 cap
        Assert.Equal(75_000m, r.TaxAfterRelief);
        Assert.Equal(3_000m, r.Cess);
        Assert.Equal(78_000m, r.TotalAnnualTax);
    }

    // Case 4: Income Rs 25,00,000 -> 30% slab + cess.
    [Fact]
    public void TaxableIncome_2500000_TopSlabWithCess()
    {
        var r = TaxCalculator.FromTaxableIncome(25_00_000m, grossSalary: 25_75_000m);

        Assert.Equal(3_30_000m, r.TaxBeforeRebate);       // 3,00,000 up to 24L + 1,00,000@30%
        Assert.Equal(0m, r.Rebate);
        Assert.Equal(0m, r.MarginalRelief);
        Assert.Equal(0m, r.Surcharge);                    // below 50,00,000
        Assert.Equal(13_200m, r.Cess);                    // 4% of 3,30,000
        Assert.Equal(3_43_200m, r.TotalAnnualTax);
        Assert.Equal(28_600m, r.MonthlyTds);              // 3,43,200 / 12
    }

    // Standard deduction of 75,000 is applied to gross salary.
    [Fact]
    public void Calculate_AppliesStandardDeduction()
    {
        var r = TaxCalculator.Calculate(12_75_000m);

        Assert.Equal(12_00_000m, r.TaxableIncome);
        Assert.Equal(0m, r.TotalAnnualTax);               // rebate at exactly 12,00,000
    }

    // Gross below the standard deduction -> nil taxable income, nil tax.
    [Fact]
    public void Calculate_GrossBelowStandardDeduction_ZeroTax()
    {
        var r = TaxCalculator.Calculate(50_000m);

        Assert.Equal(0m, r.TaxableIncome);
        Assert.Equal(0m, r.TotalAnnualTax);
        Assert.Equal(0m, r.MonthlyTds);
    }

    // Surcharge: 10% above 50,00,000; 37% bracket does not exist in new regime.
    [Fact]
    public void TaxableIncome_6000000_Surcharge10Percent()
    {
        var r = TaxCalculator.FromTaxableIncome(60_00_000m, grossSalary: 60_75_000m);

        Assert.Equal(13_80_000m, r.TaxBeforeRebate);      // 3,00,000 + 36,00,000@30%
        Assert.Equal(1_38_000m, r.Surcharge);             // 10%
        Assert.Equal(60_720m, r.Cess);                    // 4% of 15,18,000
        Assert.Equal(15_78_720m, r.TotalAnnualTax);
    }

    // Surcharge: 15% above 1,00,00,000.
    [Fact]
    public void TaxableIncome_12000000_Surcharge15Percent()
    {
        var r = TaxCalculator.FromTaxableIncome(1_20_00_000m, grossSalary: 1_20_75_000m);

        Assert.Equal(31_80_000m, r.TaxBeforeRebate);      // 3,00,000 + 96,00,000@30%
        Assert.Equal(4_77_000m, r.Surcharge);             // 15%
        Assert.Equal(1_46_280m, r.Cess);                  // 4% of 36,57,000
        Assert.Equal(38_03_280m, r.TotalAnnualTax);
    }

    // Surcharge: 25% above 2,00,00,000 (37% not applicable under new regime).
    [Fact]
    public void TaxableIncome_30000000_Surcharge25Percent()
    {
        var r = TaxCalculator.FromTaxableIncome(3_00_00_000m, grossSalary: 3_00_75_000m);

        Assert.Equal(0.25m, SurchargeRateOf(r));
        Assert.Equal(85_80_000m, r.TaxBeforeRebate);      // 3,00,000 + 2,76,00,000@30%
        Assert.Equal(21_45_000m, r.Surcharge);            // 25%
    }

    // Round-off: monthly TDS rounds to the nearest rupee (away from zero).
    [Fact]
    public void MonthlyTds_RoundsToNearestRupee()
    {
        var r = TaxCalculator.FromTaxableIncome(12_50_000m, grossSalary: 13_25_000m);

        Assert.Equal(52_000m, r.TotalAnnualTax);
        Assert.Equal(4_333m, r.MonthlyTds);
        Assert.Equal(Math.Round(r.TotalAnnualTax / 12m, 0, MidpointRounding.AwayFromZero), r.MonthlyTds);
    }

    // Validation: negative values are rejected at the calculation boundary.
    [Fact]
    public void Calculate_NegativeGross_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TaxCalculator.Calculate(-1m));
    }

    [Fact]
    public void FromTaxableIncome_NegativeIncome_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TaxCalculator.FromTaxableIncome(-1m, 0m));
    }

    // Helper: surcharge rate is not exposed on the result; derive it back.
    private static decimal SurchargeRateOf(TaxResult r) =>
        r.TaxAfterRelief == 0 ? 0m : Math.Round(r.Surcharge / r.TaxAfterRelief, 4);
}
