# Indian Payroll Calculators (FY 2025-26)

A suite of .NET 10 console calculators (PF, TDS, ESI, Professional Tax, full payroll)
plus an ASP.NET Core Minimal API exposing the payroll calculation over HTTP.

## Payroll API

Projects:

- `PayrollApi` - Minimal API web project (endpoints, validation, Swagger, CORS)
- `PayrollApi.Core` - Class library with all calculator classes
  (PF, ESI, PT, TDS old/new regimes, combined payroll core)
- `PayrollApi.Tests` - Integration tests (WebApplicationFactory)

### How to run

```bash
dotnet run --project PayrollApi
```

The API listens on `http://localhost:5000`.

### Swagger URL

http://localhost:5000/swagger

(OpenAPI document: http://localhost:5000/swagger/v1/swagger.json)

### Endpoints

Stateless calculation:

- `GET  /api/payroll/health` -> `{ "status": "ok" }`
- `POST /api/payroll/calculate` -> full monthly salary slip JSON

Employee master (SQLite, soft delete via `isActive`):

- `POST   /api/employees` -> create employee (201; 400 on duplicate `employeeCode`)
- `GET    /api/employees` -> list all active employees
- `GET    /api/employees/{id}` -> get one
- `PUT    /api/employees/{id}` -> update
- `DELETE /api/employees/{id}` -> soft delete (`isActive = false`, 204)

Payroll runs (persisted):

- `POST /api/payroll/run/{employeeId}/{month}/{year}` -> calculate and SAVE the run + details (404 if employee missing)
- `GET  /api/payroll/history/{employeeId}` -> all past payroll runs for the employee
- `GET  /api/payroll/slip/{payrollRunId}` -> one slip with all component details
- `GET  /api/payroll/slip/{payrollRunId}/pdf` -> downloadable PDF payslip (QuestPDF, A4) -> 404 if the run does not exist

PDF sample curl:

```bash
curl -OJ http://localhost:5000/api/payroll/slip/1/pdf
# downloads Payslip_{EmployeeCode}_{Month}_{Year}.pdf (e.g. Payslip_EMP001_January_2026.pdf)
```

The Blazor UI (`PayrollWeb`) also shows a "Download PDF" button on the
`/payroll/slip/{payrollRunId}` page.

### Form 16 (Part B)

Annual Form 16 Part B for one employee and one financial year (April to March):

- `GET /api/form16/{employeeId}/{financialYear}` -> Form 16 Part B JSON
  (sections: employee/employer details, gross salary, exemptions, Chapter VI-A,
  taxable income, tax computation, TDS summary)
- `GET /api/form16/{employeeId}/{financialYear}/pdf` -> downloadable PDF
  (title "FORM 16 - PART B")

Validation: `financialYear` must match `2025-26` (400 otherwise - URL-encode a
slash as `%2F` when testing, e.g. `2025%2F26`), unknown employee -> 404,
no payroll runs in that FY -> 400. Blazor page: `/form16/{employeeId}`
(employee selector, FY dropdown for the last 3 years, Download PDF button).

Sample curls:

```bash
# JSON Form 16 for FY 2025-26
curl http://localhost:5000/api/form16/1/2025-26

# PDF download
curl -OJ http://localhost:5000/api/form16/1/2025-26/pdf
# downloads Form16PartB_EMP001_2025-26.pdf
```

Chapter VI-A caps (old regime only): 80C 1,50,000 / 80CCD(1B) 50,000 /
80D 25,000 / 80TTA 10,000; 80CCD(2) employer NPS is allowed in both regimes
(not tracked yet, reported as 0). Taxable income is rounded to the nearest
Rs 10 per income-tax rules.

### Sample curl request

```bash
curl -X POST http://localhost:5000/api/payroll/calculate \
  -H "Content-Type: application/json" \
  -d '{
    "employeeId": "EMP001",
    "name": "Asha Patil",
    "state": "Maharashtra",
    "isMetro": true,
    "taxRegime": "old",
    "monthlyBasic": 50000,
    "monthlyHra": 20000,
    "monthlyDa": 5000,
    "monthlySpecialAllowance": 5000,
    "monthlyLta": 0,
    "monthlyOtherAllowances": 0,
    "annualRentPaid": 180000,
    "section80C": 150000,
    "section80CCD1B": 0,
    "section80D": 0,
    "section80TTA": 0,
    "homeLoanInterest": 0,
    "isFirstYearEmployee": false,
    "hasDisability": false,
    "monthNumber": 1
  }'
```

Returns `200 OK` with the salary slip (`grossMonthlySalary`, `totalDeductions`,
`netMonthlyTakeHome`, `totalCtc`, ...). Validation failures return
`400 Bad Request` with an `error` message (invalid state, negative salary,
month outside 1-12, invalid tax regime).

### Database (EF Core + SQLite)

Connection string lives in `PayrollApi/appsettings.json`:

```json
"ConnectionStrings": { "PayrollDb": "Data Source=payroll.db" }
```

Migrations are applied automatically at startup (`db.Database.Migrate()`), which
creates/updates `payroll.db` on first run. To create or edit migrations manually:

```bash
# one-time: install the EF CLI tool
dotnet tool install -g dotnet-ef

# add a migration after changing entities/DbContext
dotnet ef migrations add <MigrationName> --project PayrollApi.Core --startup-project PayrollApi

# apply migrations manually (optional - the app does this at startup)
dotnet ef database update --project PayrollApi.Core --startup-project PayrollApi
```

Tables: `Employees` (unique `EmployeeCode`, `IsActive` soft delete), `PayrollRuns`,
`PayrollRunDetails` (component lines: Earning / Deduction / EmployerContribution).

### Run the integration tests

Tests use an isolated temporary SQLite database per test class (never `payroll.db`):

```bash
dotnet test PayrollApi.Tests/PayrollApi.Tests.csproj
```

## Console calculators

Each calculator is a standalone console app with its own xUnit test project:
`PfCalculator`, `TdsCalculator` (old/new regimes), `EsiCalculator`,
`PtCalculator`, and `PayrollCalculator` (full monthly salary slip).

### Payroll verification suite

`PayrollCalculator.Tests/PayrollVerificationTests/` cross-checks the calculator
against hand-computed real-world scenarios for FY 2025-26:

- `testdata/expected_results.csv` - 10 scenarios (VER001-VER010) with expected
  Gross / PF / ESI / PT / TDS / Net values. Every expected value is computed
  **by hand** (never by the calculator) and documented in the `Notes` column.
- `PayrollVerificationTests.cs` - reads the CSV, runs each row through
  `PayrollCalculatorCore` and asserts every expected column within 0.01,
  printing an expected-vs-actual diff per column on mismatch.

Run just the verification suite:

```bash
dotnet test PayrollCalculator.Tests/PayrollCalculator.Tests.csproj \
  --filter FullyQualifiedName~PayrollVerificationTests
```

**Adding a new verification scenario:**

1. Compute the expected values by hand (or from a trusted source such as a
   payroll consultant) - do NOT copy them from the calculator's output.
2. Append a row to `testdata/expected_results.csv` with the next `VERxxx`
   code: inputs first (state, regime, monthly components, annual investments,
   flags, month), then `ExpectedGross, ExpectedEmployeePF, ExpectedEmployerPF,
   ExpectedEmployeeESI, ExpectedEmployerESI, ExpectedPT, ExpectedTDS,
   ExpectedNetPay` and a `Notes` column showing your derivation.
3. Keep numbers unformatted (no thousand separators) and avoid commas in
   `Notes`, or quote the field.
4. Increment the expected scenario count in `Csv_ContainsTenScenarios`.
5. `dotnet test` - a mismatch prints the scenario id, column name, expected,
   actual and the difference.
