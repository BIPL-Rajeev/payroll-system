using System.Globalization;
using PayrollCalculator;
using EsiCalculator;
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
    Console.WriteLine("=== Indian Payroll Calculator - Monthly Salary Slip (FY 2025-26) ===");
    Console.WriteLine("Combines PF + ESI + Professional Tax + TDS into one salary slip.");
    Console.WriteLine();

    Console.Write("Employee ID: ");
    string? idInput = Console.ReadLine();
    if (idInput is null)
    {
        Console.Error.WriteLine("\nNo more input. Aborting.");
        Environment.Exit(1);
    }
    string employeeId = idInput.Trim();

    Console.Write("Employee name: ");
    string? nameInput = Console.ReadLine();
    if (nameInput is null)
    {
        Console.Error.WriteLine("\nNo more input. Aborting.");
        Environment.Exit(1);
    }
    string name = nameInput.Trim();

    Console.WriteLine($"Supported states: {PtCalculatorCore.SupportedStates}");
    PtState state = ReadState("State name: ");
    bool isMetro = ReadYesNo("Metro city? (for HRA exemption) (y/n): ");
    TaxRegime regime = ReadRegime("Tax regime - old or new: ");

    Console.WriteLine();
    Console.WriteLine("--- Monthly salary components (Rs) ---");
    decimal basic = ReadNonNegative("Basic: ");
    decimal hra = ReadNonNegative("HRA: ");
    decimal da = ReadNonNegative("DA: ");
    decimal special = ReadNonNegative("Special allowance: ");
    decimal lta = ReadNonNegative("LTA: ");
    decimal other = ReadNonNegative("Other allowances: ");

    Console.WriteLine();
    Console.WriteLine("--- Annual rent & old-regime investments (Rs) ---");
    decimal rent = ReadNonNegative("Annual rent paid: ");
    decimal s80c = ReadNonNegative("80C investments (annual): ");
    decimal s80ccd = ReadNonNegative("80CCD(1B) NPS (annual): ");
    decimal s80d = ReadNonNegative("80D medical insurance (annual): ");
    decimal s80tta = ReadNonNegative("80TTA savings interest (annual): ");
    decimal homeLoan = ReadNonNegative("Home loan interest 24(b) (annual): ");

    bool isFirstYear = ReadYesNo("First-year employee (ESI employee-share exemption)? (y/n): ");
    bool hasDisability = ReadYesNo("Person with disability (ESI employee share waived)? (y/n): ");
    int month = ReadMonth($"Month number (1-12, Enter = current month {DateTime.Now.Month}): ");

    Employee employee = new(
        EmployeeId: employeeId,
        Name: name,
        State: state,
        IsMetro: isMetro,
        TaxRegime: regime,
        MonthlyBasic: basic,
        MonthlyHra: hra,
        MonthlyDa: da,
        MonthlySpecialAllowance: special,
        MonthlyLta: lta,
        MonthlyOtherAllowances: other,
        AnnualRentPaid: rent,
        Section80C: s80c,
        Section80CCD1B: s80ccd,
        Section80D: s80d,
        Section80TTA: s80tta,
        HomeLoanInterest: homeLoan,
        IsFirstYearEmployee: isFirstYear,
        HasDisability: hasDisability,
        MonthNumber: month);

    PrintSlip(PayrollCalculatorCore.Calculate(employee));
    return 0;
}

// ============================= Args =============================
static int RunWithArgs(string[] args)
{
    // Valid form (20 arguments):
    //   PayrollCalculator <id> <name> <state> <metro 0|1> <regime old|new>
    //     <basic> <hra> <da> <special> <lta> <other> <annual-rent>
    //     <80c> <80ccd1b> <80d> <80tta> <home-loan-interest>
    //     <first-year 0|1> <disabled 0|1> <month 1-12>
    if (args.Length != 20)
    {
        Console.Error.WriteLine("Error: expected 20 arguments.");
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  PayrollCalculator <id> <name> <state> <metro 0|1> <regime old|new> <basic> <hra> <da> <special> <lta> <other> <annual-rent> <80c> <80ccd1b> <80d> <80tta> <home-loan-interest> <first-year 0|1> <disabled 0|1> <month 1-12>");
        Console.Error.WriteLine($"Supported states: {PtCalculatorCore.SupportedStates}");
        return 1;
    }

    string employeeId = args[0];
    string name = args[1];

    if (!PtCalculatorCore.TryParseState(args[2], out PtState state))
    {
        Console.Error.WriteLine($"Error: unsupported state \"{args[2]}\".");
        Console.Error.WriteLine($"Supported states: {PtCalculatorCore.SupportedStates}");
        return 1;
    }

    if (!TryParseFlag(args[3], "metro flag", out bool isMetro) ||
        !TryParseRegime(args[4], out TaxRegime regime) ||
        !TryParseNonNegative(args[5], "basic", out decimal basic) ||
        !TryParseNonNegative(args[6], "HRA", out decimal hra) ||
        !TryParseNonNegative(args[7], "DA", out decimal da) ||
        !TryParseNonNegative(args[8], "special allowance", out decimal special) ||
        !TryParseNonNegative(args[9], "LTA", out decimal lta) ||
        !TryParseNonNegative(args[10], "other allowances", out decimal other) ||
        !TryParseNonNegative(args[11], "annual rent", out decimal rent) ||
        !TryParseNonNegative(args[12], "80C", out decimal s80c) ||
        !TryParseNonNegative(args[13], "80CCD(1B)", out decimal s80ccd) ||
        !TryParseNonNegative(args[14], "80D", out decimal s80d) ||
        !TryParseNonNegative(args[15], "80TTA", out decimal s80tta) ||
        !TryParseNonNegative(args[16], "home loan interest", out decimal homeLoan) ||
        !TryParseFlag(args[17], "first-year flag", out bool isFirstYear) ||
        !TryParseFlag(args[18], "disability flag", out bool hasDisability))
    {
        return 1;
    }

    if (!int.TryParse(args[19], NumberStyles.Integer, CultureInfo.InvariantCulture, out int month) ||
        month < 1 || month > PtCalculatorCore.MonthsInYear)
    {
        Console.Error.WriteLine($"Error: \"month\" must be a number between 1 and 12 (got \"{args[19]}\").");
        return 1;
    }

    Employee employee = new(
        EmployeeId: employeeId,
        Name: name,
        State: state,
        IsMetro: isMetro,
        TaxRegime: regime,
        MonthlyBasic: basic,
        MonthlyHra: hra,
        MonthlyDa: da,
        MonthlySpecialAllowance: special,
        MonthlyLta: lta,
        MonthlyOtherAllowances: other,
        AnnualRentPaid: rent,
        Section80C: s80c,
        Section80CCD1B: s80ccd,
        Section80D: s80d,
        Section80TTA: s80tta,
        HomeLoanInterest: homeLoan,
        IsFirstYearEmployee: isFirstYear,
        HasDisability: hasDisability,
        MonthNumber: month);

    PrintSlip(PayrollCalculatorCore.Calculate(employee));
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

static bool TryParseRegime(string text, out TaxRegime regime)
{
    switch (text.Trim().ToLowerInvariant())
    {
        case "old" or "o":
            regime = TaxRegime.Old;
            return true;
        case "new" or "n":
            regime = TaxRegime.New;
            return true;
        default:
            Console.Error.WriteLine($"Error: \"regime\" must be old or new (got \"{text}\").");
            regime = TaxRegime.New;
            return false;
    }
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

static TaxRegime ReadRegime(string prompt)
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

        if (TryParseRegime(input, out TaxRegime regime))
        {
            return regime;
        }

        Console.WriteLine("Invalid input. Please enter old or new.");
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
static void PrintSlip(PayrollSlip s)
{
    string regimeLabel = s.TaxRegime == TaxRegime.Old ? "Old" : "New";
    Console.WriteLine();
    Console.WriteLine("================================ SALARY SLIP ================================");
    Console.WriteLine($"Employee ID    : {s.EmployeeId}");
    Console.WriteLine($"Name           : {s.Name}");
    Console.WriteLine($"State          : {s.StateName}");
    Console.WriteLine($"Tax Regime     : {regimeLabel}");
    Console.WriteLine($"Month          : {s.MonthName}");
    Console.WriteLine("-----------------------------------------------------------------------------");
    Console.WriteLine("EARNINGS                                                       Amount (Rs)");
    Console.WriteLine("-----------------------------------------------------------------------------");
    Console.WriteLine($"Basic Salary                                                   {s.Basic,14:N2}");
    Console.WriteLine($"House Rent Allowance                                           {s.Hra,14:N2}");
    Console.WriteLine($"Dearness Allowance                                             {s.Da,14:N2}");
    Console.WriteLine($"Special Allowance                                              {s.SpecialAllowance,14:N2}");
    Console.WriteLine($"Leave Travel Allowance                                         {s.Lta,14:N2}");
    Console.WriteLine($"Other Allowances                                               {s.OtherAllowances,14:N2}");
    Console.WriteLine($"Gross Monthly Salary                                           {s.GrossMonthlySalary,14:N2}");
    Console.WriteLine("-----------------------------------------------------------------------------");
    Console.WriteLine("DEDUCTIONS                                                     Amount (Rs)");
    Console.WriteLine("-----------------------------------------------------------------------------");
    Console.WriteLine($"Employee PF (12%)                                              {s.EmployeePf,14:N2}");
    Console.WriteLine(FormatEsiLine(s));
    Console.WriteLine($"Professional Tax                                               {s.ProfessionalTax,14:N2}");
    Console.WriteLine($"TDS (income tax, {regimeLabel.ToLowerInvariant()} regime, monthly)                          {s.Tds,14:N2}");
    Console.WriteLine($"Total Deductions                                               {s.TotalDeductions,14:N2}");
    Console.WriteLine("-----------------------------------------------------------------------------");
    Console.WriteLine("EMPLOYER CONTRIBUTIONS (cost to company)                       Amount (Rs)");
    Console.WriteLine("-----------------------------------------------------------------------------");
    Console.WriteLine($"Employer EPS (8.33%)                                           {s.EmployerEps,14:N2}");
    Console.WriteLine($"Employer EPF (3.67%)                                           {s.EmployerEpf,14:N2}");
    Console.WriteLine(FormatEmployerEsiLine(s));
    Console.WriteLine($"EDLI (0.50%)                                                   {s.Edli,14:N2}");
    Console.WriteLine($"Admin Charges (0.50%)                                          {s.AdminCharges,14:N2}");
    Console.WriteLine($"Total Employer Cost                                            {s.TotalEmployerCost,14:N2}");
    Console.WriteLine("-----------------------------------------------------------------------------");
    Console.WriteLine($"NET MONTHLY TAKE-HOME (Gross - Deductions)                     {s.NetMonthlyTakeHome,14:N2}");
    Console.WriteLine($"TOTAL CTC (Gross + Employer Cost)                              {s.TotalCtc,14:N2}");
    Console.WriteLine("=============================================================================");

    if (s.PfOnCeiling)
    {
        Console.WriteLine($"Note: PF computed on the statutory wage ceiling of Rs {PayrollCalculatorCore.PfWageCeiling:N0} (Basic+DA exceeds it).");
    }
    if (s.TaxRegime == TaxRegime.Old && s.HraExemptionAnnual > 0)
    {
        Console.WriteLine($"Note: HRA exemption u/s 10(13A) applied: Rs {s.HraExemptionAnnual:N2} per year (old regime only).");
    }
    if (s.TaxRegime == TaxRegime.New && s.Hra > 0)
    {
        Console.WriteLine("Note: HRA exemption is NOT available under the new regime.");
    }
    if (!s.EsiApplicable && s.GrossMonthlySalary > EsiResult.WageCeiling)
    {
        Console.WriteLine($"Note: ESI not applicable - gross exceeds Rs {EsiResult.WageCeiling:N0} per month.");
    }
    if (s.IsFebruaryHigherPtSlab)
    {
        Console.WriteLine($"Note: February higher PT slab applied (Rs {s.ProfessionalTax:N0} instead of Rs 200).");
    }
}

static string FormatEsiLine(PayrollSlip s) =>
    s.EsiApplicable
        ? $"Employee ESI (0.75%)                                           {s.EmployeeEsi,14:N2}"
        : $"Employee ESI (not applicable)                                  {s.EmployeeEsi,14:N2}";

static string FormatEmployerEsiLine(PayrollSlip s) =>
    s.EsiApplicable
        ? $"Employer ESI (3.25%)                                           {s.EmployerEsi,14:N2}"
        : $"Employer ESI (not applicable)                                  {s.EmployerEsi,14:N2}";
