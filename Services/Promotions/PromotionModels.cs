using AldaJoyeros.DTOs;

namespace AldaJoyeros.Services.Promotions;
public sealed record DiscountRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public decimal Percent { get; set; }
    public bool Enabled { get; set; } = true;
    public long? CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public long? ProductId { get; set; }
    public string FamilyReference { get; set; } = "";
    public Dictionary<long, decimal> References { get; set; } = new();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public bool PreviousPricesConfirmed { get; set; }
}

public sealed record PromotionConfig
{
    public long Revision { get; set; }
    public List<DiscountRule> Rules { get; set; } = new();
    public bool HomeEnabled { get; set; }
    public List<long> HomeProducts { get; set; } = new();
    public bool PopupEnabled { get; set; }
    public long? PopupProductId { get; set; }
    public List<long> PopupPreviewProducts { get; set; } = new();
    public int PopupSeconds { get; set; } = 10;
    public string PopupVersion { get; set; } = Guid.NewGuid().ToString("N");
}

public sealed record PromotionPrice(decimal Final, decimal? Previous, decimal Percent)
{
    public decimal BasePrice { get; init; }
    public bool Discounted => Percent > 0 && Final < BasePrice;
}

public sealed record PromotionSelection(PromotionConfig Config, IReadOnlyList<ProductoDto> Products);
public sealed record PromotionEmailProduct(long ProductId, long? SizeId, string Name, string Url, string? Image, decimal Price, decimal Previous, decimal Percent) { public decimal? BasePrice {get;init;} }
public sealed record PromotionCampaign(string Id, string Subject, string BaseUrl, List<PromotionEmailProduct> Products, DateTime CreatedUtc, int Pending, int Sent, int Failed, int Skipped);
public sealed record PromotionDelivery(string CampaignId, long UserId, string Email, string Version, string Subject, string BaseUrl, List<PromotionEmailProduct> Products);
