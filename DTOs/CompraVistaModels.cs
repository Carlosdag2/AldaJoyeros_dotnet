namespace AldaJoyeros.DTOs;

public sealed record CompraCabeceraViewModel(int Paso, string Titulo, string Descripcion);
public sealed record CompraResumenViewModel(IEnumerable<CarritoItemDto> Articulos, decimal Total,
    string Nota = "Revisa tus artículos y la dirección antes de continuar.");
