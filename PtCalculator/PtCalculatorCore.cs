namespace PtCalculator;

/// <summary>Indian states with Professional Tax slabs supported by this calculator.</summary>
public enum PtState
{
    Maharashtra,
    Karnataka,
    TamilNadu,
}

/// <summary>Result of a Professional Tax computation for one employee-month.</summary>
/// <param name="StateName">Display name of the state (e.g. "Tamil Nadu").</param>
/// <param name="MonthlyGrossSalary">Monthly gross salary the PT was computed on.</param>
/// <param name="Month">Month number the monthly PT applies to (1-12).</param>
/// <param name="MonthName">Month name (e.g. "February").</param>
/// <param name="MonthlyPt">PT deducted for the given month.</param>
/// <param name="AnnualPt">Sum of PT over all 12 months of the financial year.</param>
/// <param name="IsFebruaryHigherSlab">True when Maharashtra's Rs 300 February slab applied.</param>
public sealed record PtResult(
    string StateName,
    PtState State,
    decimal MonthlyGrossSalary,
    int Month,
    string MonthName,
    decimal MonthlyPt,
    decimal AnnualPt,
    bool IsFebruaryHigherSlab)
{
    /// <summary>February's higher slab amount for Maharashtra (replaces Rs 200).</summary>
    public const decimal MaharashtraFebruaryPt = 300m;
}

/// <summary>
/// Indian Professional Tax (PT) computation for FY 2025-26.
/// State-wise monthly slabs; Maharashtra charges Rs 300 (instead of Rs 200)
/// in February for salaries above Rs 10,000.
/// </summary>
public static class PtCalculatorCore
{
    public const int FebruaryMonth = 2;
    public const int MonthsInYear = 12;

    // Maharashtra monthly slabs.
    public const decimal MaharashtraNilUpTo = 7_500m;
    public const decimal MaharashtraLowerSlab = 175m;   // 7,501 - 10,000
    public const decimal MaharashtraUpperSlab = 200m;   // above 10,000 (300 in February)
    public const decimal MaharashtraUpperThreshold = 10_000m;

    // Karnataka monthly slabs.
    public const decimal KarnatakaNilUpTo = 15_000m;
    public const decimal KarnatakaLowerSlab = 150m;     // 15,001 - 20,000
    public const decimal KarnatakaUpperSlab = 200m;     // above 20,000
    public const decimal KarnatakaUpperThreshold = 20_000m;

    // Tamil Nadu half-yearly slabs, deducted monthly (applied to the monthly salary).
    public const decimal TamilNaduNilUpTo = 21_000m;
    public const decimal TamilNaduSlab1 = 135m;         // 21,001 - 30,000
    public const decimal TamilNaduSlab2 = 315m;         // 30,001 - 45,000
    public const decimal TamilNaduSlab3 = 690m;         // 45,001 - 60,000
    public const decimal TamilNaduSlab4 = 1_025m;       // 60,001 - 75,000
    public const decimal TamilNaduSlab5 = 1_250m;       // above 75,000

    private static readonly string[] MonthNames =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    };

    public static string SupportedStates => "Maharashtra, Karnataka, Tamil Nadu";

    public static string StateDisplayName(PtState state) => state switch
    {
        PtState.Maharashtra => "Maharashtra",
        PtState.Karnataka => "Karnataka",
        PtState.TamilNadu => "Tamil Nadu",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown state."),
    };

    /// <summary>Parses a state name (case-insensitive; full name or common 2-letter code).</summary>
    public static bool TryParseState(string text, out PtState state)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "maharashtra" or "mh":
                state = PtState.Maharashtra;
                return true;
            case "karnataka" or "ka":
                state = PtState.Karnataka;
                return true;
            case "tamil nadu" or "tamilnadu" or "tn":
                state = PtState.TamilNadu;
                return true;
            default:
                state = default;
                return false;
        }
    }

    /// <summary>
    /// Computes Professional Tax for one month plus the annual total.
    /// </summary>
    /// <param name="monthlyGrossSalary">Monthly gross salary.</param>
    /// <param name="state">State whose PT slabs apply.</param>
    /// <param name="month">Month number 1-12 (February triggers Maharashtra's higher slab).</param>
    public static PtResult Calculate(decimal monthlyGrossSalary, PtState state, int month)
    {
        if (monthlyGrossSalary < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyGrossSalary), "Gross salary cannot be negative.");
        }
        if (month < 1 || month > MonthsInYear)
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be between 1 and 12.");
        }

        decimal monthlyPt = MonthlyPt(monthlyGrossSalary, state, month);
        decimal annualPt = AnnualPt(monthlyGrossSalary, state);

        return new PtResult(
            StateName: StateDisplayName(state),
            State: state,
            MonthlyGrossSalary: monthlyGrossSalary,
            Month: month,
            MonthName: MonthNames[month - 1],
            MonthlyPt: monthlyPt,
            AnnualPt: annualPt,
            IsFebruaryHigherSlab: state == PtState.Maharashtra && month == FebruaryMonth && monthlyPt > 0);
    }

    /// <summary>PT deducted for a single month under the state's slabs.</summary>
    public static decimal MonthlyPt(decimal monthlyGrossSalary, PtState state, int month) => state switch
    {
        PtState.Maharashtra => monthlyGrossSalary <= MaharashtraNilUpTo
            ? 0m
            : monthlyGrossSalary <= MaharashtraUpperThreshold
                ? MaharashtraLowerSlab
                : month == FebruaryMonth ? PtResult.MaharashtraFebruaryPt : MaharashtraUpperSlab,

        PtState.Karnataka => monthlyGrossSalary <= KarnatakaNilUpTo
            ? 0m
            : monthlyGrossSalary <= KarnatakaUpperThreshold
                ? KarnatakaLowerSlab
                : KarnatakaUpperSlab,

        PtState.TamilNadu => monthlyGrossSalary <= TamilNaduNilUpTo
            ? 0m
            : monthlyGrossSalary <= 30_000m ? TamilNaduSlab1
            : monthlyGrossSalary <= 45_000m ? TamilNaduSlab2
            : monthlyGrossSalary <= 60_000m ? TamilNaduSlab3
            : monthlyGrossSalary <= 75_000m ? TamilNaduSlab4
            : TamilNaduSlab5,

        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown state."),
    };

    /// <summary>Sum of PT over all 12 months (includes Maharashtra's February higher slab).</summary>
    public static decimal AnnualPt(decimal monthlyGrossSalary, PtState state)
    {
        decimal total = 0m;
        for (int month = 1; month <= MonthsInYear; month++)
        {
            total += MonthlyPt(monthlyGrossSalary, state, month);
        }
        return total;
    }
}
