namespace AldaJoyeros.Services.Interfaces;

public interface ICartCatalog
{
    Task<bool> IsPurchasableAsync(long id);
    Task<CartCatalogPrice?> PriceAsync(long id) => Task.FromResult<CartCatalogPrice?>(null);
}

public sealed record CartCatalogPrice(double Price,double? Previous,decimal Percent) {public double? BasePrice {get;init;}}
