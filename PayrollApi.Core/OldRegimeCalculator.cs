namespace PayrollApi.Core;

/// <summary>All inputs needed to compute tax under the OLD regime, FY 2025-26.</summary>
public sealed record OldTaxInput(
    decimal GrossSalary,
    decimal BasicSalary,
    decimal HraReceived,
    decimal RentPaid,
    bool IsMetro,
    decimal Section80C,
    decimal Section80CCD1B,
    decimal Section80D,
    bool SeniorCitizenParents,
    decimal Section80TTA,
    decimal HomeLoanInterest,
    decimal ProfessionalTax);

/// <summary>Result of an old-regime (FY 2025-26) computation, with every deduction itemised.</summary>
public sealed record OldTaxResult(
    decimal GrossSalary,
    decimal StandardDeduction,
    decimal HraExemption,
    bool HraMetro,
    decimal Section80C,
    decimal Section80CCD1B,
    decimal Section80D,
    bool SeniorCitizenParents,
    decimal Section80TTA,
    decimal Section24B,
    decimal ProfessionalTax,
    decimal TotalDeductions,
    decimal TaxableIncome,
    decimal TaxBeforeRebate,
    decimal Rebate,
    decimal TaxAfterRebate,
    decimal SurchargeRate,
    decimal Surcharge,
    decimal MarginalRelief,
    decimal Cess,
    decimal TotalAnnualTax,
    decimal MonthlyTds);

/// <summary>
/// Indian payroll tax under the OLD regime for FY 2025-26:
/// Rs 50,000 standard deduction, HRA exemption u/s 10(13A), Chapter VI-A
/// deductions (80C/80CCD(1B)/80D/80TTA), 24(b) and professional tax.
/// </summary>
public static class OldRegimeCalculator
{
    public const decimal StandardDeduction = 50_000m;
    public const decimal Cap80C = 1_50_000m;
    public const decimal Cap80CCD1B = 50_000m;
    public const decimal Cap80DBase = 25_000m;
    public const decimal Cap80DSeniorExtra = 50_000m;
    public const decimal Cap80TTA = 10_000m;
    public const decimal Cap24B = 2_00_000m;
    public const decimal RebateLimit = 5_00_000m;
    public const decimal MaxRebate = 12_500m;
    public const decimal CessRate = 0.04m;

    /// <summary>Old regime slabs FY 2025-26 (upper bound, rate).</summary>
    private static readonly (decimal Upper, decimal Rate)[] Slabs =
    {
        (2_50_000m, 0.00m),
        (5_00_000m, 0.05m),
        (10_00_000m, 0.20m),
        (decimal.MaxValue, 0.30m),
    };

    /// <summary>
    /// Surcharge tiers: threshold, rate above the threshold, and the rate that applies
    /// AT the threshold (used as the baseline for marginal relief).
    /// </summary>
    private static readonly (decimal Threshold, decimal Rate, decimal RateAtThreshold)[] SurchargeTiers =
    {
        (50_00_000m, 0.10m, 0.00m),
        (1_00_00_000m, 0.15m, 0.10m),
        (2_00_00_000m, 0.25m, 0.15m),
        (5_00_00_000m, 0.37m, 0.25m),
    };

    /// <summary>HRA exemption u/s 10(13A): least of (a) actual HRA, (b) rent - 10% of basic, (c) 50%/40% of basic.</summary>
    public static decimal HraExemption(decimal hraReceived, decimal basicSalary, decimal rentPaid, bool isMetro)
    {
        if (hraReceived < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hraReceived), "HRA received cannot be negative.");
        }
        if (basicSalary < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(basicSalary), "Basic salary cannot be negative.");
        }
        if (rentPaid < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rentPaid), "Rent paid cannot be negative.");
        }

        if (hraReceived == 0m || rentPaid == 0m)
        {
            return 0m;
        }

        decimal actualHra = hraReceived;
        decimal rentLessTenPercent = rentPaid - 0.10m * basicSalary;
        decimal percentOfBasic = (isMetro ? 0.50m : 0.40m) * basicSalary;

        decimal least = Math.Min(actualHra, Math.Min(rentLessTenPercent, percentOfBasic));
        return Round2(Math.Max(0m, least));
    }

    public static OldTaxResult Calculate(OldTaxInput input)
    {
        if (input.GrossSalary < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Gross salary cannot be negative.");
        }
        if (input.BasicSalary < 0 || input.HraReceived < 0 || input.RentPaid < 0 ||
            input.Section80C < 0 || input.Section80CCD1B < 0 || input.Section80D < 0 ||
            input.Section80TTA < 0 || input.HomeLoanInterest < 0 || input.ProfessionalTax < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "All inputs must be non-negative.");
        }

        decimal hraExemption = HraExemption(input.HraReceived, input.BasicSalary, input.RentPaid, input.IsMetro);

        decimal s80c = Math.Min(input.Section80C, Cap80C);
        decimal s80ccd = Math.Min(input.Section80CCD1B, Cap80CCD1B);
        decimal s80dCap = Cap80DBase + (input.SeniorCitizenParents ? Cap80DSeniorExtra : 0m);
        decimal s80d = Math.Min(input.Section80D, s80dCap);
        decimal s80tta = Math.Min(input.Section80TTA, Cap80TTA);
        decimal s24b = Math.Min(input.HomeLoanInterest, Cap24B);
        decimal profTax = input.ProfessionalTax;

        decimal totalDeductions = Round2(
            StandardDeduction + hraExemption + s80c + s80ccd + s80d + s80tta + s24b + profTax);

        decimal taxableIncome = Round2(Math.Max(0m, input.GrossSalary - totalDeductions));
        decimal taxBeforeRebate = Round2(SlabTax(taxableIncome));

        // 87A rebate (old regime): taxable income <= 5,00,000 -> rebate up to 12,500.
        decimal rebate = taxableIncome <= RebateLimit
            ? Round2(Math.Min(taxBeforeRebate, MaxRebate))
            : 0m;
        decimal taxAfterRebate = Round2(taxBeforeRebate - rebate);

        // Surcharge + statutory marginal relief at each threshold.
        (decimal threshold, decimal rate, decimal rateAtThreshold) = FindSurchargeTier(taxableIncome);
        decimal surcharge = Round2(taxAfterRebate * rate);

        decimal marginalRelief = 0m;
        if (rate > 0m)
        {
            // Cap: tax+surcharge at the threshold income, plus every rupee of income above it.
            decimal cap = Round2(SlabTax(threshold) * (1m + rateAtThreshold) + (taxableIncome - threshold));
            decimal actual = taxAfterRebate + surcharge;
            if (actual > cap)
            {
                marginalRelief = Round2(actual - cap);
            }
        }

        decimal taxPlusSurcharge = Round2(taxAfterRebate + surcharge - marginalRelief);
        decimal cess = Round2(taxPlusSurcharge * CessRate);
        decimal totalAnnualTax = Round2(taxPlusSurcharge + cess);
        decimal monthlyTds = Math.Round(totalAnnualTax / 12m, 0, MidpointRounding.AwayFromZero);

        return new OldTaxResult(
            GrossSalary: Round2(input.GrossSalary),
            StandardDeduction: StandardDeduction,
            HraExemption: hraExemption,
            HraMetro: input.IsMetro,
            Section80C: s80c,
            Section80CCD1B: s80ccd,
            Section80D: s80d,
            SeniorCitizenParents: input.SeniorCitizenParents,
            Section80TTA: s80tta,
            Section24B: s24b,
            ProfessionalTax: profTax,
            TotalDeductions: totalDeductions,
            TaxableIncome: taxableIncome,
            TaxBeforeRebate: taxBeforeRebate,
            Rebate: rebate,
            TaxAfterRebate: taxAfterRebate,
            SurchargeRate: rate,
            Surcharge: surcharge,
            MarginalRelief: marginalRelief,
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

    private static (decimal Threshold, decimal Rate, decimal RateAtThreshold) FindSurchargeTier(decimal income)
    {
        (decimal Threshold, decimal Rate, decimal RateAtThreshold) tier = default;
        foreach (var candidate in SurchargeTiers)
        {
            if (income > candidate.Threshold)
            {
                tier = candidate;
            }
        }
        return tier;
    }

    private static decimal Round2(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
