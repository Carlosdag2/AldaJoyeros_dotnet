using AldaJoyeros.Helpers;
using AldaJoyeros.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AldaJoyeros.Controllers;

public sealed class TraficoController(ITraficoService traffic) : Controller
{
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(4096)]
    public IActionResult Latido(string? pestana, string? pagina, bool visible = true, long secuencia = 0, bool cerrar = false)
    {
        Response.Headers.CacheControl = "no-store";
        var visitor = Request.Cookies[VisitorConsent.VisitorCookie];
        if (!VisitorConsent.AllowsAnalytics(Request)) {
            if (visitor != null) traffic.RetirarConsentimiento(visitor);
            return NoContent();
        }
        if (TrafficPages.IsAutomated(Request) || !Guid.TryParseExact(visitor, "N", out _) || !Guid.TryParseExact(pestana, "N", out _)) return NoContent();
        if (secuencia < 1 || secuencia > 1_000_000_000) return NoContent();
        if (cerrar) { traffic.CerrarPestana(visitor!, pestana!, secuencia); return NoContent(); }
        if (pagina != null && Request.PathBase.HasValue && pagina.StartsWith(Request.PathBase.Value + "/", StringComparison.OrdinalIgnoreCase)) pagina = pagina[Request.PathBase.Value.Length..];
        if (TrafficPages.Resolve(pagina) == null) return NoContent();
        traffic.RegistrarActividad(visitor!, pestana!, pagina!, secuencia, visible);
        return NoContent();
    }
}
