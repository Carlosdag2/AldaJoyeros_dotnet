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
public sealed class AdminPresentacionController(AldaJoyerosContext sql, MongoDbContext mongo, AldaJoyeros.Services.CatalogoService catalogo) : BaseController
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
        var products = await catalogo.GetAsync(categoriaId);
        var families = (await catalogo.FamiliesAsync()).ToDictionary(f => f.Product.Id);
        var filter = Builders<ProductoImagenMongo>.Filter.In(x => x.ProductoId,
                products.SelectMany(p => families[p.Id].Members.Where(m => !m.Eliminado).Select(m => m.Id)))
            & Builders<ProductoImagenMongo>.Filter.Exists("imagen_data")
            & Builders<ProductoImagenMongo>.Filter.Ne(x => x.ImagenData, Array.Empty<byte>());
        var images = await mongo.ProductoImagenes.Find(filter)
            .Project(x => new { x.Id, x.ProductoId, x.EsPrincipal, x.Orden }).ToListAsync(ct);
        var principal = images.GroupBy(x => x.ProductoId).ToDictionary(g => g.Key,
            g => g.OrderByDescending(x => x.EsPrincipal).ThenBy(x => x.Orden).ThenBy(x => x.Id).First().Id);
        var familyImages = products.ToDictionary(p => p.Id, p => families[p.Id].Members.Where(m => !m.Eliminado)
            .OrderBy(m => m.Id == p.Id ? 0 : 1).Select(m => principal.GetValueOrDefault(m.Id)).FirstOrDefault(id => id != null));
        var spanish = CultureInfo.GetCultureInfo("es-ES");
        return Json(products.Where(p => familyImages[p.Id] != null).Select(p => new {
            id = p.Id, referencia = p.Nombre,
            nombre = string.IsNullOrWhiteSpace(p.Descripcion) ? p.Nombre : p.Descripcion.Length > 180 ? p.Descripcion[..177] + "…" : p.Descripcion,
            categoria = p.CategoriaNombre, precio = (p.PrecioDesde ? "Desde " : "") + p.Precio.ToString("C2", spanish),
            imagen = Url.Action("Imagen", "CatalogoPantalla", new { id = familyImages[p.Id] })
        }));
    }

}
