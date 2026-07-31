using FleetOps.Infrastructure.Identity;
using Xunit;

namespace FleetOps.UnitTests.Infrastructure;

public sealed class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verifies_a_correct_password()
    {
        var hash = _hasher.Hash("correct horse battery staple");
        Assert.True(_hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Rejects_an_incorrect_password()
    {
        var hash = _hasher.Hash("correct horse battery staple");
        Assert.False(_hasher.Verify("Correct horse battery staple", hash));
    }

    [Fact]
    public void Same_password_hashes_differently_every_time()
    {
        // Distinct random salts; identical hashes would leak which users share a password.
        Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));
    }

    [Fact]
    public void Hash_records_its_iteration_count_so_it_can_be_raised_later()
    {
        var parts = _hasher.Hash("anything").Split('.');

        Assert.Equal(3, parts.Length);
        Assert.True(int.TryParse(parts[0], out var iterations));
        Assert.True(iterations >= 210_000);
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("1.2")]
    [InlineData("notanumber.c2FsdA==.a2V5")]
    public void Malformed_hashes_return_false_rather_than_throwing(string hash)
        => Assert.False(_hasher.Verify("password", hash));

    [Fact]
    public void Empty_password_never_verifies()
        => Assert.False(_hasher.Verify("", _hasher.Hash("real password")));
}
