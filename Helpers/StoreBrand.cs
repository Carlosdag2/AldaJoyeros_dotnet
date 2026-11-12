using System.Text.RegularExpressions;

namespace AldaJoyeros.Helpers;

public static class StoreBrand
{
    public const string Name = "Alda Joyeros 1962";
    // Normalize older configured brand names without changing unrelated sender/company names.
    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Name;
        var name = value.Trim();
        if (name.Equals("Alda", StringComparison.OrdinalIgnoreCase)) return Name;
        return Regex.Replace(name, @"\balda\s*joyeros(?:\s*(?:[·-]\s*)?(?:desde\s+)?196[28])?\b", Name,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
