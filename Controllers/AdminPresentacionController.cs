using System.Globalization;
using AldaJoyeros.Attributes;
using AldaJoyeros.Data;
using AldaJoyeros.DTOs;
using AldaJoyeros.Models.MongoDB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace AldaJoyeros.Controllers;

[AdminOnly]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminPresentacionController(AldaJoyerosContext sql, MongoDbContext mongo) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await sql.Categorias.AsNoTracking()
        .Where(c => c.Productos.Any(p => !p.Eliminado)).OrderBy(c => c.Nombre)
        .Select(c => new CategoriaDto { Id = c.Id, Nombre = c.Nombre }).ToListAsync(ct));

    [HttpGet]
    public async Task<IActionResult> Reproducir(long? categoriaId, CancellationToken ct)
    {
        CategoriaDto? categoria = null;
        if (categoriaId.HasValue)
        {
            categoria = await sql.Categorias.AsNoTracking().Where(c => c.Id == categoriaId.Value)
                .Select(c => new CategoriaDto { Id = c.Id, Nombre = c.Nombre }).SingleOrDefaultAsync(ct);
            if (categoria == null) return NotFound();
        }
        return View(categoria);
    }

    [HttpGet]
    public async Task<IActionResult> Catalogo(CancellationToken ct, long? categoriaId = null)
    {
        var products = await sql.Productos.AsNoTracking()
            .Where(p => !p.Eliminado && (!categoriaId.HasValue || p.CategoriaId == categoriaId.Value))
            .OrderBy(p => p.Id).Select(p => new { p.Id, p.Nombre, p.Descripcion, p.Precio,
                Categoria = p.Categoria != null ? p.Categoria.Nombre : "Joyas y relojes" }).ToListAsync(ct);
        var filter = Builders<ProductoImagenMongo>.Filter.In(x => x.ProductoId, products.Select(p => p.Id))
            & Builders<ProductoImagenMongo>.Filter.Exists("imagen_data")
            & Builders<ProductoImagenMongo>.Filter.Ne(x => x.ImagenData, Array.Empty<byte>());
        var images = await mongo.ProductoImagenes.Find(filter)
            .Project(x => new { x.Id, x.ProductoId, x.EsPrincipal, x.Orden }).ToListAsync(ct);
        var principal = images.GroupBy(x => x.ProductoId).ToDictionary(g => g.Key,
            g => g.OrderByDescending(x => x.EsPrincipal).ThenBy(x => x.Orden).ThenBy(x => x.Id).First().Id);
        var spanish = CultureInfo.GetCultureInfo("es-ES");
        return Json(products.Where(p => principal.ContainsKey(p.Id)).Select(p => new {
            id = p.Id, referencia = p.Nombre,
            nombre = string.IsNullOrWhiteSpace(p.Descripcion) ? p.Nombre : p.Descripcion.Length > 180 ? p.Descripcion[..177] + "…" : p.Descripcion,
            categoria = p.Categoria, precio = p.Precio.ToString("C2", spanish),
            imagen = Url.Action("Imagen", "CatalogoPantalla", new { id = principal[p.Id] })
        }));
    }

}
