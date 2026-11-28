namespace Diagnostic.Presentation;

public static class VinValidator
{
    /// <summary>
    /// Format check only: 17 characters, letters/digits, no I, O or Q (ISO 3779).
    /// The check digit is not verified because it is mandatory only for some markets.
    /// </summary>
    public static bool IsWellFormed(string? vin)
    {
        if (vin is null || vin.Length != 17)
        {
            return false;
        }

        foreach (var c in vin)
        {
            var upper = char.ToUpperInvariant(c);
            var isLetterOrDigit = upper is (>= 'A' and <= 'Z') or (>= '0' and <= '9');
            if (!isLetterOrDigit || upper is 'I' or 'O' or 'Q')
            {
                return false;
            }
        }

        return true;
    }
}
