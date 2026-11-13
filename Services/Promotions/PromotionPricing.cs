using AldaJoyeros.DTOs;
using AldaJoyeros.Entities;
using AldaJoyeros.Helpers;

namespace AldaJoyeros.Services.Promotions;
public sealed class PromotionPricing(PromotionStore store)
{
    Task<PromotionConfig>? state;
    public Task<PromotionConfig> ConfigAsync() => state ??= store.GetAsync();
    public Task ObserveAsync(IEnumerable<Producto> products, PromotionConfig config) => store.ObserveAsync(products.Select(p => (p.Id, Calculate(p, config).Final)));
    public static PromotionPrice Calculate(Producto p, PromotionConfig config)
    {
        var basePrice = decimal.Round((decimal)p.Precio, 2, MidpointRounding.AwayFromZero);
        var best = new PromotionPrice(basePrice, null, 0) { BasePrice = basePrice };
        foreach (var rule in config.Rules.Where(r => r.Enabled && r.Percent > 0 && r.Percent < 100))
        {
            var match = rule.CategoryId.HasValue && (p.CategoriaId == rule.CategoryId || !rule.CategoryName.Contains(" · ") && p.Categoria?.Nombre.StartsWith(rule.CategoryName + " · ", StringComparison.Ordinal) == true) || rule.ProductId == p.Id || rule.FamilyReference.Length > 0 && (ProductFamilies.Parse(p)?.Parent.Equals(rule.FamilyReference, StringComparison.OrdinalIgnoreCase) == true);
            if (!match)
                continue;
            var reference = Math.Min(basePrice, rule.References.GetValueOrDefault(p.Id, basePrice));
            var final = decimal.Round(basePrice * (1 - rule.Percent / 100m), 2, MidpointRounding.AwayFromZero);
            if (final >= 0.01m && final < best.Final)
                best = new(final, reference, rule.Percent) { BasePrice = basePrice };
        }

        return best;
    }

    public static void Apply(ProductoDto dto, Producto p, PromotionConfig state)
    {
        var price = Calculate(p, state);
        dto.Precio = (double)price.Final;
        dto.PrecioAnterior = price.Previous.HasValue ? (double)price.Previous.Value : null;
        dto.DescuentoPorcentaje = price.Percent;
        dto.PrecioSinDescuento = price.Discounted ? (double)price.BasePrice : null;
    }
}
