using MongoDB.Driver;
using AldaJoyeros.Configuration;
using AldaJoyeros.Models.MongoDB;

namespace AldaJoyeros.Data
{
    /// <summary>
    /// Contexto de MongoDB para gestión de imágenes
    /// </summary>
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(MongoDbSettings settings)
        {
            var client = new MongoClient(settings.ConnectionString);
            _database = client.GetDatabase(settings.DatabaseName);
            
            // Crear índices
            CreateIndexes();
        }

        /// <summary>
        /// Colección de imágenes de productos
        /// </summary>
        public IMongoCollection<ProductoImagenMongo> ProductoImagenes =>
            _database.GetCollection<ProductoImagenMongo>("ProductoImagenes");

        /// <summary>
        /// Crear índices para optimizar consultas
        /// </summary>
        private void CreateIndexes()
        {
            // Índice en producto_id para búsquedas rápidas
            var productoIdIndex = Builders<ProductoImagenMongo>.IndexKeys
                .Ascending(img => img.ProductoId);
            
            var productoIdIndexModel = new CreateIndexModel<ProductoImagenMongo>(
                productoIdIndex,
                new CreateIndexOptions { Name = "producto_id_index" }
            );

            // Índice compuesto para producto_id y orden
            var compoundIndex = Builders<ProductoImagenMongo>.IndexKeys
                .Ascending(img => img.ProductoId)
                .Ascending(img => img.Orden);
            
            var compoundIndexModel = new CreateIndexModel<ProductoImagenMongo>(
                compoundIndex,
                new CreateIndexOptions 
                { 
                    Name = "producto_orden_index",
                    Unique = true // Evita duplicados de orden por producto
                }
            );

            // Índice para búsqueda de imagen principal por producto
            var principalIndex = Builders<ProductoImagenMongo>.IndexKeys
                .Ascending(img => img.ProductoId)
                .Ascending(img => img.EsPrincipal);
            
            var principalIndexModel = new CreateIndexModel<ProductoImagenMongo>(
                principalIndex,
                new CreateIndexOptions { Name = "producto_principal_index" }
            );

            try
            {
                ProductoImagenes.Indexes.CreateMany(new[]
                {
                    productoIdIndexModel,
                    compoundIndexModel,
                    principalIndexModel
                });
            }
            catch (MongoCommandException)
            {
                // Los índices ya existen, continuar
            }
        }
    }
}
