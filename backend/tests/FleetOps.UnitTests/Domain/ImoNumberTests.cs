using FleetOps.Domain.Common;
using FleetOps.Domain.Vessels;
using Xunit;

namespace FleetOps.UnitTests.Domain;

public sealed class ImoNumberTests
{
    [Theory]
    [InlineData("9074729")]
    [InlineData("9395044")]
    [InlineData("9321483")]
    [InlineData("IMO 9074729")]
    [InlineData("  9074729  ")]
    public void Create_accepts_valid_imo_numbers(string raw)
    {
        var imo = ImoNumber.Create(raw);
        Assert.Equal(7, imo.Value.Length);
    }

    [Theory]
    [InlineData("9074720")] // correct length, wrong check digit
    [InlineData("907472")]  // too short
    [InlineData("90747299")] // too long
    [InlineData("90747A9")] // non-numeric
    [InlineData("")]
    [InlineData(null)]
    public void Create_rejects_invalid_imo_numbers(string? raw)
        => Assert.Throws<DomainException>(() => ImoNumber.Create(raw));

    [Fact]
    public void Check_digit_is_the_units_digit_of_the_weighted_sum()
    {
        // 9*7 + 0*6 + 7*5 + 4*4 + 7*3 + 2*2 = 63 + 0 + 35 + 16 + 21 + 4 = 139 -> 9
        Assert.True(ImoNumber.TryCreate("9074729", out _));
        Assert.False(ImoNumber.TryCreate("9074728", out _));
    }

    [Fact]
    public void TryCreate_reports_failure_without_throwing()
    {
        Assert.False(ImoNumber.TryCreate("not-an-imo", out var imo));
        Assert.Equal(default, imo);
    }
}
