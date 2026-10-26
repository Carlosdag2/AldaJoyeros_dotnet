using AldaJoyeros.Data;
using AldaJoyeros.DTOs;
using AldaJoyeros.Entities;
using AldaJoyeros.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AldaJoyeros.Services;

public sealed class AdminDashboardService(AldaJoyerosContext sql)
{
    public async Task<AdminDashboardViewModel> GetAsync(CancellationToken cancellation = default)
    {
        var model = new AdminDashboardViewModel {
            TotalProductos = await sql.Productos.CountAsync(p=>!p.Eliminado, cancellation),
            TotalCategorias = await sql.Categorias.CountAsync(cancellation),
            TotalUsuarios = await sql.Usuarios.CountAsync(cancellation),
            ActualizadoEn = MetricsPeriod.MadridNow()
        };
        var states = await sql.Pedidos.AsNoTracking().GroupBy(p=>p.Estado)
            .Select(g=>new{Estado=g.Key,Total=g.Count()}).ToListAsync(cancellation);
        model.TotalPedidos = states.Sum(s=>s.Total);
        model.Pendientes = states.FirstOrDefault(s=>s.Estado==EstadoPedido.PENDIENTE)?.Total ?? 0;
        model.EnProceso = states.FirstOrDefault(s=>s.Estado==EstadoPedido.EN_PROCESO)?.Total ?? 0;
        model.Enviados = states.FirstOrDefault(s=>s.Estado==EstadoPedido.ENVIADO)?.Total ?? 0;
        model.UltimosPedidos = await sql.Pedidos.AsNoTracking().OrderByDescending(p=>p.Fecha).ThenByDescending(p=>p.Id)
            .Take(5).Select(p=>new AdminPedidoResumen { Id=p.Id,Fecha=p.Fecha,Estado=p.Estado,
                Total=p.LineasPedido.Select(l=>(double?)(l.Cantidad*l.Precio)).Sum() ?? 0 }).ToListAsync(cancellation);
        return model;
    }
}
