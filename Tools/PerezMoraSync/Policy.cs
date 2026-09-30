namespace PerezMoraSync;

public sealed record ManagedProduct(long ProductId,string Reference,string Fingerprint,int Stock,bool Present,bool Hidden,DateTime? DeletedAt,
    decimal LastPrice,string LastDescription,long LastCategory,bool ManualVisibility,bool ManualPrice,bool ManualDescription,bool ManualCategory,string[] Images,bool Created);
public sealed record CurrentProduct(long Id,string Reference,string Description,decimal Price,long? Category,bool Hidden,DateTime? DeletedAt);
public sealed record Decision(bool Hidden,bool ManualVisibility,bool ManualPrice,bool ManualDescription,bool ManualCategory);
public static class Policy {
    public static Decision Decide(ManagedProduct old,CurrentProduct current,CatalogProduct? fresh) {
        var manualVisibility=old.ManualVisibility||old.Hidden!=current.Hidden||old.DeletedAt!=current.DeletedAt||!old.Created;
        return new(manualVisibility?current.Hidden:fresh==null||fresh.Stock==0||fresh.RequiresReview,
            manualVisibility,old.ManualPrice||old.LastPrice!=current.Price||!old.Created,
            old.ManualDescription||old.LastDescription!=current.Description||!old.Created,
            old.ManualCategory||old.LastCategory!=current.Category||!old.Created);
    }
}
