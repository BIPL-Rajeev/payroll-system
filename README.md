# Indian Payroll System

A full-stack Indian payroll system built with .NET 10, featuring PF, TDS, ESI, Professional Tax, Form 16, and PDF payslip generation.

## Tech Stack

- .NET 10
- ASP.NET Core Web API (Minimal API)
- Blazor Server
- EF Core + SQLite
- QuestPDF (PDF generation)
- xUnit (testing)

## Project Structure

### Console calculators

- `PfCalculator` - EPF/Provident Fund calculator (employee 12% / employer EPS 8.33% + EPF 3.67% / EDLI 0.5% / admin 0.5%, ₹15,000 ceiling)
- `TdsCalculator` - TDS with old + new tax regimes
- `EsiCalculator` - ESI (employee 0.75% / employer 3.25%, ₹21,000 ceiling)
- `PtCalculator` - Professional Tax (MH/KA/TN slabs, Feb 300 rule)
- `PayrollCalculator` - Combined monthly salary slip

### Web & API projects

- `PayrollApi` - ASP.NET Core Minimal API (employees CRUD, payroll run/history/slip, Form 16, Swagger)
- `PayrollApi.Core` - Calculator logic, EF Core persistence (SQLite), Form 16 generation
- `PayrollWeb` - Blazor Server UI (port 5050)

### Test projects

- `PfCalculator.Tests`
- `TdsCalculator.Tests`
- `EsiCalculator.Tests`
- `PtCalculator.Tests`
- `PayrollCalculator.Tests`
- `PayrollApi.Tests`
- `PayrollWeb.Tests`

## How to Run

### Prerequisites

- .NET 10 SDK

### Steps

1. Build the solution:
   ```bash
   dotnet build
   ```

2. Run the API (listens on `http://localhost:5000`):
   ```bash
   dotnet run --project PayrollApi
   ```

3. Run the web UI (runs on port 5050):
   ```bash
   dotnet run --project PayrollWeb
   ```

4. Open `http://localhost:5050` in your browser.

## API Endpoints

**Stateless calculation:**
- `GET  /api/payroll/health` -> `{ "status": "ok" }`
- `POST /api/payroll/calculate` -> full monthly salary slip JSON

**Employee master (SQLite, soft delete via `isActive`):**
- `POST   /api/employees` -> create employee (201; 400 on duplicate `employeeCode`)
- `GET    /api/employees` -> list all active employees
- `GET    /api/employees/{id}` -> get one
- `PUT    /api/employees/{id}` -> update
- `DELETE /api/employees/{id}` -> soft delete (`isActive = false`, 204)

**Payroll runs (persisted):**
- `POST /api/payroll/run/{employeeId}/{month}/{year}` -> calculate and SAVE the run + details (404 if employee missing)
- `GET  /api/payroll/history/{employeeId}` -> all past payroll runs for the employee
- `GET  /api/payroll/slip/{payrollRunId}` -> one slip with all component details
- `GET  /api/payroll/slip/{payrollRunId}/pdf` -> downloadable PDF payslip (QuestPDF, A4) -> 404 if the run does not exist

**Form 16 (Part B):**
- `GET /api/form16/{employeeId}/{financialYear}` -> Form 16 Part B JSON
- `GET /api/form16/{employeeId}/{financialYear}/pdf` -> downloadable PDF

## Testing

Run the full suite (140+ tests across all calculator, API, and web test projects):

```bash
dotnet test
```

Each calculator project also ships its own xUnit test project.

## Disclaimer

This is for learning/demo purposes only. All calculations must be verified with a Chartered Accountant (CA) before being used for real payroll.
