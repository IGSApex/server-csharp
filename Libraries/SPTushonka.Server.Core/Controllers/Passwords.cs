using System.Security.Cryptography;

namespace SPTarkov.Server.Core.Controllers;

/// <summary>Stores salted password hashes in profile.info.password.</summary>
internal static class Passwords
{
    private const int Iterations = 310_000;

    public static bool IsValid(string? password) =>
        !string.IsNullOrWhiteSpace(password) && password.Length >= 8 && password.Length <= 256;

    public static string Hash(string password)
    {
        if (!IsValid(password)) throw new ArgumentException("Invalid password.");
        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256:{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string? encoded, string? password)
    {
        if (string.IsNullOrWhiteSpace(encoded) || password is null) return false;
        var parts = encoded.Split(':');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" ||
            !int.TryParse(parts[1], out var iterations) || iterations < 100_000 || iterations > 1_000_000)
            return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length != 32 || expected.Length != 32) return false;
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException) { return false; }
    }
}
