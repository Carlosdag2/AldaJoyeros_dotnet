using AldaJoyeros.DTOs;
using AldaJoyeros.Entities;
using AldaJoyeros.Models.MongoDB;

namespace AldaJoyeros.Mappings;

/// <summary>Explicit, bounded conversions for application entities and forms.</summary>
public sealed class AppMapper
{
    public TDestination Map<TDestination>(object? source)
    {
        if (source == null) return default!;
        object result = source switch
        {
            Usuario value when typeof(TDestination) == typeof(UsuarioDto) => Convert(value),
            UsuarioCreateDto value when typeof(TDestination) == typeof(Usuario) => Convert(value),
            Categoria value when typeof(TDestination) == typeof(CategoriaDto) => Convert(value),
            CategoriaCreateDto value when typeof(TDestination) == typeof(Categoria) => Convert(value),
            Producto value when typeof(TDestination) == typeof(ProductoDto) => Convert(value),
            ProductoCreateDto value when typeof(TDestination) == typeof(Producto) => Convert(value),
            ProductoImagenMongo value when typeof(TDestination) == typeof(ProductoImagenDto) => Convert(value),
            ProductoImagenCreateDto value when typeof(TDestination) == typeof(ProductoImagenMongo) => Convert(value),
            CarritoItem value when typeof(TDestination) == typeof(CarritoItemDto) => Convert(value),
            CarritoItemCreateDto value when typeof(TDestination) == typeof(CarritoItem) => Convert(value),
            Direccion value when typeof(TDestination) == typeof(DireccionDto) => Convert(value),
            DireccionCreateDto value when typeof(TDestination) == typeof(Direccion) => Convert(value),
            Pedido value when typeof(TDestination) == typeof(PedidoDto) => Convert(value),
            PedidoCreateDto value when typeof(TDestination) == typeof(Pedido) => Convert(value),
            LineaPedido value when typeof(TDestination) == typeof(LineaPedidoDto) => Convert(value),
            LineaPedidoCreateDto value when typeof(TDestination) == typeof(LineaPedido) => Convert(value),
            IEnumerable<Usuario> values when typeof(TDestination) == typeof(IEnumerable<UsuarioDto>) || typeof(TDestination) == typeof(List<UsuarioDto>) => values.Select(Convert).ToList(),
            IEnumerable<Categoria> values when typeof(TDestination) == typeof(IEnumerable<CategoriaDto>) || typeof(TDestination) == typeof(List<CategoriaDto>) => values.Select(Convert).ToList(),
            IEnumerable<Producto> values when typeof(TDestination) == typeof(IEnumerable<ProductoDto>) || typeof(TDestination) == typeof(List<ProductoDto>) => values.Select(Convert).ToList(),
            IEnumerable<ProductoImagenMongo> values when typeof(TDestination) == typeof(IEnumerable<ProductoImagenDto>) || typeof(TDestination) == typeof(List<ProductoImagenDto>) => values.Select(Convert).ToList(),
            IEnumerable<CarritoItem> values when typeof(TDestination) == typeof(IEnumerable<CarritoItemDto>) || typeof(TDestination) == typeof(List<CarritoItemDto>) => values.Select(Convert).ToList(),
            IEnumerable<Direccion> values when typeof(TDestination) == typeof(IEnumerable<DireccionDto>) || typeof(TDestination) == typeof(List<DireccionDto>) => values.Select(Convert).ToList(),
            IEnumerable<Pedido> values when typeof(TDestination) == typeof(IEnumerable<PedidoDto>) || typeof(TDestination) == typeof(List<PedidoDto>) => values.Select(Convert).ToList(),
            IEnumerable<LineaPedido> values when typeof(TDestination) == typeof(IEnumerable<LineaPedidoDto>) || typeof(TDestination) == typeof(List<LineaPedidoDto>) => values.Select(Convert).ToList(),
            _ => throw new InvalidOperationException($"Unsupported mapping: {source.GetType().Name} -> {typeof(TDestination).Name}")
        };
        return (TDestination)result;
    }

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
    {
        switch (source, destination)
        {
            case (UsuarioUpdateDto value, Usuario target): Update(value, target); break;
            case (CategoriaUpdateDto value, Categoria target): Update(value, target); break;
            case (ProductoUpdateDto value, Producto target): Update(value, target); break;
            case (CarritoItemUpdateDto value, CarritoItem target): Update(value, target); break;
            case (DireccionUpdateDto value, Direccion target): Update(value, target); break;
            default: throw new InvalidOperationException("Unsupported update mapping.");
        }
        return destination;
    }

    private static UsuarioDto Convert(Usuario s) => new()
    {
        Id = s.Id,
        Email = s.Email,
        Rol = s.Rol,
        CantidadPedidos = s.Pedidos.Count,
    };

    private static Usuario Convert(UsuarioCreateDto s) => new()
    {
        Email = s.Email,
        Rol = s.Rol,
    };

    private static void Update(UsuarioUpdateDto s, Usuario d)
    {
        d.Email = s.Email;
        d.Rol = s.Rol;
    }

    private static CategoriaDto Convert(Categoria s) => new()
    {
        Id = s.Id,
        Nombre = s.Nombre,
        CantidadProductos = s.Productos.Count,
    };

    private static Categoria Convert(CategoriaCreateDto s) => new()
    {
        Nombre = s.Nombre,
    };

    private static void Update(CategoriaUpdateDto s, Categoria d)
    {
        d.Nombre = s.Nombre;
    }

    private static ProductoDto Convert(Producto s) => new()
    {
        Id = s.Id,
        Nombre = s.Nombre,
        Descripcion = s.Descripcion,
        Precio = s.Precio,
        CategoriaId = s.CategoriaId ?? 0,
        Eliminado = s.Eliminado,
        FechaEliminado = s.FechaEliminado,
        CategoriaNombre = s.Categoria?.Nombre ?? string.Empty,
    };

    private static Producto Convert(ProductoCreateDto s) => new()
    {
        Nombre = s.Nombre,
        Descripcion = s.Descripcion,
        Precio = s.Precio,
        CategoriaId = s.CategoriaId,
    };

    private static void Update(ProductoUpdateDto s, Producto d)
    {
        d.Nombre = s.Nombre;
        d.Descripcion = s.Descripcion;
        d.Precio = s.Precio;
        d.CategoriaId = s.CategoriaId;
    }

    private static ProductoImagenDto Convert(ProductoImagenMongo s) => new()
    {
        Id = s.Id,
        ProductoId = s.ProductoId,
        TipoMime = s.TipoMime,
        TamanoBytes = s.TamanoBytes,
        Orden = s.Orden,
        EsPrincipal = s.EsPrincipal,
        FechaCreacion = s.FechaCreacion,
        NombreArchivo = s.NombreArchivo,
        ImagenBase64 = s.ImagenBase64,
    };

    private static ProductoImagenMongo Convert(ProductoImagenCreateDto s) => new()
    {
        ProductoId = s.ProductoId,
        ImagenData = s.ImagenData,
        TipoMime = s.TipoMime,
        Orden = s.Orden,
        EsPrincipal = s.EsPrincipal,
        NombreArchivo = s.NombreArchivo,
        TamanoBytes = s.ImagenData.Length,
        FechaCreacion = DateTime.UtcNow,
    };

    private static CarritoItemDto Convert(CarritoItem s) => new()
    {
        Id = s.Id,
        Cantidad = s.Cantidad,
        Precio = s.Precio,
        ProductoId = s.ProductoId,
        ProductoNombre = s.Producto?.Nombre ?? string.Empty,
        Producto = s.Producto == null ? null : Convert(s.Producto),
        Subtotal = s.Precio * s.Cantidad,
    };

    private static CarritoItem Convert(CarritoItemCreateDto s) => new()
    {
        Cantidad = s.Cantidad,
        ProductoId = s.ProductoId,
    };

    private static void Update(CarritoItemUpdateDto s, CarritoItem d)
    {
        d.Cantidad = s.Cantidad;
    }

    private static DireccionDto Convert(Direccion s) => new()
    {
        Id = s.Id,
        Calle = s.Calle,
        Numero = s.Numero,
        Piso = s.Piso,
        CodigoPostal = s.CodigoPostal,
        Ciudad = s.Ciudad,
        Provincia = s.Provincia,
        UsuarioId = s.UsuarioId,
    };

    private static Direccion Convert(DireccionCreateDto s) => new()
    {
        Calle = s.Calle,
        Numero = s.Numero,
        Piso = s.Piso,
        CodigoPostal = s.CodigoPostal,
        Ciudad = s.Ciudad,
        Provincia = s.Provincia,
    };

    private static void Update(DireccionUpdateDto s, Direccion d)
    {
        d.Calle = s.Calle;
        d.Numero = s.Numero;
        d.Piso = s.Piso;
        d.CodigoPostal = s.CodigoPostal;
        d.Ciudad = s.Ciudad;
        d.Provincia = s.Provincia;
    }

    private static PedidoDto Convert(Pedido s) => new()
    {
        Id = s.Id,
        Fecha = s.Fecha,
        Estado = s.Estado?.ToString(),
        FechaEntregaEstimada = s.FechaEntregaEstimada,
        FechaEntregaReal = s.FechaEntregaReal,
        UsuarioId = s.UsuarioId,
        UsuarioEmail = s.Usuario?.Email ?? string.Empty,
        Direccion = s.Direccion == null ? null : Convert(s.Direccion),
        LineasPedido = s.LineasPedido.Select(Convert).ToList(),
        Total = s.LineasPedido.Sum(lp => lp.Precio * lp.Cantidad),
    };

    private static Pedido Convert(PedidoCreateDto s) => new()
    {
        Fecha = DateTime.Now,
        Estado = EstadoPedido.PENDIENTE,
        FechaEntregaEstimada = DateTime.Now.AddDays(7),
        Direccion = Convert(s.Direccion),
    };

    private static LineaPedidoDto Convert(LineaPedido s) => new()
    {
        Id = s.Id,
        Cantidad = s.Cantidad,
        Precio = s.Precio,
        ProductoId = s.ProductoId,
        ProductoNombre = s.Producto?.Nombre ?? string.Empty,
        Producto = s.Producto == null ? null : Convert(s.Producto),
        Subtotal = s.Precio * s.Cantidad,
    };

    private static LineaPedido Convert(LineaPedidoCreateDto s) => new()
    {
        Cantidad = s.Cantidad,
        Precio = s.Precio,
        PedidoId = s.PedidoId,
        ProductoId = s.ProductoId,
    };

}
