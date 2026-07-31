using FleetOps.Domain.Manifests;
using Xunit;

namespace FleetOps.UnitTests.Domain;

public sealed class ContainerNumberTests
{
    [Theory]
    [InlineData("CSQU3054383")] // the canonical ISO 6346 worked example
    [InlineData("MSKU3820945")]
    [InlineData("TGHU1234567")]
    [InlineData("APZU4412345")]
    public void Accepts_valid_container_numbers(string raw)
        => Assert.True(ContainerNumber.TryCreate(raw, out _));

    [Theory]
    [InlineData("CSQU3054384")] // wrong check digit
    [InlineData("CSQ13054383")] // digit where a letter belongs
    [InlineData("CSQU305438")]  // too short
    [InlineData("CSQU30543833")] // too long
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_invalid_container_numbers(string? raw)
        => Assert.False(ContainerNumber.TryCreate(raw, out _));

    [Fact]
    public void Normalises_case_and_whitespace()
    {
        Assert.True(ContainerNumber.TryCreate("  csqu 305 4383 ", out var container));
        Assert.Equal("CSQU3054383", container.Value);
    }

    [Fact]
    public void Letter_values_skip_multiples_of_eleven()
    {
        // A=10, B=12 (11 skipped), K=21, L=23 (22 skipped), U=32, V=34 (33 skipped).
        // Verified indirectly: a container whose check digit depends on those values.
        Assert.Equal(3, ContainerNumber.ComputeCheckDigit("CSQU305438"));
    }
}
