using Microsoft.EntityFrameworkCore;
using AldaJoyeros.Data;
using AldaJoyeros.DTOs;
using AldaJoyeros.Entities;
using AldaJoyeros.Services.Interfaces;

namespace AldaJoyeros.Services.Implementations
{
    public class MetricasService : IMetricasService
    {
        private readonly AldaJoyerosContext _context;
        private readonly bool _optimized;

        public MetricasService(AldaJoyerosContext context, Microsoft.Extensions.Options.IOptions<AldaJoyeros.Configuration.StorageOptimizationOptions>? optimization = null)
        {
            _context = context; _optimized = optimization?.Value.Enabled ?? true;
        }

        public async Task<MetricasDto> GetMetricasAsync(int dias = 30)
        {
            var ahora = AldaJoyeros.Helpers.MetricsPeriod.MadridNow();
            var periodo = AldaJoyeros.Helpers.MetricsPeriod.Create(ahora.Date, dias);
            var hace7Dias = ahora.Date.AddDays(-6);
            var hace30Dias = ahora.Date.AddDays(-29);
            var inicioHoy = ahora.Date;

            var metricas = new MetricasDto { DiasPeriodo = periodo.Days, InicioPeriodo = periodo.Start, FinPeriodo = ahora.Date, GeneradasEn = ahora };

            // Métricas generales - consultas simples
            var totals = _optimized ? await StoreDatabaseTotals.ReadAsync(_context) : null;
            metricas.TotalProductos = totals?.Productos ?? await _context.Productos.CountAsync(p => !p.Eliminado);
            metricas.TotalCategorias = totals?.Categorias ?? await _context.Categorias.CountAsync();
            metricas.TotalUsuarios = totals?.Usuarios ?? await _context.Usuarios.CountAsync();

            // Pedidos por estado - consultas optimizadas
            var estados = await _context.Pedidos.AsNoTracking().GroupBy(p => p.Estado).Select(g => new { Estado = g.Key, Total = g.Count() }).ToListAsync();
            metricas.TotalPedidos = estados.Sum(s=>s.Total);
            int Count(EstadoPedido status) => estados.FirstOrDefault(g=>g.Estado==status)?.Total ?? 0;
            metricas.PedidosPendientes = Count(EstadoPedido.PENDIENTE);
            metricas.PedidosEnProceso = Count(EstadoPedido.EN_PROCESO);
            metricas.PedidosEnviados = Count(EstadoPedido.ENVIADO);
            metricas.PedidosEntregados = Count(EstadoPedido.ENTREGADO);
            metricas.PedidosCancelados = Count(EstadoPedido.CANCELADO);

            // Pedidos por período
            if (_optimized)
            {
                var periods = await _context.Pedidos.GroupBy(p => 1).Select(g => new {
                    Today = g.Count(p => p.Fecha >= inicioHoy && p.Fecha < periodo.End),
                    Week = g.Count(p => p.Fecha >= hace7Dias && p.Fecha < periodo.End),
                    Month = g.Count(p => p.Fecha >= hace30Dias && p.Fecha < periodo.End)
                }).SingleOrDefaultAsync();
                metricas.PedidosHoy = periods?.Today ?? 0;
                metricas.PedidosUltimos7Dias = periods?.Week ?? 0;
                metricas.PedidosUltimos30Dias = periods?.Month ?? 0;
                var sales = await _context.LineasPedido.Where(l => l.Pedido != null && l.Pedido.Estado != EstadoPedido.CANCELADO)
                    .GroupBy(l => 1).Select(g => new {
                        Total = g.Sum(l => l.Cantidad * l.Precio),
                        Today = g.Sum(l => l.Pedido!.Fecha >= inicioHoy && l.Pedido.Fecha < periodo.End ? l.Cantidad * l.Precio : 0d),
                        Week = g.Sum(l => l.Pedido!.Fecha >= hace7Dias && l.Pedido.Fecha < periodo.End ? l.Cantidad * l.Precio : 0d),
                        Month = g.Sum(l => l.Pedido!.Fecha >= hace30Dias && l.Pedido.Fecha < periodo.End ? l.Cantidad * l.Precio : 0d)
                    }).SingleOrDefaultAsync();
                metricas.VentasTotales = sales?.Total ?? 0;
                metricas.VentasHoy = sales?.Today ?? 0;
                metricas.VentasUltimos7Dias = sales?.Week ?? 0;
                metricas.VentasUltimos30Dias = sales?.Month ?? 0;
            }
            else
            {
            metricas.PedidosHoy = await _context.Pedidos.CountAsync(p => p.Fecha >= inicioHoy && p.Fecha < periodo.End);
            metricas.PedidosUltimos7Dias = await _context.Pedidos.CountAsync(p => p.Fecha >= hace7Dias && p.Fecha < periodo.End);
            metricas.PedidosUltimos30Dias = await _context.Pedidos.CountAsync(p => p.Fecha >= hace30Dias && p.Fecha < periodo.End);

            // Ventas - usando proyección para evitar cargar entidades completas
            var ventasQuery = _context.Pedidos
                .Where(p => p.Estado != EstadoPedido.CANCELADO)
                .SelectMany(p => p.LineasPedido)
                .Select(lp => lp.Cantidad * lp.Precio);

            metricas.VentasTotales = await ventasQuery.SumAsync();

            metricas.VentasUltimos30Dias = await _context.Pedidos
                .Where(p => p.Estado != EstadoPedido.CANCELADO && p.Fecha >= hace30Dias && p.Fecha < periodo.End)
                .SelectMany(p => p.LineasPedido)
                .SumAsync(lp => lp.Cantidad * lp.Precio);

            metricas.VentasUltimos7Dias = await _context.Pedidos
                .Where(p => p.Estado != EstadoPedido.CANCELADO && p.Fecha >= hace7Dias && p.Fecha < periodo.End)
                .SelectMany(p => p.LineasPedido)
                .SumAsync(lp => lp.Cantidad * lp.Precio);

            metricas.VentasHoy = await _context.Pedidos
                .Where(p => p.Estado != EstadoPedido.CANCELADO && p.Fecha >= inicioHoy && p.Fecha < periodo.End)
                .SelectMany(p => p.LineasPedido)
                .SumAsync(lp => lp.Cantidad * lp.Precio);

            }

            // Productos más vendidos - consulta optimizada
            metricas.ProductosMasVendidos = await _context.LineasPedido
                .Where(lp => lp.Pedido != null && lp.Pedido.Fecha >= periodo.Start && lp.Pedido.Fecha < periodo.End && lp.Pedido.Estado != EstadoPedido.CANCELADO && lp.Producto != null)
                .GroupBy(lp => new { lp.ProductoId, lp.Producto!.Nombre, CategoriaNombre = lp.Producto.Categoria!.Nombre })
                .Select(g => new ProductoVendidoDto
                {
                    Id = g.Key.ProductoId ?? 0,
                    Nombre = g.Key.Nombre,
                    CategoriaNombre = g.Key.CategoriaNombre ?? "Sin categoría",
                    CantidadVendida = g.Sum(lp => lp.Cantidad),
                    TotalVentas = g.Sum(lp => lp.Cantidad * lp.Precio)
                })
                .OrderByDescending(p => p.CantidadVendida)
                .Take(10)
                .ToListAsync();

            // Categorías más populares
            metricas.CategoriasMasPopulares = await _context.LineasPedido
                .Where(lp => lp.Pedido != null && lp.Pedido.Fecha >= periodo.Start && lp.Pedido.Fecha < periodo.End && lp.Pedido.Estado != EstadoPedido.CANCELADO && lp.Producto != null && lp.Producto.Categoria != null)
                .GroupBy(lp => new { lp.Producto!.CategoriaId, lp.Producto.Categoria!.Nombre })
                .Select(g => new CategoriaPopularDto
                {
                    Id = g.Key.CategoriaId ?? 0,
                    Nombre = g.Key.Nombre,
                    CantidadVendida = g.Sum(lp => lp.Cantidad),
                    TotalVentas = g.Sum(lp => lp.Cantidad * lp.Precio),
                    NumeroProductos = g.Select(lp => lp.ProductoId).Distinct().Count()
                })
                .OrderByDescending(c => c.TotalVentas)
                .Take(5)
                .ToListAsync();

            // Usuarios activos
            metricas.UsuariosActivos = await _context.Pedidos
                .Where(p => p.UsuarioId != null && p.Estado != EstadoPedido.CANCELADO)
                .Select(p => p.UsuarioId)
                .Distinct()
                .CountAsync();

            // Ventas por día (últimos 30 días) - optimizado
            var ventasPorDiaData = await _context.Pedidos
                .Where(p => p.Estado != EstadoPedido.CANCELADO && p.Fecha >= periodo.Start && p.Fecha < periodo.End)
                .GroupBy(p => p.Fecha!.Value.Date)
                .Select(g => new { Fecha = g.Key, Total = g.SelectMany(p => p.LineasPedido).Sum(lp => lp.Cantidad * lp.Precio) })
                .ToListAsync();

            metricas.VentasPorDia = Enumerable.Range(0, periodo.Days)
                .Select(i => periodo.Start.AddDays(i))
                .Select(fecha => new VentaDiariaDto
                {
                    Fecha = fecha,
                    Total = ventasPorDiaData.FirstOrDefault(v => v.Fecha == fecha)?.Total ?? 0
                })
                .ToList();

            // Pedidos por día (últimos 30 días) - optimizado
            var pedidosPorDiaData = await _context.Pedidos
                .Where(p => p.Fecha >= periodo.Start && p.Fecha < periodo.End)
                .GroupBy(p => p.Fecha!.Value.Date)
                .Select(g => new { Fecha = g.Key, Cantidad = g.Count() })
                .ToListAsync();

            metricas.PedidosPorDia = Enumerable.Range(0, periodo.Days)
                .Select(i => periodo.Start.AddDays(i))
                .Select(fecha => new PedidoDiarioDto
                {
                    Fecha = fecha,
                    Cantidad = pedidosPorDiaData.FirstOrDefault(p => p.Fecha == fecha)?.Cantidad ?? 0
                })
                .ToList();

            // Mejores clientes
            metricas.MejoresClientes = await _context.Pedidos
                .Where(p => p.Estado != EstadoPedido.CANCELADO && p.Fecha >= periodo.Start && p.Fecha < periodo.End && p.Usuario != null)
                .GroupBy(p => new { p.UsuarioId, p.Usuario!.Email })
                .Select(g => new ClienteTopDto
                {
                    Id = g.Key.UsuarioId ?? 0,
                    Email = g.Key.Email,
                    TotalPedidos = g.Count(),
                    TotalGastado = g.SelectMany(p => p.LineasPedido).Sum(lp => lp.Cantidad * lp.Precio)
                })
                .OrderByDescending(c => c.TotalGastado)
                .Take(10)
                .ToListAsync();

            // Ticket promedio
            var totalPedidosValidos = metricas.TotalPedidos - metricas.PedidosCancelados;
            if (totalPedidosValidos > 0)
            {
                metricas.TicketPromedio = metricas.VentasTotales / totalPedidosValidos;
            }

            // Tasa de pedidos completados
            if (metricas.TotalPedidos > 0)
            {
                metricas.TasaCompletados = (double)metricas.PedidosEntregados / metricas.TotalPedidos * 100;
            }

            metricas.VentasPeriodo = metricas.VentasPorDia.Sum(v=>v.Total);
            metricas.PedidosPeriodo = metricas.PedidosPorDia.Sum(v=>v.Cantidad);
            var pedidosValidosPeriodo = await _context.Pedidos.CountAsync(p=>p.Fecha>=periodo.Start && p.Fecha<periodo.End && p.Estado != EstadoPedido.CANCELADO);
            metricas.TicketPeriodo = pedidosValidosPeriodo > 0 ? metricas.VentasPeriodo / pedidosValidosPeriodo : 0;
            return metricas;
        }
    }
}
