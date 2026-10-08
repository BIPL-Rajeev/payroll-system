using System.Globalization;
using TdsCalculator;

// ============================== Entry ==============================
if (args.Length > 0)
{
    return RunWithArgs(args);
}

return RunInteractive();

// ============================ Interactive ============================
static int RunInteractive()
{
    Console.WriteLine("=== Indian TDS Calculator - FY 2025-26 ===");
    Console.WriteLine("New regime : flat Rs 75,000 deduction only (no HRA / 80C / 80D).");
    Console.WriteLine("Old regime : Rs 50,000 deduction + HRA exemption + 80C / 80CCD(1B) / 80D / 80TTA / 24(b) + professional tax.");
    Console.WriteLine();

    TaxRegime regime = ReadRegime();

    if (regime == TaxRegime.New)
    {
        decimal gross = ReadNonNegative("Annual gross salary (CTC excluding employer PF) (Rs): ");
        decimal basic = ReadNonNegative("Annual basic salary (Rs): ");
        decimal hra = ReadNonNegative("Annual HRA received (Rs): ");
        decimal pf = ReadNonNegative("Annual employee PF contribution (Rs): ");
        decimal pt = ReadNonNegative("Professional tax paid (Rs): ");

        PrintNewRegime(TaxCalculator.Calculate(gross), basic, hra, pf, pt);
        return 0;
    }

    // Old regime and comparison share the same (richer) input set.
    OldTaxInput input = ReadOldRegimeInputs();

    if (regime == TaxRegime.Old)
    {
        PrintOldRegime(OldRegimeCalculator.Calculate(input));
        return 0;
    }

    PrintComparison(RegimeComparison.Compare(input));
    return 0;
}

// ============================= Args =============================
static int RunWithArgs(string[] args)
{
    string mode = args[0].ToLowerInvariant();

    if (mode == "new")
    {
        if (args.Length != 6)
        {
            return ArgsError("new regime expects 5 values after 'new'.");
        }

        if (!TryParseArg(args[1], "gross salary", out decimal gross) ||
            !TryParseArg(args[2], "basic salary", out decimal basic) ||
            !TryParseArg(args[3], "HRA received", out decimal hra) ||
            !TryParseArg(args[4], "employee PF", out decimal pf) ||
            !TryParseArg(args[5], "professional tax", out decimal pt))
        {
            return 1;
        }

        PrintNewRegime(TaxCalculator.Calculate(gross), basic, hra, pf, pt);
        return 0;
    }

    if (mode == "old" || mode == "compare" || mode == "comparison")
    {
        if (args.Length != 13)
        {
            return ArgsError($"'{mode}' expects 12 values after the keyword.");
        }

        if (!TryParseOldArgs(args, out OldTaxInput? input))
        {
            return 1;
        }

        if (mode == "old")
        {
            PrintOldRegime(OldRegimeCalculator.Calculate(input));
        }
        else
        {
            PrintComparison(RegimeComparison.Compare(input));
        }

        return 0;
    }

    // Legacy interface: 5 bare numbers -> New Regime.
    if (args.Length == 5)
    {
        if (!TryParseArg(args[0], "gross salary", out decimal gross) ||
            !TryParseArg(args[1], "basic salary", out decimal basic) ||
            !TryParseArg(args[2], "HRA received", out decimal hra) ||
            !TryParseArg(args[3], "employee PF", out decimal pf) ||
            !TryParseArg(args[4], "professional tax", out decimal pt))
        {
            return 1;
        }

        PrintNewRegime(TaxCalculator.Calculate(gross), basic, hra, pf, pt);
        return 0;
    }

    return ArgsError("unrecognised arguments.");
}

static int ArgsError(string message)
{
    Console.Error.WriteLine($"Error: {message}");
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  TdsCalculator new <gross> <basic> <hra> <emp-pf> <pro-tax>");
    Console.Error.WriteLine("  TdsCalculator old <gross> <basic> <hra> <rent> <metro 0|1> <80c> <80ccd1b> <80d> <senior 0|1> <80tta> <24b> <pro-tax>");
    Console.Error.WriteLine("  TdsCalculator compare <same 12 values as 'old'>");
    Console.Error.WriteLine("  TdsCalculator <gross> <basic> <hra> <emp-pf> <pro-tax>   (legacy: New Regime)");
    return 1;
}

static bool TryParseArg(string text, string label, out decimal value)
{
    if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value >= 0)
    {
        return true;
    }

    Console.Error.WriteLine($"Error: \"{label}\" must be a non-negative number (got \"{text}\").");
    value = 0m;
    return false;
}

static bool TryParseFlag(string text, string label, out bool flag)
{
    if (text is "0" or "false" or "n" or "no")
    {
        flag = false;
        return true;
    }
    if (text is "1" or "true" or "y" or "yes")
    {
        flag = true;
        return true;
    }

    Console.Error.WriteLine($"Error: \"{label}\" must be 0/1 (got \"{text}\").");
    flag = false;
    return false;
}

static bool TryParseOldArgs(string[] args, out OldTaxInput input)
{
    input = null!;

    if (!TryParseArg(args[1], "gross salary", out decimal gross) ||
        !TryParseArg(args[2], "basic salary", out decimal basic) ||
        !TryParseArg(args[3], "HRA received", out decimal hra) ||
        !TryParseArg(args[4], "rent paid", out decimal rent) ||
        !TryParseFlag(args[5], "metro flag", out bool metro) ||
        !TryParseArg(args[6], "section 80C", out decimal s80c) ||
        !TryParseArg(args[7], "section 80CCD(1B)", out decimal s80ccd) ||
        !TryParseArg(args[8], "section 80D", out decimal s80d) ||
        !TryParseFlag(args[9], "senior-citizen-parents flag", out bool senior) ||
        !TryParseArg(args[10], "section 80TTA", out decimal s80tta) ||
        !TryParseArg(args[11], "section 24(b)", out decimal s24b) ||
        !TryParseArg(args[12], "professional tax", out decimal pt))
    {
        return false;
    }

    input = new OldTaxInput(gross, basic, hra, rent, metro, s80c, s80ccd, s80d, senior, s80tta, s24b, pt);
    return true;
}

// ============================= Input =============================
static TaxRegime ReadRegime()
{
    while (true)
    {
        Console.Write("Which regime? [n] New   [o] Old   [c] Compare both: ");
        string? input = Console.ReadLine();

        if (input is null)
        {
            Console.Error.WriteLine("\nNo more input. Aborting.");
            Environment.Exit(1);
        }

        switch (input.Trim().ToLowerInvariant())
        {
            case "n" or "new":
                return TaxRegime.New;
            case "o" or "old":
                return TaxRegime.Old;
            case "c" or "compare" or "comparison":
                return TaxRegime.Compare;
        }

        Console.WriteLine("Invalid choice. Please enter n (New), o (Old) or c (Compare).");
    }
}

static OldTaxInput ReadOldRegimeInputs()
{
    Console.WriteLine();
    Console.WriteLine("--- Old regime inputs ---");

    decimal gross = ReadNonNegative("Annual gross salary (CTC excluding employer PF) (Rs): ");
    decimal basic = ReadNonNegative("Annual basic salary (Rs): ");
    decimal hra = ReadNonNegative("Annual HRA received (Rs): ");
    decimal rent = ReadNonNegative("Annual rent paid (Rs): ");
    bool metro = ReadYesNo("Is the city a metro? (y/n): ");
    bool senior = ReadYesNo("Senior citizen parents? (y/n): ");
    decimal s80c = ReadNonNegative("Section 80C investments incl. employee PF (max 1,50,000) (Rs): ");
    decimal s80ccd = ReadNonNegative("Section 80CCD(1B) NPS self-contribution (max 50,000) (Rs): ");
    decimal s80d = ReadNonNegative("Section 80D health insurance (max 25,000, or 75,000 with senior parents) (Rs): ");
    decimal s80tta = ReadNonNegative("Section 80TTA savings bank interest (max 10,000) (Rs): ");
    decimal s24b = ReadNonNegative("Section 24(b) self-occupied home loan interest (max 2,00,000) (Rs): ");
    decimal pt = ReadNonNegative("Professional tax paid (Rs): ");

    return new OldTaxInput(gross, basic, hra, rent, metro, s80c, s80ccd, s80d, senior, s80tta, s24b, pt);
}

static decimal ReadNonNegative(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        string? input = Console.ReadLine();

        if (input is null)
        {
            // End of input stream (Ctrl+D / piped stdin exhausted) - stop instead of looping forever.
            Console.Error.WriteLine("\nNo more input. Aborting.");
            Environment.Exit(1);
        }

        if (TryParseNonNegative(input, out decimal value))
        {
            return value;
        }

        Console.WriteLine("Invalid input. Please enter a non-negative number.");
    }
}

static bool ReadYesNo(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        string? input = Console.ReadLine();

        if (input is null)
        {
            Console.Error.WriteLine("\nNo more input. Aborting.");
            Environment.Exit(1);
        }

        switch (input.Trim().ToLowerInvariant())
        {
            case "y" or "yes" or "1":
                return true;
            case "n" or "no" or "0":
                return false;
        }

        Console.WriteLine("Invalid input. Please enter y or n.");
    }
}

static bool TryParseNonNegative(string? text, out decimal value)
{
    return decimal.TryParse(
               text,
               NumberStyles.Number,
               CultureInfo.InvariantCulture,
               out value)
           && value >= 0;
}

// ============================= Output =============================
static void PrintNewRegime(TaxResult r, decimal basic, decimal hra, decimal pf, decimal pt)
{
    Console.WriteLine();
    Console.WriteLine("================ TDS Breakdown (NEW Regime, FY 2025-26) ================");
    Console.WriteLine($"Regime used                               : {"NEW",14}");
    Console.WriteLine($"Annual Gross Salary (CTC excl. employer PF) : {r.GrossSalary,14:N2}");
    Console.WriteLine("---------- Reference figures (not deducted under NEW regime) ----------");
    Console.WriteLine($"  Basic Salary                            : {basic,14:N2}");
    Console.WriteLine($"  HRA Received (no exemption allowed)     : {hra,14:N2}");
    Console.WriteLine($"  Employee PF                             : {pf,14:N2}");
    Console.WriteLine($"  Professional Tax (not deductible)       : {pt,14:N2}");
    Console.WriteLine("---------- Deductions applied ----------");
    Console.WriteLine($"Less: Standard Deduction (flat)           : {-TaxCalculator.StandardDeduction,14:N2}");
    Console.WriteLine($"Total Deductions                          : {-TaxCalculator.StandardDeduction,14:N2}");
    Console.WriteLine($"Taxable Income                           : {r.TaxableIncome,14:N2}");
    Console.WriteLine("---------- Tax ----------");
    Console.WriteLine($"Tax before rebate                        : {r.TaxBeforeRebate,14:N2}");
    Console.WriteLine($"Less: Section 87A rebate                 : {-r.Rebate,14:N2}");
    Console.WriteLine($"Tax after rebate                         : {r.TaxAfterRebate,14:N2}");
    if (r.MarginalRelief > 0)
    {
        Console.WriteLine($"Less: Marginal relief (sec. 87A)         : {-r.MarginalRelief,14:N2}");
        Console.WriteLine($"Tax after marginal relief                : {r.TaxAfterRelief,14:N2}");
    }
    Console.WriteLine($"Add: Surcharge                           : {r.Surcharge,14:N2}");
    Console.WriteLine($"Add: Health & Education Cess (4%)        : {r.Cess,14:N2}");
    Console.WriteLine("-----------------------------------------");
    Console.WriteLine($"Total Annual Tax                         : {r.TotalAnnualTax,14:N2}");
    Console.WriteLine($"Monthly TDS (annual / 12, nearest Rs)    : {r.MonthlyTds,14:N0}");
    Console.WriteLine("========================================================================");
}

static void PrintOldRegime(OldTaxResult r)
{
    Console.WriteLine();
    Console.WriteLine("================ TDS Breakdown (OLD Regime, FY 2025-26) ================");
    Console.WriteLine($"Regime used                               : {"OLD",14}");
    Console.WriteLine($"Annual Gross Salary                       : {r.GrossSalary,14:N2}");
    Console.WriteLine("---------- Deductions applied ----------");
    Console.WriteLine($"Less: Standard Deduction (flat)           : {-r.StandardDeduction,14:N2}");
    Console.WriteLine($"Less: HRA exemption u/s 10(13A) {HraTag(r.HraMetro)}   : {-r.HraExemption,14:N2}");
    Console.WriteLine($"Less: Section 80C (max 1,50,000)          : {-r.Section80C,14:N2}");
    Console.WriteLine($"Less: Section 80CCD(1B) (max 50,000)      : {-r.Section80CCD1B,14:N2}");
    Console.WriteLine($"Less: Section 80D (max {D80Cap(r),8})        : {-r.Section80D,14:N2}");
    Console.WriteLine($"Less: Section 80TTA (max 10,000)          : {-r.Section80TTA,14:N2}");
    Console.WriteLine($"Less: Section 24(b) (max 2,00,000)        : {-r.Section24B,14:N2}");
    Console.WriteLine($"Less: Professional Tax                    : {-r.ProfessionalTax,14:N2}");
    Console.WriteLine($"Total Deductions                          : {-r.TotalDeductions,14:N2}");
    Console.WriteLine($"Taxable Income                           : {r.TaxableIncome,14:N2}");
    Console.WriteLine("---------- Tax ----------");
    Console.WriteLine($"Tax before rebate                        : {r.TaxBeforeRebate,14:N2}");
    Console.WriteLine($"Less: Section 87A rebate (<= 5,00,000)    : {-r.Rebate,14:N2}");
    Console.WriteLine($"Tax after rebate                         : {r.TaxAfterRebate,14:N2}");
    if (r.Surcharge > 0 || r.MarginalRelief > 0)
    {
        Console.WriteLine($"Add: Surcharge ({r.SurchargeRate * 100:0.##}%)                 : {r.Surcharge,14:N2}");
    }
    else
    {
        Console.WriteLine($"Add: Surcharge                           : {r.Surcharge,14:N2}");
    }
    if (r.MarginalRelief > 0)
    {
        Console.WriteLine($"Less: Marginal relief (surcharge)        : {-r.MarginalRelief,14:N2}");
    }
    Console.WriteLine($"Add: Health & Education Cess (4%)        : {r.Cess,14:N2}");
    Console.WriteLine("-----------------------------------------");
    Console.WriteLine($"Total Annual Tax                         : {r.TotalAnnualTax,14:N2}");
    Console.WriteLine($"Monthly TDS (annual / 12, nearest Rs)    : {r.MonthlyTds,14:N0}");
    Console.WriteLine("========================================================================");
}

static void PrintComparison(RegimeComparisonResult c)
{
    TaxResult n = c.NewRegime;
    OldTaxResult o = c.OldRegime;

    Console.WriteLine();
    Console.WriteLine("=================== REGIME COMPARISON (FY 2025-26) ===================");
    Console.WriteLine($"{"",-30}{"New Regime",16}{"Old Regime",16}");
    Console.WriteLine($"{"Gross Salary",-30}{n.GrossSalary,16:N2}{o.GrossSalary,16:N2}");
    Console.WriteLine($"{"Total Deductions",-30}{-TaxCalculator.StandardDeduction,16:N2}{-o.TotalDeductions,16:N2}");
    Console.WriteLine($"{"Taxable Income",-30}{n.TaxableIncome,16:N2}{o.TaxableIncome,16:N2}");
    Console.WriteLine($"{"Tax before rebate",-30}{n.TaxBeforeRebate,16:N2}{o.TaxBeforeRebate,16:N2}");
    Console.WriteLine($"{"Rebate",-30}{-n.Rebate,16:N2}{-o.Rebate,16:N2}");
    Console.WriteLine($"{"Surcharge",-30}{n.Surcharge,16:N2}{o.Surcharge,16:N2}");
    Console.WriteLine($"{"Cess (4%)",-30}{n.Cess,16:N2}{o.Cess,16:N2}");
    Console.WriteLine($"{"Total Annual Tax",-30}{n.TotalAnnualTax,16:N2}{o.TotalAnnualTax,16:N2}");
    Console.WriteLine($"{"Monthly TDS",-30}{n.MonthlyTds,16:N0}{o.MonthlyTds,16:N0}");
    Console.WriteLine("---------------------------------------------------------------------");

    switch (c.Better)
    {
        case BetterRegime.New:
            Console.WriteLine($"Better: NEW REGIME - saves Rs {c.TaxSaving:N2} per year (Rs {MonthlySaving(c.TaxSaving):N0} per month)");
            break;
        case BetterRegime.Old:
            Console.WriteLine($"Better: OLD REGIME - saves Rs {c.TaxSaving:N2} per year (Rs {MonthlySaving(c.TaxSaving):N0} per month)");
            break;
        default:
            Console.WriteLine("Both regimes cost exactly the same.");
            break;
    }

    Console.WriteLine("======================================================================");
}

static decimal MonthlySaving(decimal annualSaving) =>
    Math.Round(annualSaving / 12m, 0, MidpointRounding.AwayFromZero);

static string HraTag(bool metro) => metro ? "(metro)" : "(non-metro)";

static string D80Cap(OldTaxResult r) =>
    r.SeniorCitizenParents ? "75,000" : "25,000";
