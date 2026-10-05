using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AldaJoyeros.Helpers;

public sealed class CatalogSearch
{
    private static readonly Regex Words = new("[a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex Karats = new(@"\b(9|10|14|18|22|24)\s*(?:quilates?|kilates?|karats?|carats?|kts?|ct|k)\b", RegexOptions.Compiled);
    private static readonly Regex Purity = new(@"\b(375|417|585|750|916|999)\s*(?:milesimas?|‰)(?=\W|$)", RegexOptions.Compiled);
    private static readonly HashSet<string> Stop = new("de del la las el los un una unos unas con para en y al".Split(' '));
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["anillos"]="anillo", ["sortija"]="anillo", ["sortijas"]="anillo", ["aros"]="aro", ["alianzas"]="alianza",
        ["pulseras"]="pulsera", ["brazalete"]="pulsera", ["brazaletes"]="pulsera", ["collares"]="collar", ["cadenas"]="cadena",
        ["colgantes"]="colgante", ["medallas"]="medalla", ["pendientes"]="pendiente", ["arete"]="pendiente", ["aretes"]="pendiente",
        ["zarcillo"]="pendiente", ["zarcillos"]="pendiente", ["relojes"]="reloj", ["diamantes"]="diamante", ["brillantes"]="brillante",
        ["circonitas"]="circonita", ["zirconita"]="circonita", ["zirconitas"]="circonita", ["perlas"]="perla",
        ["amarillos"]="amarillo", ["blancos"]="blanco", ["rosados"]="rosa", ["rosado"]="rosa",
        ["caballero"]="hombre", ["caballeros"]="hombre", ["masculino"]="hombre", ["senora"]="mujer", ["femenino"]="mujer"
    };
    private readonly string raw;
    private readonly string[] terms;
    public CatalogSearch(string query)
    {
        raw = Plain(query);
        terms = Tokens(query).Where(t => !Stop.Contains(t)).Distinct().ToArray();
    }
    public int Score(string name, string? description, string category, IEnumerable<string> references, IEnumerable<string> details)
    {
        if (terms.Length == 0) return 0;
        var refs = references.ToArray();
        var fields = new[] { (name, 80), (category, 60), (description ?? "", 35), (string.Join(' ', details), 25), (string.Join(' ', refs), 100) };
        var tokenFields = fields.Select(f => (Tokens(f.Item1, expandTypes: true), f.Item2)).ToArray();
        var score = 0;
        foreach (var term in terms)
        {
            var best = 0;
            foreach (var (tokens, weight) in tokenFields)
                foreach (var token in tokens)
                {
                    var match = token == term ? weight :
                        !term.Any(char.IsDigit) && term.Length >= 3 && token.StartsWith(term, StringComparison.Ordinal) ? weight / 2 :
                        !Aliases.Values.Contains(term) && !term.Any(char.IsDigit) && term.Length >= 5 && !token.Any(char.IsDigit) && OneEdit(term, token) ? weight / 3 : 0;
                    best = Math.Max(best, match);
                }
            if (best == 0) return ReferenceScore(refs);
            score += best;
        }
        if (Plain(name) == raw) score += 500;
        return score + ReferenceScore(refs);
    }
    private int ReferenceScore(string[] references)
    {
        var compact = string.Concat(Words.Matches(raw).Select(m => m.Value));
        if (compact.Length < 3) return 0;
        foreach (var reference in references)
        {
            var candidate = string.Concat(Words.Matches(Plain(reference)).Select(m => m.Value));
            if (candidate == compact) return 1000;
            if (candidate.StartsWith(compact, StringComparison.Ordinal)) return 150;
        }
        return 0;
    }
    private static string[] Tokens(string text, bool expandTypes = false)
    {
        var normalized = Plain(text);
        var goldContext = Regex.IsMatch(normalized, @"\b(?:oro|gold)\b");
        normalized = Karats.Replace(normalized, m =>
            Regex.IsMatch(m.Value, @"\b(?:ct|carats?)\b") && !goldContext ? m.Value : m.Groups[1].Value + "k");
        normalized = Purity.Replace(normalized, m => Equivalent(m.Groups[1].Value));
        // Las cifras de ley son unidades de oro, nunca referencias o medidas aisladas.
        normalized = Regex.Replace(normalized, @"\boro\s+(?:de\s+)?(?:ley\s+)?(375|417|585|750|916|999)\b", m => "oro " + Equivalent(m.Groups[1].Value));
        if (Regex.IsMatch(normalized, @"\boro\b"))
            normalized = Regex.Replace(normalized, @"\bley\s+(375|417|585|750|916|999)\b", m => Equivalent(m.Groups[1].Value));
        var tokens = Words.Matches(normalized).Select(m => Aliases.GetValueOrDefault(m.Value, m.Value)).ToList();
        if (expandTypes && tokens.Contains("alianza")) tokens.Add("anillo");
        return tokens.ToArray();
    }
    private static string Equivalent(string purity) => purity switch { "375"=>"9k", "417"=>"10k", "585"=>"14k", "750"=>"18k", "916"=>"22k", "999"=>"24k", _=>purity };
    private static string Plain(string text)
    {
        var result = new StringBuilder();
        foreach (var c in text.ToLowerInvariant().Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) result.Append(c);
        return result.ToString().Normalize(NormalizationForm.FormC);
    }
    private static bool OneEdit(string left, string right)
    {
        if (Math.Abs(left.Length-right.Length)>1) return false;
        var i=0; var j=0; var edits=0;
        while(i<left.Length && j<right.Length)
        {
            if(left[i]==right[j]) { i++; j++; continue; }
            if(++edits>1) return false;
            if(left.Length>=right.Length) i++;
            if(right.Length>=left.Length) j++;
        }
        return edits+(left.Length-i)+(right.Length-j)<=1;
    }
    public static IEnumerable<string> AttributeText(string json)
    {
        try { using var document = JsonDocument.Parse(json); return Values(document.RootElement).ToArray(); }
        catch (JsonException) { return Array.Empty<string>(); }
    }
    private static IEnumerable<string> Values(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => new[] { element.GetString() ?? "" },
        JsonValueKind.Array => element.EnumerateArray().SelectMany(Values),
        JsonValueKind.Object => element.EnumerateObject().SelectMany(p => Values(p.Value)),
        _ => Array.Empty<string>()
    };
}
