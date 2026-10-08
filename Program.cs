const decimal EmployeePfRate = 0.12m;   // employee 12% -> EPF
const decimal EmployerEpsRate = 0.0833m; // employer 8.33% -> EPS
const decimal EmployerEpfRate = 0.0367m; // employer 3.67% -> EPF
const decimal EdliRate = 0.005m;         // employer-only EDLI 0.5%
const decimal AdminRate = 0.005m;        // employer-only admin charges 0.5%

Console.WriteLine("=== Indian EPF (Provident Fund) Calculator ===");
Console.WriteLine($"Employee EPF {EmployeePfRate:P2} | Employer EPS {EmployerEpsRate:P2} + EPF {EmployerEpfRate:P2}");
Console.WriteLine($"Employer EDLI {EdliRate:P2} | Admin Charges {AdminRate:P2}");
Console.WriteLine();

decimal statutoryCeiling = ReadNonNegative("Enter statutory wage ceiling (default 15000, 0 for no ceiling): ", 15000m);

decimal basicSalary = ReadNonNegative("Enter basic salary: ", null);

// Contributions are computed on the lower of actual basic salary and the wage ceiling.
bool ceilingApplies = statutoryCeiling > 0;
decimal contributionBase = ceilingApplies ? Math.Min(basicSalary, statutoryCeiling) : basicSalary;
bool onCeiling = ceilingApplies && basicSalary > statutoryCeiling;

decimal employeeEpf = Round2(contributionBase * EmployeePfRate);
decimal employerEps = Round2(contributionBase * EmployerEpsRate);
decimal employerEpf = Round2(contributionBase * EmployerEpfRate);
decimal edli = Round2(contributionBase * EdliRate);
decimal adminCharges = Round2(contributionBase * AdminRate);
decimal totalEmployerCost = Round2(employerEpf + employerEps + edli + adminCharges);

Console.WriteLine();
Console.WriteLine("---------- PF Contribution Breakdown ----------");
Console.WriteLine($"Basic Salary          : {basicSalary:N2}");
if (ceilingApplies)
{
    Console.WriteLine($"Wage ceiling          : {statutoryCeiling:N2}{(onCeiling ? " (applied - basic exceeds ceiling)" : "")}");
}
Console.WriteLine($"Contribution base     : {contributionBase:N2}");
Console.WriteLine();
Console.WriteLine($"Employee EPF (12%)    : {employeeEpf:N2}");
Console.WriteLine($"Employer EPF (3.67%)  : {employerEpf:N2}");
Console.WriteLine($"Employer EPS (8.33%)  : {employerEps:N2}");
Console.WriteLine($"EDLI (0.50%)          : {edli:N2}");
Console.WriteLine($"Admin Charges (0.50%) : {adminCharges:N2}");
Console.WriteLine("-----------------------------------------------");
Console.WriteLine($"Total Employer Cost   : {totalEmployerCost:N2}");
Console.WriteLine($"Total PF + EDLI + Admin (all parties) : {Round2(employeeEpf + totalEmployerCost):N2}");
Console.WriteLine($"Salary after employee EPF deduction   : {Round2(basicSalary - employeeEpf):N2}");

static decimal ReadNonNegative(string prompt, decimal? defaultValue)
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

        if (string.IsNullOrWhiteSpace(input))
        {
            if (defaultValue.HasValue)
            {
                return defaultValue.Value;
            }
            Console.WriteLine("Input is required. Please enter a non-negative number.");
            continue;
        }

        if (decimal.TryParse(input, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out decimal value) && value >= 0)
        {
            return value;
        }

        Console.WriteLine("Invalid input. Please enter a non-negative number.");
    }
}

static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
