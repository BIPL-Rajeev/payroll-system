using Microsoft.EntityFrameworkCore;
using PayrollApi.Core.Entities;

namespace PayrollApi.Core.Persistence;

// Inside the namespace so this alias wins over PayrollApi.Core.Employee (the calculation model).
using Employee = PayrollApi.Core.Entities.Employee;

/// <summary>EF Core DbContext for the payroll database (SQLite).</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<PayrollRunDetail> PayrollRunDetails => Set<PayrollRunDetail>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasIndex(e => e.EmployeeCode).IsUnique();
            entity.Property(e => e.EmployeeCode).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.State).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TaxRegime).IsRequired().HasMaxLength(16);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MonthlyBasic).HasPrecision(18, 2);
            entity.Property(e => e.MonthlyHra).HasPrecision(18, 2);
            entity.Property(e => e.MonthlyDa).HasPrecision(18, 2);
            entity.Property(e => e.MonthlySpecialAllowance).HasPrecision(18, 2);
            entity.Property(e => e.MonthlyLta).HasPrecision(18, 2);
            entity.Property(e => e.MonthlyOtherAllowances).HasPrecision(18, 2);
            entity.Property(e => e.AnnualRentPaid).HasPrecision(18, 2);
            entity.Property(e => e.Investment80C).HasPrecision(18, 2);
            entity.Property(e => e.Investment80CCD1B).HasPrecision(18, 2);
            entity.Property(e => e.Investment80D).HasPrecision(18, 2);
            entity.Property(e => e.Investment80TTA).HasPrecision(18, 2);
            entity.Property(e => e.HomeLoanInterest).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PayrollRun>(entity =>
        {
            entity.Property(r => r.GrossSalary).HasPrecision(18, 2);
            entity.Property(r => r.TotalDeductions).HasPrecision(18, 2);
            entity.Property(r => r.NetPay).HasPrecision(18, 2);
            entity.Property(r => r.TotalEmployerCost).HasPrecision(18, 2);
            entity.Property(r => r.TotalCtc).HasPrecision(18, 2);

            entity.HasOne(r => r.Employee)
                  .WithMany(e => e.PayrollRuns)
                  .HasForeignKey(r => r.EmployeeId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.Username).IsRequired().HasMaxLength(96);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
            entity.Property(u => u.Role).IsRequired().HasMaxLength(32);
            entity.Property(u => u.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<PayrollRunDetail>(entity =>
        {
            entity.Property(d => d.ComponentName).IsRequired().HasMaxLength(200);
            entity.Property(d => d.ComponentType).IsRequired().HasMaxLength(32);
            entity.Property(d => d.Amount).HasPrecision(18, 2);

            entity.HasOne(d => d.PayrollRun)
                  .WithMany(r => r.Details)
                  .HasForeignKey(d => d.PayrollRunId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
