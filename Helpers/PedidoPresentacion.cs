namespace AldaJoyeros.Helpers;

public sealed record PedidoPresentacion(string Etiqueta, string Color, string Icono,
    string Grupo, int Paso, string Descripcion)
{
    public static PedidoPresentacion Desde(string? estado) => estado?.Trim().ToUpperInvariant() switch
    {
        "PENDIENTE" => new("Pendiente", "bg-amber-50 text-amber-800 ring-amber-200", "fa-clock", "preparacion", 0, "Hemos recibido tu pedido."),
        "EN_PROCESO" => new("En preparación", "bg-blue-50 text-blue-800 ring-blue-200", "fa-box", "preparacion", 1, "Estamos preparando tus artículos."),
        "ENVIADO" => new("Enviado", "bg-indigo-50 text-indigo-800 ring-indigo-200", "fa-truck", "enviados", 2, "Tu pedido está en camino."),
        "ENTREGADO" => new("Entregado", "bg-emerald-50 text-emerald-800 ring-emerald-200", "fa-check-circle", "entregados", 3, "El pedido figura como entregado."),
        "CANCELADO" => new("Cancelado", "bg-red-50 text-red-800 ring-red-200", "fa-times-circle", "cancelados", -1, "Este pedido está cancelado."),
        _ => new(string.IsNullOrWhiteSpace(estado) ? "Estado no disponible" : estado,
            "bg-gray-100 text-gray-700 ring-gray-200", "fa-info-circle", "otros", -1, "Consulta los datos de tu pedido o contacta con nosotros si necesitas ayuda.")
    };
}
