namespace AldaJoyeros.Services.Interfaces;

public interface ICartCatalog
{
    Task<bool> IsPurchasableAsync(long id);
}
