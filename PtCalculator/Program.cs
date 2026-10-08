using System.Globalization;
using PtCalculator;

// ============================== Entry ==============================
if (args.Length > 0)
{
    return RunWithArgs(args);
}

return RunInteractive();

// ============================ Interactive ============================
static int RunInteractive()
{
    Console.WriteLine("=== Indian Professional Tax (PT) Calculator - FY 2025-26 ===");
    Console.WriteLine($"Supported states: {PtCalculatorCore.SupportedStates}");
    Console.WriteLine("Maharashtra: <= 7,500 nil | 7,501-10,000 Rs 175 | > 10,000 Rs 200 (Rs 300 in February).");
    Console.WriteLine("Karnataka  : <= 15,000 nil | 15,001-20,000 Rs 150 | > 20,000 Rs 200.");
    Console.WriteLine("Tamil Nadu : half-yearly slabs deducted monthly (21,000 / 30,000 / 45,000 / 60,000 / 75,000).");
    Console.WriteLine();

    decimal gross = ReadNonNegative("Monthly gross salary (Rs): ");
    PtState state = ReadState("State name: ");
    int month = ReadMonth($"Month number (1-12, Enter = current month {DateTime.Now.Month}): ");

    PrintResult(PtCalculatorCore.Calculate(gross, state, month));
    return 0;
}

// ============================= Args =============================
static int RunWithArgs(string[] args)
{
    // Valid forms:
    //   PtCalculator <gross> <state>
    //   PtCalculator <gross> <state> <month 1-12>
    if (args.Length != 2 && args.Length != 3)
    {
        Console.Error.WriteLine("Error: expected 2 arguments (gross salary, state) or 3 arguments (gross salary, state, month).");
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  PtCalculator <monthly-gross-salary> <state> [month 1-12]");
        Console.Error.WriteLine($"Supported states: {PtCalculatorCore.SupportedStates}");
        return 1;
    }

    if (!TryParseNonNegative(args[0], "gross salary", out decimal gross))
    {
        return 1;
    }

    if (!PtCalculatorCore.TryParseState(args[1], out PtState state))
    {
        Console.Error.WriteLine($"Error: unsupported state \"{args[1]}\".");
        Console.Error.WriteLine($"Supported states: {PtCalculatorCore.SupportedStates}");
        return 1;
    }

    int month = DateTime.Now.Month;
    if (args.Length == 3 && !TryParseMonth(args[2], "month", out month))
    {
        return 1;
    }

    PrintResult(PtCalculatorCore.Calculate(gross, state, month));
    return 0;
}

static bool TryParseNonNegative(string text, string label, out decimal value)
{
    if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value >= 0)
    {
        return true;
    }

    Console.Error.WriteLine($"Error: \"{label}\" must be a non-negative number (got \"{text}\").");
    value = 0m;
    return false;
}

static bool TryParseMonth(string text, string label, out int month)
{
    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out month) &&
        month >= 1 && month <= PtCalculatorCore.MonthsInYear)
    {
        return true;
    }

    Console.Error.WriteLine($"Error: \"{label}\" must be a number between 1 and 12 (got \"{text}\").");
    month = 1;
    return false;
}

// ============================= Input =============================
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

        if (decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) && value >= 0)
        {
            return value;
        }

        Console.WriteLine("Invalid input. Please enter a non-negative number.");
    }
}

static PtState ReadState(string prompt)
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

        if (PtCalculatorCore.TryParseState(input, out PtState state))
        {
            return state;
        }

        Console.WriteLine($"Invalid state. Supported states: {PtCalculatorCore.SupportedStates}");
    }
}

static int ReadMonth(string prompt)
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

        if (input.Trim().Length == 0)
        {
            return DateTime.Now.Month;
        }

        if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int month) &&
            month >= 1 && month <= PtCalculatorCore.MonthsInYear)
        {
            return month;
        }

        Console.WriteLine("Invalid input. Please enter a number between 1 and 12.");
    }
}

// ============================= Output =============================
static void PrintResult(PtResult r)
{
    Console.WriteLine();
    Console.WriteLine("---------------- PT Summary ----------------");
    Console.WriteLine($"State                     : {r.StateName}");
    Console.WriteLine($"Monthly Gross Salary      : {r.MonthlyGrossSalary,12:N2}");
    Console.WriteLine($"Month                     : {r.MonthName}");
    Console.WriteLine($"PT for the month          : {r.MonthlyPt,12:N0}");
    if (r.IsFebruaryHigherSlab)
    {
        Console.WriteLine($"February higher slab      : {PtResult.MaharashtraFebruaryPt,12:N0} (February deduction is Rs {PtResult.MaharashtraFebruaryPt:N0} instead of Rs {PtCalculatorCore.MaharashtraUpperSlab:N0})");
    }
    Console.WriteLine($"Annual PT Total (12 mo)   : {r.AnnualPt,12:N0}");
    Console.WriteLine("--------------------------------------------");
}
