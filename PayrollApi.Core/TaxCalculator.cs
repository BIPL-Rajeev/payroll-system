namespace PayrollApi.Core;

/// <summary>Result of a new-regime (FY 2025-26) TDS computation.</summary>
public sealed record TaxResult(
    decimal GrossSalary,
    decimal TaxableIncome,
    decimal TaxBeforeRebate,
    decimal Rebate,
    decimal TaxAfterRebate,
    decimal MarginalRelief,
    decimal TaxAfterRelief,
    decimal Surcharge,
    decimal Cess,
    decimal TotalAnnualTax,
    decimal MonthlyTds);

/// <summary>
/// Indian payroll TDS under the NEW tax regime for FY 2025-26.
/// Only the flat standard deduction is allowed; 80C/80D, HRA exemption and
/// 80CCD(2) employer NPS are NOT applied under the new regime.
/// </summary>
public static class TaxCalculator
{
    public const decimal StandardDeduction = 75_000m;
    public const decimal RebateLimit = 12_00_000m;
    public const decimal MaxRebate = 60_000m;
    public const decimal CessRate = 0.04m;

    /// <summary>New regime slabs for FY 2025-26 (upper bound, rate).</summary>
    private static readonly (decimal Upper, decimal Rate)[] Slabs =
    {
        (4_00_000m, 0.00m),
        (8_00_000m, 0.05m),
        (12_00_000m, 0.10m),
        (16_00_000m, 0.15m),
        (20_00_000m, 0.20m),
        (24_00_000m, 0.25m),
        (decimal.MaxValue, 0.30m),
    };

    public static TaxResult Calculate(decimal grossSalary)
    {
        if (grossSalary < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(grossSalary), "Gross salary cannot be negative.");
        }

        decimal taxableIncome = Round2(Math.Max(0m, grossSalary - StandardDeduction));
        return FromTaxableIncome(taxableIncome, grossSalary);
    }

    /// <summary>Computes tax starting from an already-known taxable income.</summary>
    public static TaxResult FromTaxableIncome(decimal taxableIncome, decimal grossSalary)
    {
        if (taxableIncome < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(taxableIncome), "Taxable income cannot be negative.");
        }

        decimal taxBeforeRebate = Round2(SlabTax(taxableIncome));

        // 87A rebate: full tax waived when taxable income <= 12,00,000 (capped at 60,000).
        decimal rebate = taxableIncome <= RebateLimit
            ? Round2(Math.Min(taxBeforeRebate, MaxRebate))
            : 0m;
        decimal taxAfterRebate = Round2(taxBeforeRebate - rebate);

        // Marginal relief just above 12,00,000: tax payable must not exceed (income - 12,00,000).
        decimal marginalRelief = 0m;
        if (taxableIncome > RebateLimit)
        {
            decimal taxCapped = Round2(taxableIncome - RebateLimit);
            if (taxAfterRebate > taxCapped)
            {
                marginalRelief = Round2(taxAfterRebate - taxCapped);
            }
        }
        decimal taxAfterRelief = Round2(taxAfterRebate - marginalRelief);

        decimal surchargeRate = SurchargeRate(taxableIncome);
        decimal surcharge = Round2(taxAfterRelief * surchargeRate);
        decimal cess = Round2((taxAfterRelief + surcharge) * CessRate);
        decimal totalAnnualTax = Round2(taxAfterRelief + surcharge + cess);
        decimal monthlyTds = Math.Round(totalAnnualTax / 12m, 0, MidpointRounding.AwayFromZero);

        return new TaxResult(
            GrossSalary: Round2(grossSalary),
            TaxableIncome: taxableIncome,
            TaxBeforeRebate: taxBeforeRebate,
            Rebate: rebate,
            TaxAfterRebate: taxAfterRebate,
            MarginalRelief: marginalRelief,
            TaxAfterRelief: taxAfterRelief,
            Surcharge: surcharge,
            Cess: cess,
            TotalAnnualTax: totalAnnualTax,
            MonthlyTds: monthlyTds);
    }

    private static decimal SlabTax(decimal taxableIncome)
    {
        decimal tax = 0m;
        decimal lower = 0m;

        foreach ((decimal upper, decimal rate) in Slabs)
        {
            if (taxableIncome <= lower)
            {
                break;
            }

            decimal slice = Math.Min(taxableIncome, upper) - lower;
            tax += slice * rate;
            lower = upper;
        }

        return tax;
    }

    private static decimal SurchargeRate(decimal taxableIncome) => taxableIncome switch
    {
        > 2_00_00_000m => 0.25m, // 37% is NOT applicable under the new regime
        > 1_00_00_000m => 0.15m,
        > 50_00_000m => 0.10m,
        _ => 0m,
    };

    private static decimal Round2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
