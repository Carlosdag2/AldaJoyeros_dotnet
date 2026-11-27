using ServerVersion = Microsoft.EntityFrameworkCore.ServerVersion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using AldaJoyeros.Mappings;
using AldaJoyeros.Repositories.Implementations;
using AldaJoyeros.Services.Implementations;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using AldaJoyeros.Configuration;
using AldaJoyeros.Data;
using AldaJoyeros.DTOs;
using AldaJoyeros.Entities;
using AldaJoyeros.Helpers;
using AldaJoyeros.Models.MongoDB;
using AldaJoyeros.Services;
using AldaJoyeros.Services.Promotions;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MySqlConnector;

// Optional integration checks. Only freshly created, random test databases are modified.
if (args.Length != 1) throw new ArgumentException("Indica la carpeta local de configuración; nunca pegues credenciales.");
var source = new ConfigurationBuilder().SetBasePath(Path.GetFullPath(args[0]))
    .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json", true).Build();
var name = "alda_optcheck_" + Guid.NewGuid().ToString("N");
if (!name.StartsWith("alda_optcheck_") || name.Length != 46 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
    throw new Exception("Nombre de pruebas inválido");
var originalSql = new MySqlConnectionStringBuilder(source.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Falta la conexión local de pruebas"));
if (name == originalSql.Database || name == source["MongoDbSettings:DatabaseName"]) throw new Exception("Base de pruebas insegura");
var adminSettings = new MySqlConnectionStringBuilder(originalSql.ConnectionString) { Database = "" };
await using var admin = new MySqlConnection(adminSettings.ConnectionString);
await admin.OpenAsync();
var created = false;
var mongoSettings = new MongoDbSettings { ConnectionString = source["MongoDbSettings:ConnectionString"] ?? "mongodb://localhost:27017", DatabaseName = name };
var client = new MongoClient(mongoSettings.ConnectionString);
var db = client.GetDatabase(name);
var enabled = Options.Create(new StorageOptimizationOptions());
var disabled = Options.Create(new StorageOptimizationOptions { Enabled = false });
var checks = 0;
void Check(bool condition, string text) { if (!condition) throw new Exception(text); checks++; Console.WriteLine("PASS " + text); }
try
{
    await using (var create = new MySqlCommand("CREATE DATABASE `" + name + "`", admin)) await create.ExecuteNonQueryAsync();
    created = true;
    var testSqlSettings = new MySqlConnectionStringBuilder(originalSql.ConnectionString) { Database = name };
    var counter = new ReadCounter();
    await using var entitySql = new AldaJoyerosContext(new DbContextOptionsBuilder<AldaJoyerosContext>()
        .UseMySql(testSqlSettings.ConnectionString, ServerVersion.AutoDetect(testSqlSettings.ConnectionString)).AddInterceptors(counter).Options);
    await entitySql.Database.EnsureCreatedAsync(); // Only the new database created by this test.
    var emptyOld = await new MetricasService(entitySql, disabled).GetMetricasAsync();
    var emptyNew = await new MetricasService(entitySql, enabled).GetMetricasAsync();
    emptyOld.GeneradasEn=emptyNew.GeneradasEn=DateTime.MinValue;
    Check(JsonSerializer.Serialize(emptyOld)==JsonSerializer.Serialize(emptyNew), "Métricas vacías idénticas y SQL agregado válido");
    var today=MetricsPeriod.MadridNow().Date;
    entitySql.Categorias.AddRange(new Categoria {Id=1,Nombre="Anillos"},new Categoria {Id=2,Nombre="Relojes"});
    entitySql.Usuarios.Add(new Usuario {Id=1,Email="prueba@ejemplo.invalid",Password="unused-test-data",Rol="ADMIN"});
    entitySql.Productos.AddRange(new Producto {Id=1,Nombre="A",Precio=30,CategoriaId=1},new Producto {Id=2,Nombre="B",Precio=20,CategoriaId=1},new Producto {Id=3,Nombre="C",Precio=10,CategoriaId=2,Eliminado=true});
    entitySql.Pedidos.AddRange(new Pedido {Id=11,UsuarioId=1,Fecha=today,Estado=EstadoPedido.PENDIENTE},new Pedido {Id=12,UsuarioId=1,Fecha=today.AddDays(-8),Estado=EstadoPedido.ENTREGADO},new Pedido {Id=13,UsuarioId=1,Fecha=today.AddDays(-1),Estado=EstadoPedido.CANCELADO});
    entitySql.LineasPedido.AddRange(new LineaPedido {PedidoId=11,ProductoId=2,Cantidad=2,Precio=49},new LineaPedido {PedidoId=12,ProductoId=2,Cantidad=3,Precio=10},new LineaPedido {PedidoId=13,ProductoId=2,Cantidad=1,Precio=100});
    entitySql.CarritoItems.Add(new CarritoItem {UsuarioId=1,ProductoId=2,Cantidad=3,Precio=20});
    await entitySql.SaveChangesAsync();entitySql.ChangeTracker.Clear();
    var mapper = new AppMapper();
    var categoryRepo=new CategoriaRepository(entitySql);
    var categoryOld=new CategoriaService(categoryRepo,mapper,NullLogger<CategoriaService>.Instance,entitySql,disabled);
    var categoryNew=new CategoriaService(categoryRepo,mapper,NullLogger<CategoriaService>.Instance,entitySql,enabled);
    var oldCategories=(await categoryOld.GetAllAsync()).OrderBy(c=>c.Id).ToArray();entitySql.ChangeTracker.Clear();
    var readStart=counter.Reads;var newCategories=(await categoryNew.GetAllAsync()).OrderBy(c=>c.Id).ToArray();await categoryNew.GetAllAsync();
    Check(JsonSerializer.Serialize(oldCategories)==JsonSerializer.Serialize(newCategories), "Categorías conservan nombres y recuentos sin cargar productos");
    Check(counter.Reads-readStart==1&&!entitySql.ChangeTracker.Entries<Producto>().Any(), "Categorías repetidas: una consulta y ningún producto materializado");
    newCategories[0].CantidadProductos=999;Check((await categoryNew.GetAllAsync()).First(c=>c.Id==1).CantidadProductos==2,"Las modificaciones de las vistas no contaminan la caché de petición");
    var userRepo=new UsuarioRepository(entitySql);
    var usersOld=await new UsuarioService(userRepo,mapper,entitySql,disabled).GetAllAsync();entitySql.ChangeTracker.Clear();
    var usersNew=await new UsuarioService(userRepo,mapper,entitySql,enabled).GetAllAsync();
    Check(JsonSerializer.Serialize(usersOld)==JsonSerializer.Serialize(usersNew)&&!counter.Last.Contains("password",StringComparison.OrdinalIgnoreCase),"Usuarios conservan número de pedidos sin leer contraseñas ni pedidos completos");
    readStart=counter.Reads;var metricsOld=await new MetricasService(entitySql,disabled).GetMetricasAsync();var oldQueries=counter.Reads-readStart;
    readStart=counter.Reads;var metricsNew=await new MetricasService(entitySql,enabled).GetMetricasAsync();var newQueries=counter.Reads-readStart;
    metricsOld.GeneradasEn=metricsNew.GeneradasEn=DateTime.MinValue;
    Check(JsonSerializer.Serialize(metricsOld)==JsonSerializer.Serialize(metricsNew),"Métricas idénticas con pedidos de distintas fechas y cancelaciones");
    Check(oldQueries-newQueries==7,"Métricas: siete consultas menos mediante agregaciones");
    readStart=counter.Reads;var oldDashboard=await new AdminDashboardService(entitySql,disabled).GetAsync();var oldDashboardQueries=counter.Reads-readStart;
    readStart=counter.Reads;var newDashboard=await new AdminDashboardService(entitySql,enabled).GetAsync();var newDashboardQueries=counter.Reads-readStart;
    oldDashboard.ActualizadoEn=newDashboard.ActualizadoEn=DateTime.MinValue;
    Check(JsonSerializer.Serialize(oldDashboard)==JsonSerializer.Serialize(newDashboard)&&oldDashboardQueries-newDashboardQueries==2,"Panel de administración conserva datos con dos consultas menos");
    var cartRepo=new CarritoRepository(entitySql);
    Check(await cartRepo.GetTotalItemsAsync(1)==3&&!counter.Last.Contains("JOIN",StringComparison.OrdinalIgnoreCase),"Contador de carrito usa SUM sin cargar productos");
    Check(await cartRepo.GetTotalItemsAsync(999)==0,"Carrito vacío devuelve cero");
    var mongo = new MongoDbContext(mongoSettings);
    var photos = Enumerable.Range(1, 205).Select(i => new ProductoImagenMongo {
        Id = ObjectId.GenerateNewId().ToString(), ProductoId = i, Orden = 0, EsPrincipal = true,
        FechaCreacion = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc), ImagenData = new byte[] { 1, 2, 3 }, TamanoBytes = 3
    }).ToArray();
    await mongo.ProductoImagenes.InsertManyAsync(photos);
    await db.RunCommandAsync<BsonDocument>(new BsonDocument("profile", 2));
    async Task<long> Reads() => await db.GetCollection<BsonDocument>("system.profile")
        .CountDocumentsAsync(new BsonDocument("command.find", "ProductoImagenes"));
    var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
    accessor.HttpContext.Request.PathBase = "/joyeria";
    var old = new PublicImageService(mongo, accessor, disabled);
    var current = new PublicImageService(mongo, accessor, enabled);
    var ids = Enumerable.Range(1,12).Select(i => (long)i).ToArray();
    var before = await Reads(); var legacy = await old.GetByProductoIdsAsync(ids); var legacyReads = await Reads() - before;
    before = await Reads(); var optimized = await current.GetByProductoIdsAsync(ids); var optimizedReads = await Reads() - before;
    Check(JsonSerializer.Serialize(legacy) == JsonSerializer.Serialize(optimized), "Metadatos, orden y enlaces idénticos al modo anterior");
    Check(legacyReads == 12 && optimizedReads == 1, "12 consultas MongoDB pasan a 1 para 12 productos");
    before = await Reads(); await current.GetByProductoIdsAsync(ids.Concat(new long[] { 1, 2 }));
    Check(await Reads() == before, "Las consultas repetidas dentro de una petición reutilizan metadatos");
    var large = new PublicImageService(mongo, accessor, enabled);
    before = await Reads(); var many = await large.GetByProductoIdsAsync(Enumerable.Range(1,205).Select(i => (long)i));
    Check(await Reads() - before == 2 && many.Count == 205, "Lotes limitados a 200 productos");
    Check((await current.GetByProductoIdsAsync(Array.Empty<long>())).Count == 0, "Selección vacía sin consultas");
    var missing = await current.GetByProductoIdsAsync(new long[] { 999, 999 });
    before = await Reads(); await current.GetByProductoIdAsync(999);
    Check(missing[999].Count == 0 && await Reads() == before, "Ausencias y referencias duplicadas se resuelven sin consultas repetidas");
    await mongo.ProductoImagenes.DeleteOneAsync(x => x.ProductoId == 1);
    Check(!(await new PublicImageService(mongo, accessor, enabled).GetByProductoIdAsync(1)).Any(), "Una nueva petición ve las imágenes eliminadas");

    List<ProductoDto> Products() => new() {
        new ProductoDto { Id = 900 }, new ProductoDto { Id = 2 }, new ProductoDto { Id = 998 },
        new ProductoDto { Id = 997, Imagenes = new() { new ProductoImagenDto { Id = "keep" } } }
    };
    var families = new ProductFamily[] {
        new(new ProductoDto { Id = 900 }, new Producto[] { new() { Id=900 }, new() { Id=1, Eliminado=true }, new() { Id=3 }, new() { Id=4 } }),
        new(new ProductoDto { Id = 2 }, new Producto[] { new() { Id=2 }, new() { Id=5 } })
    };
    CatalogoService Catalog(IOptions<StorageOptimizationOptions> options) {
        var service = new CatalogoService(null!,null!,new PublicImageService(mongo,accessor,options),null!,null,options);
        typeof(CatalogoService).GetField("snapshot", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(service, Task.FromResult<IReadOnlyList<ProductFamily>>(families));
        return service;
    }
    var oldProducts=Products();var newProducts=Products();
    await Catalog(disabled).LoadImagesAsync(oldProducts);await Catalog(enabled).LoadImagesAsync(newProducts);
    Check(JsonSerializer.Serialize(oldProducts)==JsonSerializer.Serialize(newProducts), "Misma selección de foto en padres, tallas, eliminados y productos sin imagen");
    Check(newProducts[0].Imagenes[0].ProductoId==3 && newProducts[1].Imagenes[0].ProductoId==2 && newProducts[3].Imagenes[0].Id=="keep", "Se conserva la prioridad del representante y la primera talla activa");
    var fullRepo=new ProductoImagenMongoRepository(mongo);
    var fullOld=new ProductoImagenMongoService(fullRepo,mapper,NullLogger<ProductoImagenMongoService>.Instance,disabled);
    var fullNew=new ProductoImagenMongoService(fullRepo,mapper,NullLogger<ProductoImagenMongoService>.Instance,enabled);
    var fullIds=Enumerable.Range(2,12).Select(i=>(long)i).ToArray();
    before=await Reads();var oldFull=await fullOld.GetByProductoIdsAsync(fullIds);var oldFullQueries=await Reads()-before;
    before=await Reads();var newFull=await fullNew.GetByProductoIdsAsync(fullIds);var newFullQueries=await Reads()-before;
    Check(JsonSerializer.Serialize(oldFull)==JsonSerializer.Serialize(newFull),"Imágenes de administración y pedidos mantienen datos originales y orden");
    Check(oldFullQueries==12&&newFullQueries==1,"Administración y pedidos: doce consultas de fotos pasan a una");
    var repeated = new[] { (3L, new ProductoDto {Id=3}), (3L, new ProductoDto {Id=3}), (999L,new ProductoDto {Id=999}) };
    await ProductImageBatch.LoadAsync(fullNew,repeated);
    Check(repeated[0].Item2.TieneImagenes&&repeated[1].Item2.TieneImagenes&&!repeated[2].Item2.TieneImagenes,"Pedidos con productos repetidos, eliminados o sin fotos conservan sus imágenes");
    var prodRepo = new ProductoRepository(entitySql);
    var prodOld = new ProductoService(prodRepo,fullOld,mapper,disabled);
    var prodNew = new ProductoService(prodRepo,fullNew,mapper,enabled);
    readStart=counter.Reads;var oldProductBatch=await prodOld.GetByIdsAsync(new long[]{2,3,999});var oldProductReads=counter.Reads-readStart;
    readStart=counter.Reads;var newProductBatch=await prodNew.GetByIdsAsync(new long[]{2,3,999});var newProductReads=counter.Reads-readStart;
    Check(JsonSerializer.Serialize(oldProductBatch)==JsonSerializer.Serialize(newProductBatch)&&oldProductReads==3&&newProductReads==1,"Carrito invitado: productos idénticos con tres consultas SQL frente a una");
    var readConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:DefaultConnection"]=testSqlSettings.ConnectionString }).Build();
    var realCatalog = new CatalogoService(entitySql,mapper,new PublicImageService(mongo,accessor,enabled),new CatalogUrlStore(readConfig),null,enabled,categoryNew);
    var cartService = new CarritoService(cartRepo,prodRepo,mapper,realCatalog,null,enabled);
    Check(await cartService.GetTotalItemsAsync(1)==3 && await cartService.GetTotalAsync(1)==60,"El contador del carrito y el importe de compra mantienen sus funciones distintas");
    var parent=new Producto {Id=4,Nombre="PM",Precio=50,CategoriaId=1,Proveedores=new List<ProductoProveedor>{new(){Proveedor="PerezMora",ReferenciaExterna="PM",Estado="completo"}}};
    var size=new Producto {Id=5,Nombre="PM/12",Precio=55,CategoriaId=1,Proveedores=new List<ProductoProveedor>{new(){Proveedor="PerezMora",ReferenciaExterna="PM/12",Estado="completo"}}};
    var deletedSize=new Producto {Id=6,Nombre="PM/14",Precio=60,CategoriaId=1,Eliminado=true,Proveedores=new List<ProductoProveedor>{new(){Proveedor="PerezMora",ReferenciaExterna="PM/14",Estado="completo"}}};
    entitySql.Productos.AddRange(parent,size,deletedSize);await entitySql.SaveChangesAsync();entitySql.ChangeTracker.Clear();
    var catalogOld=new CatalogoService(entitySql,mapper,new PublicImageService(mongo,accessor,disabled),new CatalogUrlStore(readConfig),null,disabled,categoryOld);
    var catalogNew=new CatalogoService(entitySql,mapper,new PublicImageService(mongo,accessor,enabled),new CatalogUrlStore(readConfig),null,enabled,categoryNew);
    var allowedOld=new List<bool>();foreach(var id in new long[]{1,3,4,5,6,999})allowedOld.Add(await catalogOld.IsPurchasableAsync(id));
    var allowedNew=new List<bool>();foreach(var id in new long[]{1,3,4,5,6,999})allowedNew.Add(await catalogNew.IsPurchasableAsync(id));
    Check(allowedOld.SequenceEqual(allowedNew)&&!allowedNew[2]&&allowedNew[3]&&!allowedNew[4],"Disponibilidad idéntica: padres no comprables, tallas activas y eliminadas");
    readStart=counter.Reads;await catalogNew.IsPurchasableAsync(5);await catalogNew.IsPurchasableAsync(4);
    Check(counter.Reads==readStart,"Disponibilidad reutiliza la misma instantánea de petición sin más SELECT");
    var stored = await mongo.ProductoImagenes.Find(FilterDefinition<ProductoImagenMongo>.Empty).ToListAsync();
    Check(stored.Count==204 && stored.All(i=>i.ImagenData.SequenceEqual(new byte[] {1,2,3})), "Las consultas no reescriben ni recomprimen los originales");

    var sqlSettings = new MySqlConnectionStringBuilder(originalSql.ConnectionString) { Database = name };
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
        ["ConnectionStrings:DefaultConnection"] = sqlSettings.ConnectionString }).Build();
    var opens=0;
    var activities=new HashSet<string>();
    using var listener=new ActivityListener {
        ShouldListenTo=s=>s.Name=="MySqlConnector",
        Sample=(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        ActivityStarted=a=>{ activities.Add(a.OperationName); if(a.OperationName=="Open") Interlocked.Increment(ref opens); }
    };
    ActivitySource.AddActivityListener(listener);
    var store = new PromotionStore(config,enabled);
    await store.ObserveAsync(new[] { (Id:1L, Price:49m) });
    var knownOpens=opens;await store.ObserveAsync(new[] { (Id:1L, Price:49m) });
    Check(knownOpens>0 && opens==knownOpens, "Precio sin cambios: ninguna conexión MySQL adicional");
    await store.ObserveAsync(new[] { (Id:1L, Price:51m) });
    Check(opens>knownOpens, "Los cambios de precio siguen guardándose");
    var legacyStore=new PromotionStore(config,disabled);await legacyStore.ObserveAsync(new[] { (Id:1L, Price:51m) });
    knownOpens=opens;await legacyStore.ObserveAsync(new[] { (Id:1L, Price:51m) });
    Check(opens>knownOpens, "Desactivar optimización recupera las conexiones anteriores");
    await using var sql=new MySqlConnection(sqlSettings.ConnectionString);await sql.OpenAsync();
    await using var count=new MySqlCommand("SELECT COUNT(*) FROM promociones_precios WHERE producto_id=1",sql);
    Check(Convert.ToInt32(await count.ExecuteScalarAsync())==2, "Historial intacto: solo registra los dos precios diferentes");
    Console.WriteLine($"ALL {checks} CHECKS PASSED; consultas verificadas en bases nuevas aisladas; sin SMTP ni pagos.");
}
finally
{
    if(created) {
        // Both database names were generated here, validated and compared to production before creation.
        await client.DropDatabaseAsync(name);
        await using var drop=new MySqlCommand("DROP DATABASE `"+name+"`",admin);await drop.ExecuteNonQueryAsync();
        Console.WriteLine("Eliminadas únicamente las dos bases de prueba creadas en esta ejecución.");
    }
}

sealed class ReadCounter : DbCommandInterceptor
{
    public int Reads; public string Last="";
    public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(System.Data.Common.DbCommand command,CommandEventData eventData,InterceptionResult<System.Data.Common.DbDataReader> result,CancellationToken cancellationToken=default) {
        if(command.CommandText.TrimStart().StartsWith("SELECT",StringComparison.OrdinalIgnoreCase)){Reads++;Last=command.CommandText;}
        return base.ReaderExecutingAsync(command,eventData,result,cancellationToken);
    }
}
