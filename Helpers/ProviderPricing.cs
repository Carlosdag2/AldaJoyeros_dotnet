using System.Globalization;
using System.Text.RegularExpressions;

namespace AldaJoyeros.Catalog;

public static class ProviderPricing
{
    public static bool TryCoefficient(string? input,out decimal coefficient)
    {
        coefficient=0;
        return input!=null && Regex.IsMatch(input.Trim(),@"^\d{1,3}([.,]\d{1,4})?$") &&
            decimal.TryParse(input.Trim().Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out coefficient) && coefficient is >=0.01m and <=100m;
    }
    public static decimal Pvp(decimal cost,decimal coefficient)
    {
        if(cost<0 || coefficient is <0.01m or >100m)throw new ArgumentOutOfRangeException(nameof(coefficient));
        var value=decimal.Round(cost*coefficient,2,MidpointRounding.AwayFromZero);
        if(value>9999999999.99m)throw new InvalidDataException("El PVP calculado supera el precio máximo permitido.");
        return value;
    }
}
