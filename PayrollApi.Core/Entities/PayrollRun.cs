namespace PayrollApi.Core.Entities;

/// <summary>One generated monthly payroll run (the saved salary slip header).</summary>
public class PayrollRun
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    /// <summary>Month 1-12 the run was generated for.</summary>
    public int MonthNumber { get; set; }

    /// <summary>Financial/calendar year of the run.</summary>
    public int Year { get; set; }

    public decimal GrossSalary { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }
    public decimal TotalEmployerCost { get; set; }
    public decimal TotalCtc { get; set; }

    public DateTime GeneratedAt { get; set; }

    public Employee? Employee { get; set; }

    public ICollection<PayrollRunDetail> Details { get; set; } = new List<PayrollRunDetail>();
}
