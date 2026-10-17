using AldaJoyeros.DTOs;
using AldaJoyeros.Entities;
using AldaJoyeros.Helpers;

static Producto P(long id, string reference, double price, bool hidden = false, string provider = "PerezMora") => new()
{
    Id = id, Nombre = reference, Precio = price, Eliminado = hidden,
    Proveedores = new List<ProductoProveedor> { new() { Proveedor = provider, Estado = "completo" } }
};
static ProductoDto Map(Producto p) => new() { Id = p.Id, Nombre = p.Nombre, Precio = p.Precio };
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
var source = new[] { P(1, "02000090", 100), P(2, "02000090/08", 50, true), P(3, "02000090/10", 381.80), P(4, "02000090/09", 358.68), P(5, "OTHER", 40), P(6, "02000090/11", 200, provider: "Munreco") };
var families = ProductFamilies.Build(source, Map);
Check(families.Count == 3, "Una familia, un producto suelto y la referencia de otro proveedor");
var family = families.Single(f => f.Product.Id == 1).Product;
Check(family.Nombre == "02000090", "Referencia padre visible");
Check(family.Tallas.Count == 2 && family.Tallas[0].Talla == "09" && family.Tallas[1].Talla == "10", "Orden numérico y exclusión de tallas ocultas");
Check(family.Precio == 358.68 && family.PrecioDesde, "Precio desde usa una talla disponible, nunca el padre ni tallas ocultas");
Check(family.Tallas[0].ProductoId == 4 && family.Tallas[0].Precio == 358.68, "Se conserva el ID y el precio exacto de cada talla");
Check(source[0].Nombre == "02000090" && source[0].Precio == 100 && source[2].Nombre == "02000090/10", "No se alteran las entidades originales");
Check(!families.Single(f => f.Product.Id == 6).Product.TieneTallas, "No agrupa referencias con barra de otros proveedores");
var noParent = ProductFamilies.Build(new[] { P(10, "BASE/12", 200), P(11, "BASE/08", 180) }, Map).Single().Product;
Check(noParent.Nombre == "BASE" && noParent.Tallas.Count == 2 && noParent.Id == 10, "Familia sin registro padre");
var hiddenParent = ProductFamilies.Build(new[] { P(20, "BASE", 10, true), P(21, "BASE/13", 200) }, Map).Single().Product;
Check(hiddenParent.Id == 21 && hiddenParent.Precio == 200, "Padre oculto con talla disponible");
Check(ProductFamilies.Build(new[] { P(30, "BASE", 100), P(31, "BASE/12", 200, true) }, Map).Count == 0, "Sin tallas disponibles no se vende el padre");
Check(!ProductFamilies.Build(new[] { P(40, "SIMPLE", 20) }, Map).Single().Product.TieneTallas, "Producto normal conserva comportamiento");
Check(!ProductFamilies.Build(new[] { P(50, "BASE/COLOR", 20) }, Map).Single().Product.TieneTallas, "Un sufijo de texto no es una talla numérica");
Console.WriteLine("PASS: agrupación, ID y precio por talla, orden, ocultos, ausencia de padre, proveedores distintos y entidades intactas.");
