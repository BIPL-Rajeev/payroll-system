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

## Authentication (JWT)

All API endpoints require a JWT bearer token except `POST /api/auth/login`:

- `POST /api/auth/login` -> `{ token, username, role }` (anonymous)
- `POST /api/auth/register` -> create a new user (**Admin only**, 201)
- `GET  /api/auth/me` -> current user info from the token claims

Roles: **Admin** (full access incl. user management), **HR** (may create/update/delete
employees and run payroll), **Viewer** (read-only).

Rules:

- `GET /api/employees/*`, `GET /api/payroll/*`, `GET /api/form16/*` -> any authenticated role
- `POST/PUT/DELETE /api/employees/*` and `POST /api/payroll/run/*` -> Admin or HR
- All other `/api/*` endpoints -> any authenticated user

### Default admin credentials - CHANGE ON FIRST LOGIN

On first startup (when no users exist) the seed creates:

- Username: `admin`
- Password: `Admin@123`

**This is a well-known default documented here - change this password before any
real use.** (Immediately register a personal Admin account with a strong password
and deactivate the seeded user.)

### Get a token via curl

```bash
# 1. Login and grab the token
curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "admin", "password": "Admin@123"}'

# Response: { "token": "eyJ...", "username": "admin", "role": "Admin" }

# 2. Call a protected endpoint
curl http://localhost:5000/api/employees \
  -H "Authorization: Bearer eyJ..."

# 3. Register a new user (Admin only)
curl -X POST http://localhost:5000/api/auth/register \
  -H "Authorization: Bearer eyJ..." \
  -H "Content-Type: application/json" \
  -d '{"username": "hr2", "password": "StrongPass@1", "role": "hr"}'
```

JWT settings live in `PayrollApi/appsettings.json` (`Jwt:SecretKey` - change the
development default in production, `Issuer: PayrollApi`, `Audience: PayrollWeb`,
`ExpiryMinutes: 60`). Passwords are hashed with BCrypt. The Swagger UI has an
**Authorize** button to paste the token.

## Testing

Run the full suite (140+ tests across all calculator, API, and web test projects):

```bash
dotnet test
```

Each calculator project also ships its own xUnit test project.

## Disclaimer

This is for learning/demo purposes only. All calculations must be verified with a Chartered Accountant (CA) before being used for real payroll.
