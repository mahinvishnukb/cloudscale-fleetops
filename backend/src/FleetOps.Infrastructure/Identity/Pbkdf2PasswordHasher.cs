using System.Globalization;
using System.Security.Cryptography;
using FleetOps.Application.Abstractions;

namespace FleetOps.Infrastructure.Identity;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing. Format: {iterations}.{base64 salt}.{base64 key},
/// so the iteration count can be raised later without invalidating existing hashes.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int DefaultIterations = 210_000; // OWASP guidance for PBKDF2-HMAC-SHA256.

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, DefaultIterations, Algorithm, KeySize);

        return string.Join('.',
            DefaultIterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        var parts = hash.Split('.', 3);
        if (parts.Length != 3 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iterations))
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[1]);
            var expected = Convert.FromBase64String(parts[2]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);

            // Constant-time comparison; a plain == leaks timing information.
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
