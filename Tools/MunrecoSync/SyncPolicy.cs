using System.Text.Json.Nodes;

record ManagedState(bool Hidden,DateTime? DeletedAt,bool Manual,string Reason,bool Present=true);
record Visibility(bool Hidden,bool Manual,string Reason);
static class SyncPolicy {
    public static Visibility Decide(bool created,bool hidden,DateTime? deletedAt,ManagedState previous,bool present,int quantity,bool review) {
        if(!created)return new(hidden,true,"catalogo_propio");
        if(previous.Manual || previous.Hidden!=hidden || previous.DeletedAt!=deletedAt)
            return new(hidden,true,"decision_manual");
        if(!present)return new(true,false,"retirado_proveedor");
        if(review)return new(true,false,"datos_incompletos");
        return quantity>0?new(false,false,"disponible"):new(true,false,"sin_existencias");
    }
    public static ManagedState Bootstrap(bool created,bool hidden,DateTime? deletedAt,int? oldQuantity,bool review,DateTime importedAt,DateTime? initialVerifiedAt=null) {
        var expectedHidden=review || !oldQuantity.HasValue || oldQuantity<=0;
        var verifiedInitial=initialVerifiedAt.HasValue && deletedAt.HasValue && deletedAt<=initialVerifiedAt && importedAt<=initialVerifiedAt;
        var manual=!created || hidden!=expectedHidden || (hidden && !verifiedInitial && (!deletedAt.HasValue || Math.Abs((importedAt-deletedAt.Value).TotalSeconds)>3));
        return new(hidden,deletedAt,manual,manual?"decision_manual":review?"datos_incompletos":expectedHidden?"sin_existencias":"disponible");
    }
    public static int Quantity(JsonObject raw) {
        if(!int.TryParse(raw["quantity"]?.ToString(),out var quantity)||quantity<0)
            throw new InvalidOperationException("Cantidad invalida en el catalogo; no se aplican cambios");
        return quantity;
    }
    public static JsonObject DetailComparable(JsonObject source) {
        var result=source.DeepClone().AsObject();
        foreach(var key in new[]{"quantity","children_quantity","has_stock","availability","stock_status","button"})result.Remove(key);
        return result;
    }
    public static void ValidateCoverage(JsonArray fresh,JsonObject coverage,IEnumerable<(string Source,string Section)> previous) {
        if(fresh.Count==0 || coverage["products"]?.GetValue<int>()!=fresh.Count)
            throw new InvalidOperationException("Catalogo vacio o incompleto; no se aplican cambios");
        var ids=new HashSet<string>();var references=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in fresh) {
            var p=item!.AsObject();Quantity(p);
            if(!ids.Add(p["product_id"]!.ToString())||!references.Add(p["model"]!.ToString()))
                throw new InvalidOperationException("Referencias repetidas en el catalogo");
        }
        var sections=coverage["sections"]!.AsArray();
        if(sections.Count!=CatalogCrawler.Sections.Length)throw new InvalidOperationException("Faltan secciones del catalogo");
        foreach(var section in CatalogCrawler.Sections) {
            var c=sections.SingleOrDefault(x=>x!["Id"]?.ToString()==section.Id);
            var current=fresh.Where(x=>x!["_source_section"]!.ToString()==section.Id).Select(x=>x!["product_id"]!.ToString()).ToHashSet();
            if(c==null || current.Count==0 || current.Count!=c["products"]!.GetValue<int>() || c["pages"]!.GetValue<int>()<1)
                throw new InvalidOperationException("Seccion vacia o incompleta: "+section.Name);
            var prior=previous.Where(x=>x.Section==section.Id).Select(x=>x.Source).Distinct().ToArray();
            if(prior.Length>0 && prior.Count(x=>!current.Contains(x))>Math.Max(5,prior.Length*0.15))
                throw new InvalidOperationException("Retirada inusual superior al 15% en "+section.Name+"; requiere revision manual");
        }
    }
}
