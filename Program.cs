using Microsoft.EntityFrameworkCore;
using AldaJoyeros.Data;
using AldaJoyeros.Configuration;
using AldaJoyeros.Repositories.Interfaces;
using AldaJoyeros.Repositories.Implementations;
using AldaJoyeros.Services.Interfaces;
using AldaJoyeros.Services.Implementations;
using AldaJoyeros.Services;
using AldaJoyeros.Middleware;
using AldaJoyeros.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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

            // Configurar JWT
            var jwtSettings = builder.Configuration.GetSection("JwtSettings");
            builder.Services.Configure<JwtSettings>(jwtSettings);

            // Configurar Email
            builder.Services.Configure<EmailSettings>(
                builder.Configuration.GetSection("EmailSettings"));

            // Configurar Empresa (para facturas)
            builder.Services.Configure<EmpresaSettings>(
                builder.Configuration.GetSection("EmpresaSettings"));

            // Configurar Stripe
            builder.Services.Configure<StripeSettings>(
                builder.Configuration.GetSection("StripeSettings"));

            var jwtKey = jwtSettings.Get<JwtSettings>()?.Secret ?? throw new InvalidOperationException("JWT Secret no configurado en appsettings.json");
            
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Get<JwtSettings>()?.Issuer ?? "AldaJoyeros",
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Get<JwtSettings>()?.Audience ?? "AldaJoyerosApp",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
                
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var token = context.Request.Cookies["jwt_token"];
                        if (!string.IsNullOrEmpty(token))
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            // Configurar políticas de autorización
            builder.Services.AddAuthorization(AuthorizationPolicies.ConfigurePolicies);

            // Configurar AutoMapper
            builder.Services.AddAutoMapper(typeof(Program));

            // Registrar Repositorios MySQL
            builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
            builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
            builder.Services.AddScoped<IProductoRepository, ProductoRepository>();
            builder.Services.AddScoped<ICarritoRepository, CarritoRepository>();
            builder.Services.AddScoped<IDireccionRepository, DireccionRepository>();
            builder.Services.AddScoped<IPedidoRepository, PedidoRepository>();
            builder.Services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
            
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

            // Registrar Servicio de Pago con Stripe
            builder.Services.AddScoped<IPaymentService, StripePaymentService>();

            // Registrar Servicio JWT
            builder.Services.AddScoped<IJwtService, JwtService>();

            // Registrar Servicio de Email
            builder.Services.AddScoped<IEmailService, EmailService>();

            // Registrar Servicio de Facturas
            builder.Services.AddScoped<IFacturaService, FacturaService>();

            // Registrar Servicio de Códigos Postales
            builder.Services.AddSingleton<ICodigoPostalService, CodigoPostalService>();

            // Configurar Sesiones solo para carrito temporal (usuarios no autenticados)
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.Name = ".AldaJoyeros.TempCart";
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
            
            // Middleware JWT personalizado (establece ClaimsPrincipal)
            app.UseMiddleware<JwtMiddleware>();
            
            app.UseAuthentication();
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
