using AldaJoyeros.DTOs;
using AldaJoyeros.Services.Promotions;

namespace AldaJoyeros.Models;
public sealed record PromotionsAdminViewModel(PromotionConfig Config, IReadOnlyList<ProductoDto> Products, IReadOnlyList<ProductoDto> Selected, IEnumerable<CategoriaDto> Categories, string Search, long? CategoryId, int Page, int TotalPages, int Total, int Audience, IReadOnlyList<PromotionCampaign> Campaigns);
