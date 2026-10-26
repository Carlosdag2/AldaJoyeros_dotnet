using AldaJoyeros.DTOs;
using AldaJoyeros.Helpers;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Services.Implementations;

public sealed class TraficoService(TimeProvider? time = null) : ITraficoService
{
    public const int ActiveSeconds = 120;
    private readonly TimeProvider clock = time ?? TimeProvider.System;
    private readonly object gate = new();
    private readonly Dictionary<(string Visitor, string Tab), (DateTimeOffset Last, string Page, long Sequence, bool Open, bool Visible)> presence = new();
    private readonly Dictionary<string, DateTimeOffset> withdrawn = new();
    private readonly Queue<(DateTimeOffset Date, string Visitor)> views = new();
    private readonly SortedDictionary<DateTimeOffset, int> peaks = new();
    private readonly DateTimeOffset started = (time ?? TimeProvider.System).GetUtcNow();
    private DateTimeOffset cleaned = DateTimeOffset.MinValue;
    private const string NavigationTab = "navigation";
    private static bool Valid(string id) => Guid.TryParseExact(id, "N", out _);
    private static DateTimeOffset Minute(DateTimeOffset date) => new(date.Year, date.Month, date.Day, date.Hour, date.Minute, 0, TimeSpan.Zero);

    public void RegistrarVisita(string sessionId, string pagina)
    {
        if (!Valid(sessionId) || TrafficPages.Resolve(pagina) == null) return;
        lock (gate) {
            var now = clock.GetUtcNow(); Clean(now);
            if (withdrawn.ContainsKey(sessionId)) return;
            // A successful document is already confirmed activity, even before its script starts.
            if (!presence.Any(p=>p.Key.Visitor==sessionId && p.Value.Open && p.Value.Last>now.AddSeconds(-ActiveSeconds)) && presence.Count < 10000)
                presence[(sessionId, NavigationTab)] = (now, TrafficPages.Resolve(pagina)!, 0, true, false);
            views.Enqueue((now, sessionId));
            while (views.Count > 10000) views.Dequeue();
        }
    }
    public void RegistrarActividad(string visitante, string pestana, string pagina, long secuencia = 0, bool visible = true)
    {
        var section = TrafficPages.Resolve(pagina);
        if (!Valid(visitante) || !Valid(pestana) || section == null) return;
        lock (gate) {
            var now = clock.GetUtcNow(); Clean(now);
            if (presence.Count >= 10000 && !presence.ContainsKey((visitante, pestana))) return;
            if (withdrawn.ContainsKey(visitante)) return;
            if (presence.TryGetValue((visitante,pestana),out var previous) && secuencia > 0 && previous.Sequence >= secuencia) return;
            presence.Remove((visitante, NavigationTab));
            presence[(visitante, pestana)] = (now, section, secuencia, true, visible);
            Sample(now);
        }
    }
    public void CerrarPestana(string visitante, string pestana, long secuencia = 0) {
        if (!Valid(visitante) || !Valid(pestana)) return;
        lock (gate) {
            var now = clock.GetUtcNow(); Clean(now);
            if (presence.TryGetValue((visitante,pestana),out var previous) && secuencia > 0 && previous.Sequence >= secuencia) return;
            if (presence.Count >= 10000 && !presence.ContainsKey((visitante,pestana))) return;
            // Keep the close sequence briefly so a delayed older heartbeat cannot reopen the tab.
            presence.Remove((visitante, NavigationTab));
            presence[(visitante,pestana)] = (now, "", secuencia, false, false);
        }
    }
    public void RetirarConsentimiento(string visitante)
    {
        lock (gate) {
            withdrawn[visitante] = clock.GetUtcNow();
            if (withdrawn.Count > 10000) withdrawn.Remove(withdrawn.MinBy(p=>p.Value).Key);
            foreach (var key in presence.Keys.Where(k => k.Visitor == visitante).ToArray()) presence.Remove(key);
            var retained = views.Where(v => v.Visitor != visitante).ToArray(); views.Clear();
            foreach (var item in retained) views.Enqueue(item);
        }
    }
    private IEnumerable<(string Visitor, DateTimeOffset Last, string Page)> Active(DateTimeOffset now) => presence
        .Where(p => p.Value.Open && p.Value.Last > now.AddSeconds(-ActiveSeconds))
        .GroupBy(p => p.Key.Visitor).Select(g => g.OrderByDescending(p => p.Value.Visible).ThenByDescending(p => p.Value.Last).First())
        .Select(p => (p.Key.Visitor, p.Value.Last, p.Value.Page));
    private void Sample(DateTimeOffset now) {
        var minute = Minute(now); var count = Active(now).Count();
        peaks[minute] = Math.Max(peaks.GetValueOrDefault(minute), count);
    }
    private void Clean(DateTimeOffset now) {
        while (views.TryPeek(out var first) && first.Date <= now.AddMinutes(-30)) views.Dequeue();
        if (now - cleaned < TimeSpan.FromSeconds(30)) return;
        foreach (var key in presence.Where(p => p.Value.Last <= now.AddSeconds(-ActiveSeconds)).Select(p=>p.Key).ToArray()) presence.Remove(key);
        foreach (var key in peaks.Keys.Where(k=>k<Minute(now).AddMinutes(-14)).ToArray()) peaks.Remove(key);
        foreach (var key in withdrawn.Where(p=>p.Value<now.AddDays(-1)).Select(p=>p.Key).ToArray()) withdrawn.Remove(key);
        cleaned = now;
    }
    public TraficoDto ObtenerSnapshot()
    {
        lock (gate) {
            var now = clock.GetUtcNow(); Clean(now); Sample(now);
            var active = Active(now).ToArray();
            var pages = active.GroupBy(p=>p.Page).OrderByDescending(g=>g.Count()).ThenBy(g=>g.Key).ToDictionary(g=>g.Key,g=>g.Count());
            var history = Enumerable.Range(0,15).Select(i=>Minute(now).AddMinutes(i-14))
                .Select(t=>new TraficoMinutoDto(t, peaks.GetValueOrDefault(t))).ToArray();
            return new(active.Length, pages, views.Count, views.Select(v=>v.Visitor).Distinct().Count(), now, started, ActiveSeconds, history);
        }
    }
    public int ObtenerVisitantesActivos() => ObtenerSnapshot().VisitantesActivos;
    public Dictionary<string,int> ObtenerVisitantesPorPagina() => ObtenerSnapshot().VisitantesPorPagina;
    public void LimpiarSesionesExpiradas() { lock(gate) { cleaned=DateTimeOffset.MinValue; Clean(clock.GetUtcNow()); } }
}
