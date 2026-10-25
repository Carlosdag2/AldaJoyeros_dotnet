using System.Globalization;

namespace AldaJoyeros.Helpers;

// The range applies to the displayed catalog price, including the starting price of a size family.
public sealed record CatalogPriceRange(decimal? Min, decimal? Max)
{
    public string MinText => Min?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
    public string MaxText => Max?.ToString("0.##", CultureInfo.InvariantCulture) ?? "";
    public bool Contains(double price) => double.IsFinite(price) && price >= 0 && price <= (double)decimal.MaxValue
        && (!Min.HasValue || decimal.Round((decimal)price, 2) >= Min)
        && (!Max.HasValue || decimal.Round((decimal)price, 2) <= Max);

    public static bool TryCreate(string? min, string? max, out CatalogPriceRange range)
    {
        range = new(null, null);
        if (!Parse(min, out var lower) || !Parse(max, out var upper) || lower.HasValue && upper.HasValue && lower > upper) return false;
        range = new(lower, upper);
        return true;
    }
    private static bool Parse(string? text, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
            || number < 0 || decimal.Round(number, 2) != number) return false;
        value = number;
        return true;
    }
}
