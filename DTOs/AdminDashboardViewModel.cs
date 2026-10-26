using AldaJoyeros.Entities;

namespace AldaJoyeros.DTOs;

public sealed class AdminDashboardViewModel
{
    public int TotalProductos { get; set; }
    public int TotalCategorias { get; set; }
    public int TotalPedidos { get; set; }
    public int TotalUsuarios { get; set; }
    public int Pendientes { get; set; }
    public int EnProceso { get; set; }
    public int Enviados { get; set; }
    public DateTime ActualizadoEn { get; set; }
    public List<AdminPedidoResumen> UltimosPedidos { get; set; } = new();
}
public sealed class AdminPedidoResumen
{
    public long Id { get; set; }
    public DateTime? Fecha { get; set; }
    public EstadoPedido? Estado { get; set; }
    public double Total { get; set; }
}
