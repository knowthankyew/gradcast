namespace GradCast.Data;

/// <summary>
/// Normalizes CIP codes to the four-digit category used by GradCast program data.
/// For example, both "11.0701" and "1107" become "1107".
/// </summary>
public static class CipCode
{
    public static string? NormalizeToFourDigit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var digits = value
            .Where(character => character is >= '0' and <= '9')
            .Take(4)
            .ToArray();

        return digits.Length == 4 ? new string(digits) : null;
    }
}
