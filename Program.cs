using Microsoft.EntityFrameworkCore;
using AldaJoyeros.Data;
using AldaJoyeros.Configuration;
using AldaJoyeros.Repositories.Interfaces;
using AldaJoyeros.Repositories.Implementations;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.Services.Implementations;
using AldaJoyeros.Utilities;

namespace AldaJoyeros
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            // Configurar DbContext con MySQL (para productos, usuarios, etc.)
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
            builder.Services.AddDbContext<AldaJoyerosContext>(options =>
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

            // Configurar MongoDB (para imágenes)
            builder.Services.Configure<MongoDbSettings>(
                builder.Configuration.GetSection("MongoDbSettings"));
            
            builder.Services.AddSingleton<MongoDbSettings>(sp =>
                builder.Configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>() 
                ?? new MongoDbSettings());
            
            builder.Services.AddSingleton<MongoDbContext>();

            // Configurar AutoMapper
            builder.Services.AddAutoMapper(typeof(Program));

            // Registrar Repositorios MySQL
            builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
            builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
            builder.Services.AddScoped<IProductoRepository, ProductoRepository>();
            builder.Services.AddScoped<ICarritoRepository, CarritoRepository>();
            builder.Services.AddScoped<IDireccionRepository, DireccionRepository>();
            builder.Services.AddScoped<IPedidoRepository, PedidoRepository>();
            
            // Registrar Repositorio MongoDB
            builder.Services.AddScoped<IProductoImagenMongoRepository, ProductoImagenMongoRepository>();

            // Registrar Servicios
            builder.Services.AddScoped<IUsuarioService, UsuarioService>();
            builder.Services.AddScoped<ICategoriaService, CategoriaService>();
            builder.Services.AddScoped<IProductoService, ProductoService>();
            builder.Services.AddScoped<ICarritoService, CarritoService>();
            builder.Services.AddScoped<IDireccionService, DireccionService>();
            builder.Services.AddScoped<IPedidoService, PedidoService>();
            
            // Registrar Servicio de Imágenes MongoDB
            builder.Services.AddScoped<IProductoImagenService, ProductoImagenMongoService>();

            // Registrar Servicio de Pago Ficticio
            builder.Services.AddSingleton<IPaymentService, FakePaymentService>();

            // Registrar Utilidades
            builder.Services.AddScoped<ImageMigrationUtility>();

            // Configurar Sesiones
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseSession();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}
