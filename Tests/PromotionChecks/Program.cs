using AldaJoyeros.DTOs;
using AldaJoyeros.Entities;
using AldaJoyeros.Helpers;
using AldaJoyeros.Services.Promotions;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;

int count = 0;
void Check(bool ok, string text)
{
    if (!ok)
        throw new Exception(text);
    count++;
    Console.WriteLine("PASS " + text);
}

var p = new Producto
{
    Id = 1,
    Nombre = "AN/12",
    Precio = 100,
    CategoriaId = 3,
    Categoria = new()
    {
        Id = 3,
        Nombre = "Anillos · Oro"
    },
    Proveedores = new List<ProductoProveedor>
    {
        new()
        {
            Proveedor = "PerezMora",
            Estado = "completo"
        }
    }
};
var config = new PromotionConfig();
Check(PromotionPricing.Calculate(p, config).Final == 100, "Sin ofertas conserva PVP");
config.Rules.Add(new() { ProductId = 1, Percent = 15, References = new() { { 1, 100 } } });
var price = PromotionPricing.Calculate(p, config);
Check(price.Final == 85 && price.Previous == 100 && price.Percent == 15, "Porcentaje de producto y referencia");
config.Rules.Add(new() { CategoryId = 2, CategoryName = "Anillos", Percent = 20 });
price = PromotionPricing.Calculate(p, config);
Check(price.Final == 80, "Categoría principal incluye subcategorías y gana la mejor oferta");
Check(price.Final != 68, "Los porcentajes simultáneos no se acumulan");
config.Rules[1].Enabled = false;
Check(PromotionPricing.Calculate(p, config).Final == 85, "Pausar categoría mantiene oferta individual");
config.Rules[0].Enabled = false;
Check(PromotionPricing.Calculate(p, config).Final == 100, "Pausar todo restaura PVP base");
config.Rules[0].Enabled = true;
config.Rules[0].References[1] = 90;
price = PromotionPricing.Calculate(p, config);
Check(price.Final == 85 && price.Previous == 90 && price.Percent == 15 && price.BasePrice == 100, "El historial no recalcula el porcentaje configurado");
config.Rules[0].References[1] = 70;
price = PromotionPricing.Calculate(p, config);
Check(price.Final == 85 && price.Discounted && price.Previous == 70 && price.Percent == 15, "El historial más bajo no oculta una oferta activa");
var regression=new Producto{Id=99,Nombre="HS2005-10",Precio=49};
foreach(var history in new[]{49m,43.12m,24.50m,20m}) foreach(var rate in new[]{12m,50m,20m,50m}) {
 var rules=new PromotionConfig{Rules=new(){new(){ProductId=99,Percent=rate,References=new(){{99,history}}}}};
 var calculated=PromotionPricing.Calculate(regression,rules);var dto=new ProductoDto();PromotionPricing.Apply(dto,regression,rules);
 Check(calculated.Final==decimal.Round(49*(1-rate/100),2,MidpointRounding.AwayFromZero)&&calculated.Percent==rate&&dto.TieneDescuento&&dto.PrecioSinDescuento==49,"Regresión: "+rate+" % exacto y detectable con referencia "+history);
 Check(regression.Precio==49,"No altera el PVP base al aplicar sucesivas ofertas");
}
config.Rules.Clear();
config.Rules.Add(new() { CategoryId = 9, CategoryName = "Pulseras", Percent = 50 });
Check(PromotionPricing.Calculate(p, config).Final == 100, "No aplica ofertas de otra categoría");
config.Rules.Clear();
config.Rules.Add(new() { FamilyReference = "AN", ProductId = 2, Percent = 10 });
Check(PromotionPricing.Calculate(p, config).Final == 90, "Oferta del padre alcanza la talla Pérez Mora");
p.Proveedores.Clear();
Check(PromotionPricing.Calculate(p, config).Final == 100, "No agrupa por barra productos de otros proveedores");
p.Precio = 19.99;
config.Rules.Clear();
config.Rules.Add(new() { ProductId = 1, Percent = 15 });
Check(PromotionPricing.Calculate(p, config).Final == 16.99m, "Redondeo monetario a dos decimales");
foreach (var invalid in new[]
{
    0m,
    -1m,
    100m,
    101m
}

)
{
    config.Rules[0].Percent = invalid;
    Check(PromotionPricing.Calculate(p, config).Final == 19.99m, "Porcentaje inválido ignorado " + invalid);
}

p.Precio = 0.01;
config.Rules[0].Percent = 99;
Check(PromotionPricing.Calculate(p, config).Final == 0.01m, "No genera precio cero por redondeo");
// Family offers must use the actual price of each size, without altering provider entities.
Producto Variant(long id, string reference, double amount) => new()
{
    Id = id,
    Nombre = reference,
    Precio = amount,
    Proveedores = new List<ProductoProveedor>
    {
        new()
        {
            Proveedor = "PerezMora",
            Estado = "completo"
        }
    }
};
var familySource = new[]
{
    Variant(10, "BASE", 999),
    Variant(11, "BASE/10", 100),
    Variant(12, "BASE/12", 200)
};
var familyConfig = new PromotionConfig
{
    Rules = new()
    {
        new()
        {
            ProductId = 10,
            FamilyReference = "BASE",
            Percent = 10
        }
    }
};
ProductoDto Map(Producto source)
{
    var dto = new AldaJoyeros.Mappings.AppMapper().Map<ProductoDto>(source);
    PromotionPricing.Apply(dto, source, familyConfig);
    return dto;
}

var family = ProductFamilies.Build(familySource, Map).Single().Product;
Check(family.Precio == 90 && family.PrecioAnterior == 100 && family.Tallas[1].Precio == 180, "Precio desde y talla con sus rebajas correctas");
Check(familySource[1].Precio == 100 && familySource[2].Precio == 200, "No modifica PVP de proveedor en las entidades");
family.ProductoSeleccionadoId = 11;
family.Precio = 100;
family.PrecioAnterior = null;
Check(!family.TieneDescuento, "Una talla sin oferta no hereda el distintivo de otra talla");
var rows = new[]
{
    new CarritoItemDto
    {
        Id = 1,
        ProductoId = 7,
        Cantidad = 2,
        Precio = 85,
        Subtotal = 170
    },
    new CarritoItemDto
    {
        Id = 2,
        ProductoId = 8,
        Cantidad = 1,
        Precio = 20,
        Subtotal = 20
    }
};
var quote = new CartPaymentQuote(4, "pi_1", CartPaymentQuote.Signature(rows));
Check(quote.Matches(4, "pi_1", rows.Reverse()), "La firma no depende del orden de las líneas");
Check(!quote.Matches(5, "pi_1", rows) && !quote.Matches(4, "pi_2", rows), "No reutiliza pago de otra cuenta o intento");
rows[0].Cantidad++;
Check(!quote.Matches(4, "pi_1", rows), "Detecta cambio de cantidad tras iniciar pago");
rows[0].Cantidad--;
rows[0].Precio = 84;
Check(!quote.Matches(4, "pi_1", rows), "Detecta cambio de precio tras iniciar pago");
var mail = new PromotionMail(new EphemeralDataProtectionProvider());
var token = mail.Token(4, "version1");
Check(mail.Parse(token) == (4L, "version1"), "Baja protegida sin incluir email");
Check(mail.Parse(token + "x") == null && mail.Parse("bad") == null && mail.Parse(null) == null, "Rechaza enlaces manipulados");
var products = new List<PromotionEmailProduct>
{
    new(1, null, "Anillo <oro> & plata", "https://joyas.example/producto/anillo", "https://joyas.example/foto.webp", 85, 100, 15)
};
var html = PromotionMail.Render("Ofertas <especiales>", "https://joyas.example", products, token);
Check(html.Contains("Anillo &lt;oro&gt; &amp; plata") && !html.Contains("Anillo <oro>"), "Correo escapa contenido de productos");
Check(html.Contains("foto.webp") && html.Contains("85,00") && html.Contains("100,00") && html.Contains("15 %"), "Correo incluye imagen y precios");
Check(html.Contains("/Promociones/Baja?token=") && html.Contains("/catalogo?descuentos=si"), "Enlaces de baja y catálogo sin localhost");
Check(!html.Contains("pixel") && !html.Contains("cliente@"), "Sin seguimiento ni destinatarios expuestos en el mensaje");
var clone = JsonSerializer.Deserialize<PromotionConfig>(JsonSerializer.Serialize(config))!;
Check(clone.Rules[0].Percent == 99, "Configuración persistible sin perder decimales");
var legacyConfig = JsonSerializer.Deserialize<PromotionConfig>("{\"PopupEnabled\":true}")!;
Check(legacyConfig.PopupPreviewProducts.Count == 0, "Configuración anterior mantiene imágenes automáticas sin migración");
config.PopupPreviewProducts = new List<long> { 3, 1, 2 };
var imageClone = JsonSerializer.Deserialize<PromotionConfig>(JsonSerializer.Serialize(config))!;
Check(imageClone.PopupPreviewProducts.SequenceEqual(new long[] { 3, 1, 2 }), "Selección de imágenes del aviso persiste su orden independientemente del inicio");

var plain=EmailPlainText.FromHtml("<html><head><style>.x{color:red}</style></head><body><p>Oferta de oro &amp; plata: 15 %</p><a href='https://joyas.example/oferta'>Ver oferta</a></body></html>");
Check(plain.Contains("oro & plata")&&plain.Contains("https://joyas.example/oferta")&&!plain.Contains("color:red")&&!plain.Contains("<p>"),"Versión de texto conserva contenido y enlaces sin CSS");
Console.WriteLine($"ALL {count} CHECKS PASSED; sin bases de datos, SMTP ni pagos.");
