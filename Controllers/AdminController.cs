using Microsoft.AspNetCore.Mvc;
using AldaJoyeros.Services;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.Attributes;

namespace AldaJoyeros.Controllers;

[AdminOnly]
public class AdminController(AdminDashboardService dashboard, IMetricasService metricas, ITraficoService traffic) : BaseController
{
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index() => View(await dashboard.GetAsync(HttpContext.RequestAborted));

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Metricas(int dias = 30)
    {
        var model = await metricas.GetMetricasAsync(dias);
        ViewBag.Trafico = traffic.ObtenerSnapshot();
        return View(model);
    }

    [HttpGet]
    public IActionResult ObtenerTrafico()
    {
        Response.Headers.CacheControl = "no-store, no-cache";
        return Json(traffic.ObtenerSnapshot());
    }
}
