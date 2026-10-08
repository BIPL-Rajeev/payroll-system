using System.Globalization;
using EsiCalculator;

// ============================== Entry ==============================
if (args.Length > 0)
{
    return RunWithArgs(args);
}

return RunInteractive();

// ============================ Interactive ============================
static int RunInteractive()
{
    Console.WriteLine("=== Indian ESI Calculator - FY 2025-26 ===");
    Console.WriteLine($"Applicable when monthly gross salary <= Rs {EsiResult.WageCeiling:N0}.");
    Console.WriteLine("Rates: employee 0.75% | employer 3.25% | total 4.00%.");
    Console.WriteLine();

    decimal gross = ReadNonNegative("Monthly gross salary (incl. allowances, excl. annual bonus) (Rs): ");
    bool isDisabled = ReadYesNo("Person with disability? (employee share waived) (y/n): ");
    bool isFirstYear = ReadYesNo("First-time employee (2-year employee-share exemption)? (y/n): ");

    PrintResult(EsiCalculatorCore.Calculate(gross, isDisabled, isFirstYear));
    return 0;
}

// ============================= Args =============================
static int RunWithArgs(string[] args)
{
    // Valid forms:
    //   EsiCalculator <gross>
    //   EsiCalculator <gross> <disabled 0|1> <first-year-exempt 0|1>
    if (args.Length != 1 && args.Length != 3)
    {
        Console.Error.WriteLine("Error: expected 1 argument (gross salary) or 3 arguments (gross, disabled flag, first-year-exempt flag).");
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  EsiCalculator <monthly-gross-salary>");
        Console.Error.WriteLine("  EsiCalculator <monthly-gross-salary> <disabled 0|1> <first-year-exempt 0|1>");
        return 1;
    }

    if (!TryParseNonNegative(args[0], "gross salary", out decimal gross))
    {
        return 1;
    }

    bool isDisabled = false;
    bool isFirstYear = false;

    if (args.Length == 3)
    {
        if (!TryParseFlag(args[1], "disabled flag", out isDisabled) ||
            !TryParseFlag(args[2], "first-year-exempt flag", out isFirstYear))
        {
            return 1;
        }
    }

    PrintResult(EsiCalculatorCore.Calculate(gross, isDisabled, isFirstYear));
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

// ============================= Output =============================
static void PrintResult(EsiResult r)
{
    Console.WriteLine();

    if (!r.IsApplicable)
    {
        Console.WriteLine("---------------- ESI Summary ----------------");
        Console.WriteLine($"Monthly Gross Salary      : {r.MonthlyGrossSalary,12:N2}");
        Console.WriteLine($"Wage Ceiling              : {EsiResult.WageCeiling,12:N0}");
        Console.WriteLine("ESI not applicable (salary exceeds Rs 21,000 per month).");
        Console.WriteLine("----------------------------------------------");
        return;
    }

    Console.WriteLine("---------------- ESI Summary ----------------");
    Console.WriteLine($"Applicability             : APPLICABLE");
    Console.WriteLine($"Monthly Gross Salary      : {r.MonthlyGrossSalary,12:N2}");
    if (r.IsDisabled)
    {
        Console.WriteLine("Person with disability    : yes (employee share waived)");
    }
    if (r.IsFirstYearExempt)
    {
        Console.WriteLine("First-time employee       : yes (2-year employee-share exemption)");
    }
    Console.WriteLine("---------- Monthly Breakdown ----------");
    Console.WriteLine($"Employee contribution ({Rate(r.EmployeeRate),5})  : {r.EmployeeContribution,12:N0}");
    Console.WriteLine($"Employer contribution ({Rate(r.EmployerRate),5})  : {r.EmployerContribution,12:N0}");
    Console.WriteLine($"Total ESI (4.00%)        : {r.TotalEsi,12:N0}");
    Console.WriteLine("----------------------------------------------");
}

static string Rate(decimal rate) => $"{rate * 100m:0.##}%";
