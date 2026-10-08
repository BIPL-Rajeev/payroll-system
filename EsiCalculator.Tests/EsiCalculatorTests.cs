using Xunit;

namespace EsiCalculator.Tests;

public class EsiCalculatorTests
{
    // Case 1: Salary Rs 15,000 -> employee 113, employer 488, total 601.
    [Fact]
    public void Salary15000_StandardRates()
    {
        var r = EsiCalculatorCore.Calculate(15_000m);

        Assert.True(r.IsApplicable);
        Assert.Equal(113m, r.EmployeeContribution);   // 15,000 x 0.75% = 112.5 -> 113
        Assert.Equal(488m, r.EmployerContribution);   // 15,000 x 3.25% = 487.5 -> 488
        Assert.Equal(601m, r.TotalEsi);
        Assert.Equal(0.0075m, r.EmployeeRate);
        Assert.Equal(0.0325m, r.EmployerRate);
    }

    // Case 2: Salary Rs 21,000 -> applicable (boundary inclusive).
    [Fact]
    public void Salary21000_Applicable()
    {
        var r = EsiCalculatorCore.Calculate(21_000m);

        Assert.True(r.IsApplicable);
        Assert.Equal(158m, r.EmployeeContribution);   // 157.5 -> 158
        Assert.Equal(683m, r.EmployerContribution);   // 682.5 -> 683
        Assert.Equal(841m, r.TotalEsi);
    }

    // Case 3: Salary Rs 21,001 -> not applicable.
    [Fact]
    public void Salary21001_NotApplicable()
    {
        var r = EsiCalculatorCore.Calculate(21_001m);

        Assert.False(r.IsApplicable);
        Assert.Equal(0m, r.EmployeeContribution);
        Assert.Equal(0m, r.EmployerContribution);
        Assert.Equal(0m, r.TotalEsi);
        Assert.Equal(0m, r.EmployeeRate);
        Assert.Equal(0m, r.EmployerRate);
    }

    // Case 4: Salary Rs 10,000 with disability flag -> employee contribution 0.
    [Fact]
    public void Salary10000_Disability_EmployeeContributionZero()
    {
        var r = EsiCalculatorCore.Calculate(10_000m, isDisabled: true);

        Assert.True(r.IsApplicable);
        Assert.Equal(0m, r.EmployeeContribution);
        Assert.Equal(0m, r.EmployeeRate);
        Assert.Equal(325m, r.EmployerContribution);   // employer share unchanged at 3.25%
        Assert.Equal(0.0325m, r.EmployerRate);
        Assert.Equal(325m, r.TotalEsi);
    }

    // First-time employee exemption mirrors the disability waiver.
    [Fact]
    public void Salary10000_FirstYearExempt_EmployeeContributionZero()
    {
        var r = EsiCalculatorCore.Calculate(10_000m, isFirstYearExempt: true);

        Assert.True(r.IsApplicable);
        Assert.Equal(0m, r.EmployeeContribution);
        Assert.Equal(325m, r.EmployerContribution);
        Assert.Equal(325m, r.TotalEsi);
    }

    // Both flags set -> still just a zero employee share, employer unchanged.
    [Fact]
    public void BothFlags_EmployeeZero_EmployerNormal()
    {
        var r = EsiCalculatorCore.Calculate(18_000m, isDisabled: true, isFirstYearExempt: true);

        Assert.True(r.IsApplicable);
        Assert.Equal(0m, r.EmployeeContribution);
        Assert.Equal(585m, r.EmployerContribution);   // 18,000 x 3.25% = 585 exactly
        Assert.Equal(585m, r.TotalEsi);
    }

    // The ceiling itself is covered but flags do not make an over-ceiling salary applicable.
    [Fact]
    public void OverCeiling_WithFlags_StillNotApplicable()
    {
        var r = EsiCalculatorCore.Calculate(25_000m, isDisabled: true, isFirstYearExempt: true);

        Assert.False(r.IsApplicable);
        Assert.Equal(0m, r.TotalEsi);
    }

    // Exact-rate salaries round without a midpoint.
    [Fact]
    public void Salary10000_ExactContributions()
    {
        var r = EsiCalculatorCore.Calculate(10_000m);

        Assert.Equal(75m, r.EmployeeContribution);
        Assert.Equal(325m, r.EmployerContribution);
        Assert.Equal(400m, r.TotalEsi);
    }

    // Negative salary is rejected at the calculation boundary.
    [Fact]
    public void NegativeSalary_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EsiCalculatorCore.Calculate(-1m));
    }

    // Zero salary is applicable with zero contributions.
    [Fact]
    public void ZeroSalary_Applicable_ZeroContributions()
    {
        var r = EsiCalculatorCore.Calculate(0m);

        Assert.True(r.IsApplicable);
        Assert.Equal(0m, r.EmployeeContribution);
        Assert.Equal(0m, r.EmployerContribution);
        Assert.Equal(0m, r.TotalEsi);
    }
}
