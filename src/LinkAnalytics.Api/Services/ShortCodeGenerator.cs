using System.Security.Cryptography;

namespace LinkAnalytics.Api.Services;

public interface IShortCodeGenerator
{
    string Generate();
}

/// <summary>
/// Generates random Base62 codes (e.g. "aZ3kP9x") using a cryptographically secure RNG,
/// so codes are not sequential or guessable.
/// </summary>
public sealed class RandomShortCodeGenerator : IShortCodeGenerator
{
    public const int CodeLength = 7;

    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public string Generate() => RandomNumberGenerator.GetString(Alphabet, CodeLength);
}
