using System.Security.Cryptography;

namespace RaqmiSystem.Infrastructure.Security;

/// <summary>
/// Generates a random temporary password for the two administrator-triggered hand-off paths:
/// creating an account (UserAdministrationService.CreateAsync) and resetting a password
/// (POST /security/users/{id}/reset-password). It lives in the infrastructure security namespace
/// next to <see cref="Pbkdf2PasswordHasher"/> so both callers share a single implementation.
/// Uses RandomNumberGenerator (CSPRNG) exclusively, never System.Random.
/// </summary>
public static class TemporaryPasswordGenerator
{
    private const int Length = 20;
    private const string UpperCase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string LowerCase = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*-_=+";
    private const string AllCharacters = UpperCase + LowerCase + Digits + Symbols;

    public static string Generate()
    {
        var passwordChars = new char[Length];

        // Guarantee at least one character from every class, then fill the rest from the
        // combined alphabet so length and class coverage are both satisfied.
        passwordChars[0] = PickRandomCharacter(UpperCase);
        passwordChars[1] = PickRandomCharacter(LowerCase);
        passwordChars[2] = PickRandomCharacter(Digits);
        passwordChars[3] = PickRandomCharacter(Symbols);

        for (var index = 4; index < Length; index++)
        {
            passwordChars[index] = PickRandomCharacter(AllCharacters);
        }

        Shuffle(passwordChars);

        return new string(passwordChars);
    }

    private static char PickRandomCharacter(string alphabet)
    {
        return alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
    }

    private static void Shuffle(char[] characters)
    {
        for (var index = characters.Length - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (characters[index], characters[swapIndex]) = (characters[swapIndex], characters[index]);
        }
    }
}
