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
        private readonly IProductoImagenService _imagenService;

        public MetricasService(AldaJoyerosContext context, IProductoImagenService imagenService)
        {
            _context = context;
            _imagenService = imagenService;
        }

        public async Task<MetricasDto> GetMetricasAsync()
        {
            var ahora = DateTime.Now;
            var hace7Dias = ahora.AddDays(-7);
            var hace30Dias = ahora.AddDays(-30);
            var inicioHoy = ahora.Date;

            var metricas = new MetricasDto();

            // Métricas generales - consultas simples
            metricas.TotalProductos = await _context.Productos.CountAsync(p => !p.Eliminado);
            metricas.TotalCategorias = await _context.Categorias.CountAsync();
            metricas.TotalPedidos = await _context.Pedidos.CountAsync();
            metricas.TotalUsuarios = await _context.Usuarios.CountAsync();

            // Pedidos por estado - consultas optimizadas
            metricas.PedidosPendientes = await _context.Pedidos.CountAsync(p => p.Estado == EstadoPedido.PENDIENTE);
            metricas.PedidosEnProceso = await _context.Pedidos.CountAsync(p => p.Estado == EstadoPedido.EN_PROCESO);
            metricas.PedidosEnviados = await _context.Pedidos.CountAsync(p => p.Estado == EstadoPedido.ENVIADO);
            metricas.PedidosEntregados = await _context.Pedidos.CountAsync(p => p.Estado == EstadoPedido.ENTREGADO);
            metricas.PedidosCancelados = await _context.Pedidos.CountAsync(p => p.Estado == EstadoPedido.CANCELADO);

            // Pedidos por período
            metricas.PedidosHoy = await _context.Pedidos.CountAsync(p => p.Fecha >= inicioHoy);
            metricas.PedidosUltimos7Dias = await _context.Pedidos.CountAsync(p => p.Fecha >= hace7Dias);
            metricas.PedidosUltimos30Dias = await _context.Pedidos.CountAsync(p => p.Fecha >= hace30Dias);

            // Ventas - usando proyección para evitar cargar entidades completas
            var ventasQuery = _context.Pedidos
                .Where(p => p.Estado != EstadoPedido.CANCELADO)
                .SelectMany(p => p.LineasPedido)
                .Select(lp => lp.Cantidad * lp.Precio);

            metricas.VentasTotales = await ventasQuery.SumAsync();

            metricas.VentasUltimos30Dias = await _context.Pedidos
                .Where(p => p.Estado != EstadoPedido.CANCELADO && p.Fecha >= hace30Dias)
                .SelectMany(p => p.LineasPedido)
                .SumAsync(lp => lp.Cantidad * lp.Precio);

            metricas.VentasUltimos7Dias = await _context.Pedidos
                .Where(p => p.Estado != EstadoPedido.CANCELADO && p.Fecha >= hace7Dias)
                .SelectMany(p => p.LineasPedido)
                .SumAsync(lp => lp.Cantidad * lp.Precio);

            metricas.VentasHoy = await _context.Pedidos
                .Where(p => p.Estado != EstadoPedido.CANCELADO && p.Fecha >= inicioHoy)
                .SelectMany(p => p.LineasPedido)
                .SumAsync(lp => lp.Cantidad * lp.Precio);

            // Productos más vendidos - consulta optimizada
            metricas.ProductosMasVendidos = await _context.LineasPedido
                .Where(lp => lp.Pedido != null && lp.Pedido.Estado != EstadoPedido.CANCELADO && lp.Producto != null)
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

            // Obtener imágenes para los productos más vendidos
            foreach (var producto in metricas.ProductosMasVendidos)
            {
                try
                {
                    var imagenes = await _imagenService.GetByProductoIdAsync(producto.Id);
                    var imagenPrincipal = imagenes.FirstOrDefault(i => i.EsPrincipal) ?? imagenes.FirstOrDefault();
                    producto.ImagenPrincipal = imagenPrincipal?.ImagenBase64;
                }
                catch
                {
                    // Si falla obtener imagen, continuar sin ella
                    producto.ImagenPrincipal = null;
                }
            }

            // Categorías más populares
            metricas.CategoriasMasPopulares = await _context.LineasPedido
                .Where(lp => lp.Pedido != null && lp.Pedido.Estado != EstadoPedido.CANCELADO && lp.Producto != null && lp.Producto.Categoria != null)
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
                .Where(p => p.UsuarioId != null)
                .Select(p => p.UsuarioId)
                .Distinct()
                .CountAsync();

            // Ventas por día (últimos 30 días) - optimizado
            var ventasPorDiaData = await _context.Pedidos
                .Where(p => p.Estado != EstadoPedido.CANCELADO && p.Fecha >= hace30Dias)
                .GroupBy(p => p.Fecha!.Value.Date)
                .Select(g => new { Fecha = g.Key, Total = g.SelectMany(p => p.LineasPedido).Sum(lp => lp.Cantidad * lp.Precio) })
                .ToListAsync();

            metricas.VentasPorDia = Enumerable.Range(0, 30)
                .Select(i => hace30Dias.AddDays(i).Date)
                .Select(fecha => new VentaDiariaDto
                {
                    Fecha = fecha,
                    Total = ventasPorDiaData.FirstOrDefault(v => v.Fecha == fecha)?.Total ?? 0
                })
                .ToList();

            // Pedidos por día (últimos 30 días) - optimizado
            var pedidosPorDiaData = await _context.Pedidos
                .Where(p => p.Fecha >= hace30Dias)
                .GroupBy(p => p.Fecha!.Value.Date)
                .Select(g => new { Fecha = g.Key, Cantidad = g.Count() })
                .ToListAsync();

            metricas.PedidosPorDia = Enumerable.Range(0, 30)
                .Select(i => hace30Dias.AddDays(i).Date)
                .Select(fecha => new PedidoDiarioDto
                {
                    Fecha = fecha,
                    Cantidad = pedidosPorDiaData.FirstOrDefault(p => p.Fecha == fecha)?.Cantidad ?? 0
                })
                .ToList();

            // Mejores clientes
            metricas.MejoresClientes = await _context.Pedidos
                .Where(p => p.Estado != EstadoPedido.CANCELADO && p.Usuario != null)
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

            return metricas;
        }
    }
}
