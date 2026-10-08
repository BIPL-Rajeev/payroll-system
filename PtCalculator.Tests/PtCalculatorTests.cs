using Xunit;

namespace PtCalculator.Tests;

public class PtCalculatorTests
{
    // ---- Required cases ----

    // Maharashtra Rs 12,000 in January -> Rs 200 (above Rs 10,000, non-February).
    [Fact]
    public void Maharashtra12000_January_Is200()
    {
        var r = PtCalculatorCore.Calculate(12_000m, PtState.Maharashtra, 1);

        Assert.Equal("Maharashtra", r.StateName);
        Assert.Equal(200m, r.MonthlyPt);
        Assert.False(r.IsFebruaryHigherSlab);
    }

    // Maharashtra Rs 12,000 in February -> Rs 300 (February higher slab).
    [Fact]
    public void Maharashtra12000_February_Is300()
    {
        var r = PtCalculatorCore.Calculate(12_000m, PtState.Maharashtra, 2);

        Assert.Equal(300m, r.MonthlyPt);
        Assert.True(r.IsFebruaryHigherSlab);
        Assert.Equal("February", r.MonthName);
    }

    // Karnataka Rs 18,000 -> Rs 150 (15,001 - 20,000 slab).
    [Fact]
    public void Karnataka18000_Is150()
    {
        var r = PtCalculatorCore.Calculate(18_000m, PtState.Karnataka, 1);

        Assert.Equal("Karnataka", r.StateName);
        Assert.Equal(150m, r.MonthlyPt);
    }

    // Tamil Nadu Rs 25,000 -> Rs 135 (21,001 - 30,000 slab).
    [Fact]
    public void TamilNadu25000_Is135()
    {
        var r = PtCalculatorCore.Calculate(25_000m, PtState.TamilNadu, 1);

        Assert.Equal("Tamil Nadu", r.StateName);
        Assert.Equal(135m, r.MonthlyPt);
    }

    // Invalid state name fails to parse.
    [Fact]
    public void InvalidState_Gujarat_FailsToParse()
    {
        Assert.False(PtCalculatorCore.TryParseState("Gujarat", out _));
    }

    // ---- Maharashtra boundaries ----

    [Fact]
    public void Maharashtra7500_IsNil()
    {
        Assert.Equal(0m, PtCalculatorCore.MonthlyPt(7_500m, PtState.Maharashtra, 1));
    }

    [Fact]
    public void Maharashtra7501_Is175()
    {
        Assert.Equal(175m, PtCalculatorCore.MonthlyPt(7_501m, PtState.Maharashtra, 1));
    }

    [Fact]
    public void Maharashtra10000_Is175()
    {
        Assert.Equal(175m, PtCalculatorCore.MonthlyPt(10_000m, PtState.Maharashtra, 1));
    }

    [Fact]
    public void Maharashtra10001_Is200()
    {
        Assert.Equal(200m, PtCalculatorCore.MonthlyPt(10_001m, PtState.Maharashtra, 1));
    }

    // February's Rs 300 applies only above Rs 10,000; the 175 slab stays 175 in February.
    [Fact]
    public void Maharashtra8000_February_Still175()
    {
        Assert.Equal(175m, PtCalculatorCore.MonthlyPt(8_000m, PtState.Maharashtra, 2));
    }

    // ---- Karnataka boundaries ----

    [Fact]
    public void Karnataka15000_IsNil()
    {
        Assert.Equal(0m, PtCalculatorCore.MonthlyPt(15_000m, PtState.Karnataka, 1));
    }

    [Fact]
    public void Karnataka15001_Is150()
    {
        Assert.Equal(150m, PtCalculatorCore.MonthlyPt(15_001m, PtState.Karnataka, 1));
    }

    [Fact]
    public void Karnataka20000_Is150()
    {
        Assert.Equal(150m, PtCalculatorCore.MonthlyPt(20_000m, PtState.Karnataka, 1));
    }

    [Fact]
    public void Karnataka20001_Is200()
    {
        Assert.Equal(200m, PtCalculatorCore.MonthlyPt(20_001m, PtState.Karnataka, 1));
    }

    // ---- Tamil Nadu slabs ----

    [Theory]
    [InlineData(21_000, 0)]        // nil up to 21,000
    [InlineData(21_001, 135)]      // 21,001 - 30,000
    [InlineData(30_000, 135)]
    [InlineData(30_001, 315)]      // 30,001 - 45,000
    [InlineData(45_000, 315)]
    [InlineData(45_001, 690)]      // 45,001 - 60,000
    [InlineData(60_000, 690)]
    [InlineData(60_001, 1_025)]    // 60,001 - 75,000
    [InlineData(75_000, 1_025)]
    [InlineData(75_001, 1_250)]    // above 75,000
    public void TamilNadu_SlabBoundaries(int salary, int expectedPt)
    {
        Assert.Equal(expectedPt, PtCalculatorCore.MonthlyPt(salary, PtState.TamilNadu, 1));
    }

    // Tamil Nadu ignores the month (no February rule).
    [Fact]
    public void TamilNadu_February_SameAsOtherMonths()
    {
        Assert.Equal(
            PtCalculatorCore.MonthlyPt(50_000m, PtState.TamilNadu, 2),
            PtCalculatorCore.MonthlyPt(50_000m, PtState.TamilNadu, 5));
    }

    // ---- Annual totals ----

    // Maharashtra > 10,000: 11 x 200 + 300 (February) = 2,500.
    [Fact]
    public void Maharashtra_Annual_IncludesFebruaryHigherSlab()
    {
        var r = PtCalculatorCore.Calculate(12_000m, PtState.Maharashtra, 1);

        Assert.Equal(2_500m, r.AnnualPt);
    }

    // Maharashtra 175 slab: 12 x 175 = 2,100.
    [Fact]
    public void Maharashtra_Annual_LowerSlab()
    {
        var r = PtCalculatorCore.Calculate(8_000m, PtState.Maharashtra, 2);

        Assert.Equal(175m, r.MonthlyPt);
        Assert.Equal(2_100m, r.AnnualPt);
    }

    // Karnataka > 20,000: 12 x 200 = 2,400.
    [Fact]
    public void Karnataka_Annual_UpperSlab()
    {
        var r = PtCalculatorCore.Calculate(25_000m, PtState.Karnataka, 1);

        Assert.Equal(2_400m, r.AnnualPt);
    }

    // Tamil Nadu > 75,000: 12 x 1,250 = 15,000.
    [Fact]
    public void TamilNadu_Annual_TopSlab()
    {
        var r = PtCalculatorCore.Calculate(80_000m, PtState.TamilNadu, 1);

        Assert.Equal(1_250m, r.MonthlyPt);
        Assert.Equal(15_000m, r.AnnualPt);
    }

    // ---- State parsing ----

    [Theory]
    [InlineData("maharashtra", PtState.Maharashtra)]
    [InlineData("MAHARASHTRA", PtState.Maharashtra)]
    [InlineData("mh", PtState.Maharashtra)]
    [InlineData("Karnataka", PtState.Karnataka)]
    [InlineData("ka", PtState.Karnataka)]
    [InlineData("tamil nadu", PtState.TamilNadu)]
    [InlineData("TamilNadu", PtState.TamilNadu)]
    [InlineData("tn", PtState.TamilNadu)]
    public void TryParseState_AcceptsSupportedNames(string text, PtState expected)
    {
        Assert.True(PtCalculatorCore.TryParseState(text, out PtState state));
        Assert.Equal(expected, state);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Gujarat")]
    [InlineData("Delhi")]
    [InlineData("Kerala")]
    public void TryParseState_RejectsUnsupportedNames(string text)
    {
        Assert.False(PtCalculatorCore.TryParseState(text, out _));
    }

    // ---- Validation ----

    [Fact]
    public void NegativeSalary_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PtCalculatorCore.Calculate(-1m, PtState.Maharashtra, 1));
    }

    [Fact]
    public void MonthOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PtCalculatorCore.Calculate(12_000m, PtState.Maharashtra, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PtCalculatorCore.Calculate(12_000m, PtState.Maharashtra, 13));
    }
}
