using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;

namespace AldaJoyeros.Models;

public sealed class FeaturedProductsViewModel
{
    public List<(long Id, ProductoDto? Product)> Selected { get; set; } = new();
    public PagedResult<ProductoDto> Candidates { get; set; } = new();
    public string Search { get; set; } = "";
}
