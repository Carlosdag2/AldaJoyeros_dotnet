using System.Globalization;
using System.Text;

namespace AldaJoyeros.Catalog;

public static class CatalogTaxonomy
{
    static string Key(string value) => string.Concat(value.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant().Trim();

    public static string Normalize(string name)
    {
        if (name.Contains(" · ")) return name.Trim();
        var key = Key(name);
        if (key == "smartwatch" || key == "smartwatches") return "Smartwatches";
        if (key.StartsWith("reloj bolsillo") || key == "relojes de bolsillo") return "Relojes de bolsillo";
        if (key.StartsWith("reloj")) return "Relojes";
        if (key.StartsWith("anillo") || key == "mujer anillos" || key == "sellos y solitarios") return "Anillos";
        if (key.StartsWith("pendiente") && !key.Contains("revisar") || key == "mujer pendientes") return "Pendientes";
        if (key.StartsWith("colgante") || key == "mujer colgantes") return "Colgantes";
        if (key.StartsWith("cadena")) return "Cadenas";
        if (key.StartsWith("alianza")) return "Alianzas";
        if (key.StartsWith("broche")) return "Broches";
        if (key.StartsWith("piercing")) return "Piercings";
        if (key.StartsWith("pulsera") || key == "mujer pulseras") return "Pulseras";
        if (key.StartsWith("collar") || key == "mujer gargantillas y collares") return "Collares";
        if (key.StartsWith("abalorio")) return "Abalorios";
        if (key.StartsWith("tobillera")) return "Tobilleras";
        if (key.StartsWith("llavero")) return "Llaveros";
        if (key.StartsWith("gemelos")) return "Gemelos y pisacorbatas";
        if (key.StartsWith("medalla")) return "Medallas";
        return key switch
        {
            "mujer juegos" => "Conjuntos",
            "complementos acero" => "Otros complementos",
            "hombre" => "Joyas para hombre",
            "amor y madre" => "Amor y Día de la Madre",
            "9 kilates" => "Oro de 9 quilates",
            "diamantes" => "Joyas con diamantes",
            "colecciones" => "Otras colecciones",
            _ => name.Trim()
        };
    }

    public static string Family(string name) => name == "Pérez Mora · Pendientes de revisar" ? name : name.Split(" · ")[0];

    public static string Specific(string category, string description, string metal = "", string subtype = "")
    {
        var family = Normalize(Family(category));
        if (Group(family) is "Pendientes de organizar" or "Colecciones especiales" || family is "Smartwatches" or "Relojes" or "Relojes de bolsillo") return family;
        var text = Key(description);
        bool Has(string value, string pattern) => System.Text.RegularExpressions.Regex.IsMatch(value, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        string Material(string value)
        {
            if (Has(value, @"\bplata\b")) return "Plata";
            if (Has(value, @"\bacero\b")) return "Acero";
            if (Has(value, @"\btitanio\b")) return "Titanio";
            if (Has(value, @"\b(?:chapad[oa]|banad[oa]|bano)\b")) return "";
            var karats = System.Text.RegularExpressions.Regex.Match(value, @"\b(9|14|18|24)\s*(?:k(?:t)?|quilates?|kilates?)\b");
            if (karats.Success) return "Oro de " + karats.Groups[1].Value + " quilates";
            if (Has(value, @"\boro\b")) return "Oro";
            if (Has(value, @"\bpiel\b")) return "Piel";
            return "";
        }
        var material = Material(Key(metal));
        if (material.Length == 0) material = Material(text);
        if (material.Length == 0 && family == "Oro de 9 quilates") material = family;
        var typeText = string.IsNullOrWhiteSpace(subtype) ? text : Key(subtype) + " " + text;
        string? kind = family switch
        {
            "Pendientes" when Has(typeText, @"\b(?:aros?|criollas?)\b") => "Aros",
            "Pendientes" when Has(typeText, @"\b(?:largos?|colgantes?)\b") => "Largos",
            "Pendientes" when Has(typeText, @"\bboton(?:es)?\b") => "Botón",
            "Anillos" when Has(text, @"\bsolitarios?\b") => "Solitarios",
            "Anillos" when Has(text, @"\bsellos?\b") => "Sellos",
            "Anillos" when Has(text, @"\btresillos?\b") => "Tresillos",
            "Pulseras" when Has(text, @"\besclavas?\b") => "Esclavas",
            "Pulseras" when Has(text, @"\brigid[oa]s?\b") => "Rígidas",
            _ => null
        };
        var parts = new[] { family, kind, material }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct();
        return string.Join(" · ", parts);
    }

    public static string Group(string name) => Normalize(Family(name)) switch
    {
        "Relojes" or "Smartwatches" or "Relojes de bolsillo" => "Relojería",
        "Gemelos y pisacorbatas" or "Llaveros" or "Otros complementos" or "Fornituras" or "Insignias profesionales" or "Placas" => "Complementos",
        "Bebé" or "Comunión e infantil" or "Boda y compromiso" or "Amor y Día de la Madre" or "Joyas con foto" or "Nombres y letras" or "Oro de 9 quilates" or "Joyas con diamantes" or "Joyas para hombre" or "Otras colecciones" or "Oro" => "Colecciones especiales",
        "Sin categoría" or "Pérez Mora · Pendientes de revisar" => "Pendientes de organizar",
        _ => "Joyas"
    };

    public static int GroupOrder(string group) => group switch
    {
        "Joyas" => 0, "Relojería" => 1, "Complementos" => 2, "Colecciones especiales" => 3, _ => 4
    };
}
