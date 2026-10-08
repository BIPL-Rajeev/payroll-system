using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PayrollApi.Core.Entities;

namespace PayrollApi.Core.Services;

// Inside the namespace so this alias wins over PayrollApi.Core.Employee (the calculation model).
using Employee = PayrollApi.Core.Entities.Employee;

/// <summary>Configurable company header for the payslip.</summary>
public sealed record PayslipOptions
{
    public string CompanyName { get; init; } = "ABC Technologies Pvt Ltd";
    public string CompanyAddress { get; init; } = "123, MG Road, Mumbai - 400001";

    /// <summary>Employer PAN (placeholder) used on Form 16 Part B.</summary>
    public string EmployerPan { get; init; } = "ABCDE1234F";

    /// <summary>Employer TAN (placeholder) used on Form 16 Part B.</summary>
    public string EmployerTAN { get; init; } = "MUMX12345E";
}

/// <summary>
/// Generates a single-page A4 salary slip PDF (QuestPDF) from a payroll run,
/// its component details and the employee master record.
/// </summary>
public static class PayslipPdfGenerator
{
    private static readonly CultureInfo InrCulture = CultureInfo.GetCultureInfo("en-IN");

    private static readonly string[] MonthNames =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    ];

    /// <summary>Month name for filenames and subtitles (1-12).</summary>
    public static string MonthName(int month) =>
        month is >= 1 and <= 12 ? MonthNames[month - 1] : $"Month{month}";

    /// <summary>
    /// Produces the payslip PDF. Throws when employee is missing.
    /// </summary>
    /// <param name="run">The saved payroll run (header totals).</param>
    /// <param name="employee">Employee master; required - throws ArgumentNullException when null.</param>
    /// <param name="details">Component lines of the run.</param>
    /// <param name="options">Company header overrides (defaults to the placeholder company).</param>
    public static byte[] GeneratePdf(
        PayrollRun run,
        Employee? employee,
        IReadOnlyList<PayrollRunDetail> details,
        PayslipOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(details);
        if (employee is null)
        {
            throw new ArgumentNullException(nameof(employee), "Employee is required to generate a payslip.");
        }

        options ??= new PayslipOptions();
        string monthName = MonthName(run.MonthNumber);
        string regime = string.Equals(employee.TaxRegime, "old", StringComparison.OrdinalIgnoreCase)
            ? "Old"
            : "New";

        decimal earnings = Amount(details, "Basic Salary")
            + Amount(details, "House Rent Allowance")
            + Amount(details, "Dearness Allowance")
            + Amount(details, "Special Allowance")
            + Amount(details, "Leave Travel Allowance")
            + Amount(details, "Other Allowances");
        decimal employerPf = Amount(details, "Employer EPS") + Amount(details, "Employer EPF");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(t => t.FontSize(9));

                // ---------------- HEADER ----------------
                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text(options.CompanyName).FontSize(14).Bold();
                    col.Item().AlignCenter().Text(options.CompanyAddress).FontSize(8).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(6).AlignCenter()
                        .Text("SALARY SLIP").FontSize(13).Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().AlignCenter()
                        .Text($"For the month of {monthName} {run.Year}");
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                });

                // ---------------- CONTENT ----------------
                page.Content().PaddingTop(8).Column(col =>
                {
                    // Employee details - 2 columns (label/value pairs).
                    col.Item().Background(Colors.Grey.Lighten3).Padding(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        DetailRow("Employee Code", employee.EmployeeCode, "Name", employee.Name);
                        DetailRow("State", employee.State, "Tax Regime", regime);
                        DetailRow("Month / Year", $"{monthName} {run.Year}", "Payroll Run ID", run.Id.ToString());

                        void DetailRow(string l1, string v1, string l2, string v2)
                        {
                            table.Cell().PaddingBottom(2).Text(l1).FontSize(8).FontColor(Colors.Grey.Darken1);
                            table.Cell().PaddingBottom(2).Text(v1).Bold();
                            table.Cell().PaddingBottom(2).Text(l2).FontSize(8).FontColor(Colors.Grey.Darken1);
                            table.Cell().PaddingBottom(2).Text(v2).Bold();
                        }
                    });

                    // Earnings (left) + Deductions (right).
                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().PaddingRight(6).Column(earningsCol =>
                        {
                            earningsCol.Item().Text("EARNINGS").Bold().FontColor(Colors.Blue.Darken2);
                            earningsCol.Item().PaddingTop(2).Table(t =>
                            {
                                FillAmountTable(t,
                                    ("Basic Salary", Amount(details, "Basic Salary"), false),
                                    ("HRA", Amount(details, "House Rent Allowance"), false),
                                    ("DA", Amount(details, "Dearness Allowance"), false),
                                    ("Special Allowance", Amount(details, "Special Allowance"), false),
                                    ("LTA", Amount(details, "Leave Travel Allowance"), false),
                                    ("Other Allowances", Amount(details, "Other Allowances"), false),
                                    ("Gross Salary", run.GrossSalary, true));
                            });
                        });

                        row.RelativeItem().PaddingLeft(6).Column(deductionsCol =>
                        {
                            deductionsCol.Item().Text("DEDUCTIONS").Bold().FontColor(Colors.Blue.Darken2);
                            deductionsCol.Item().PaddingTop(2).Table(t =>
                            {
                                FillAmountTable(t,
                                    ("Employee PF", Amount(details, "Employee PF"), false),
                                    ("Employee ESI", Amount(details, "Employee ESI"), false),
                                    ("Professional Tax", Amount(details, "Professional Tax"), false),
                                    ("TDS", Amount(details, "TDS"), false),
                                    ("Total Deductions", run.TotalDeductions, true));
                            });
                        });
                    });

                    // Employer contributions - full width.
                    col.Item().PaddingTop(10).Text("EMPLOYER CONTRIBUTIONS").Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().PaddingTop(2).Table(t =>
                    {
                        FillAmountTable(t,
                            ("Employer PF (EPF + EPS)", employerPf, false),
                            ("Employer ESI", Amount(details, "Employer ESI"), false),
                            ("EDLI", Amount(details, "EDLI"), false),
                            ("Admin Charges", Amount(details, "Admin Charges"), false),
                            ("Total Employer Cost", run.TotalEmployerCost, true));
                    });

                    // Summary box.
                    col.Item().PaddingTop(12).Border(1).BorderColor(Colors.Blue.Darken2)
                        .Background(Colors.Grey.Lighten3).Padding(8).Row(box =>
                        {
                            box.RelativeItem().AlignCenter().Column(netCol =>
                            {
                                netCol.Item().Text("Net Take-Home Pay").FontSize(9).FontColor(Colors.Grey.Darken1);
                                netCol.Item().AlignCenter().Text(Rs(run.NetPay))
                                    .FontSize(16).Bold().FontColor(Colors.Green.Darken2);
                            });

                            box.RelativeItem().Column(ctcCol =>
                            {
                                ctcCol.Item().AlignCenter().Text("Total CTC").FontSize(9).FontColor(Colors.Grey.Darken1);
                                ctcCol.Item().AlignCenter().Text(Rs(run.TotalCtc))
                                    .FontSize(16).Bold();
                            });
                        });

                    // Sanity note when details and header drift apart (defensive).
                    if (earnings != run.GrossSalary)
                    {
                        col.Item().PaddingTop(2).Text("(Earnings components shown before Gross Salary total.)")
                            .FontSize(7).FontColor(Colors.Grey.Medium);
                    }
                });

                // ---------------- FOOTER ----------------
                page.Footer().PaddingTop(8).Column(foot =>
                {
                    foot.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    foot.Item().PaddingTop(2).Text(
                        "This is a computer-generated salary slip and does not require signature.")
                        .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                    foot.Item().Row(r =>
                    {
                        r.RelativeItem().Text($"Generated on: {DateTime.Now.ToString("dd MMM yyyy HH:mm", InrCulture)}")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        r.ConstantItem(100).AlignRight().Text("Page 1 of 1")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            });
        }).GeneratePdf();
    }

    private static string Rs(decimal value) => $"Rs {value.ToString("N2", InrCulture)}";

    private static decimal Amount(IReadOnlyList<PayrollRunDetail> details, string componentName) =>
        details.FirstOrDefault(d => d.ComponentName == componentName)?.Amount ?? 0m;

    private static void FillAmountTable(TableDescriptor table, params (string Label, decimal Amount, bool Bold)[] rows)
    {
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn();
            columns.ConstantColumn(100);
        });

        foreach ((string label, decimal amount, bool bold) in rows)
        {
            TextSpanDescriptor left = table.Cell().PaddingBottom(2).Text(label);
            TextSpanDescriptor right = table.Cell().PaddingBottom(2).AlignRight().Text(Rs(amount));
            if (bold)
            {
                left.Bold();
                right.Bold();
            }
        }
    }
}
