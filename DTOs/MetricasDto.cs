namespace AldaJoyeros.DTOs
{
    public class MetricasDto
    {
        // Métricas generales
        public int TotalProductos { get; set; }
        public int TotalCategorias { get; set; }
        public int TotalPedidos { get; set; }
        public int TotalUsuarios { get; set; }
        
        // Ventas
        public double VentasTotales { get; set; }
        public double VentasUltimos30Dias { get; set; }
        public double VentasUltimos7Dias { get; set; }
        public double VentasHoy { get; set; }
        public int PedidosHoy { get; set; }
        public int PedidosUltimos7Dias { get; set; }
        public int PedidosUltimos30Dias { get; set; }
        
        // Pedidos por estado
        public int PedidosPendientes { get; set; }
        public int PedidosEnProceso { get; set; }
        public int PedidosEnviados { get; set; }
        public int PedidosEntregados { get; set; }
        public int PedidosCancelados { get; set; }
        
        // Productos más vendidos
        public List<ProductoVendidoDto> ProductosMasVendidos { get; set; } = new();
        
        // Categorías más populares
        public List<CategoriaPopularDto> CategoriasMasPopulares { get; set; } = new();
        
        // Usuarios
        public int UsuariosNuevosUltimos30Dias { get; set; }
        public int UsuariosActivos { get; set; } // Usuarios con al menos un pedido
        
        // Ventas por día (últimos 30 días) para gráfico
        public List<VentaDiariaDto> VentasPorDia { get; set; } = new();
        
        // Pedidos por día (últimos 30 días) para gráfico
        public List<PedidoDiarioDto> PedidosPorDia { get; set; } = new();
        
        // Mejores clientes
        public List<ClienteTopDto> MejoresClientes { get; set; } = new();
        
        // Ticket promedio
        public double TicketPromedio { get; set; }
        
        // Tasa de conversión (pedidos completados vs total)
        public double TasaCompletados { get; set; }
    }
    
    public class ProductoVendidoDto
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? ImagenPrincipal { get; set; }
        public string CategoriaNombre { get; set; } = string.Empty;
        public int CantidadVendida { get; set; }
        public double TotalVentas { get; set; }
    }
    
    public class CategoriaPopularDto
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int CantidadVendida { get; set; }
        public double TotalVentas { get; set; }
        public int NumeroProductos { get; set; }
    }
    
    public class VentaDiariaDto
    {
        public DateTime Fecha { get; set; }
        public double Total { get; set; }
    }
    
    public class PedidoDiarioDto
    {
        public DateTime Fecha { get; set; }
        public int Cantidad { get; set; }
    }
    
    public class ClienteTopDto
    {
        public long Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public int TotalPedidos { get; set; }
        public double TotalGastado { get; set; }
    }
}
