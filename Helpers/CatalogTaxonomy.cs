using System.Globalization;
using System.Text;

namespace AldaJoyeros.Catalog;

public static class CatalogTaxonomy
{
    static string Key(string value) => string.Concat(value.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant().Trim();

    public static string Normalize(string name)
    {
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

    public static string Group(string name) => Normalize(name) switch
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
