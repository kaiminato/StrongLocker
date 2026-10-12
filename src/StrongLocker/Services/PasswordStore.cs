using System;
using System.Security.Cryptography;
using StrongLocker.Models;

namespace StrongLocker.Services;

/// <summary>
/// Password hashing/verification using PBKDF2 (SHA-256). Only the salt and the
/// derived hash are ever stored; the password is never persisted or logged.
/// </summary>
public static class PasswordStore
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static void SetPassword(AppSettings settings, string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
        byte[] hash = Derive(password, salt, settings.PasswordIterations);
        settings.PasswordSalt = Convert.ToBase64String(salt);
        settings.PasswordHash = Convert.ToBase64String(hash);
        settings.PasswordLength = password.Length;
    }

    public static bool Verify(AppSettings settings, string password)
    {
        if (!settings.HasPassword) return false;
        try
        {
            byte[] salt = Convert.FromBase64String(settings.PasswordSalt!);
            byte[] expected = Convert.FromBase64String(settings.PasswordHash!);
            byte[] actual = Derive(password, salt, settings.PasswordIterations);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashBytes);
}
