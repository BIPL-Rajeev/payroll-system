namespace TdsCalculator;

public enum TaxRegime
{
    New,
    Old,
    Compare,
}

public enum BetterRegime
{
    New,
    Old,
    Tie,
}

/// <summary>Result of computing the same income under both regimes.</summary>
public sealed record RegimeComparisonResult(
    TaxResult NewRegime,
    OldTaxResult OldRegime,
    BetterRegime Better,
    decimal TaxSaving);

/// <summary>Computes tax under both regimes for one gross salary and picks the cheaper one.</summary>
public static class RegimeComparison
{
    public static RegimeComparisonResult Compare(OldTaxInput input)
    {
        TaxResult newRegime = TaxCalculator.Calculate(input.GrossSalary);
        OldTaxResult oldRegime = OldRegimeCalculator.Calculate(input);

        (BetterRegime better, decimal saving) = newRegime.TotalAnnualTax switch
        {
            var n when n < oldRegime.TotalAnnualTax => (BetterRegime.New, oldRegime.TotalAnnualTax - n),
            var n when n > oldRegime.TotalAnnualTax => (BetterRegime.Old, n - oldRegime.TotalAnnualTax),
            _ => (BetterRegime.Tie, 0m),
        };

        return new RegimeComparisonResult(newRegime, oldRegime, better, Math.Round(saving, 2));
    }
}
