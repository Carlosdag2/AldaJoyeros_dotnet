using MongoDB.Driver;
using AldaJoyeros.Data;
using AldaJoyeros.Models.MongoDB;
using AldaJoyeros.Repositories.Interfaces;

namespace AldaJoyeros.Repositories.Implementations
{
    /// <summary>
    /// Implementación del repositorio de imágenes en MongoDB
    /// </summary>
    public class ProductoImagenMongoRepository : IProductoImagenMongoRepository
    {
        private readonly MongoDbContext _context;
        private readonly IMongoCollection<ProductoImagenMongo> _imagenes;

        public ProductoImagenMongoRepository(MongoDbContext context)
        {
            _context = context;
            _imagenes = _context.ProductoImagenes;
        }

        public async Task<IEnumerable<ProductoImagenMongo>> GetByProductoIdAsync(long productoId)
        {
            var filter = Builders<ProductoImagenMongo>.Filter.Eq(img => img.ProductoId, productoId);
            var sort = Builders<ProductoImagenMongo>.Sort.Ascending(img => img.Orden);
            
            return await _imagenes.Find(filter)
                .Sort(sort)
                .ToListAsync();
        }

        public async Task<ProductoImagenMongo?> GetByIdAsync(string id)
        {
            var filter = Builders<ProductoImagenMongo>.Filter.Eq(img => img.Id, id);
            return await _imagenes.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<ProductoImagenMongo?> GetPrincipalByProductoIdAsync(long productoId)
        {
            var filter = Builders<ProductoImagenMongo>.Filter.And(
                Builders<ProductoImagenMongo>.Filter.Eq(img => img.ProductoId, productoId),
                Builders<ProductoImagenMongo>.Filter.Eq(img => img.EsPrincipal, true)
            );
            
            return await _imagenes.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<ProductoImagenMongo> CreateAsync(ProductoImagenMongo imagen)
        {
            imagen.FechaCreacion = DateTime.UtcNow;
            await _imagenes.InsertOneAsync(imagen);
            return imagen;
        }

        public async Task<bool> UpdateAsync(string id, ProductoImagenMongo imagen)
        {
            var filter = Builders<ProductoImagenMongo>.Filter.Eq(img => img.Id, id);
            var result = await _imagenes.ReplaceOneAsync(filter, imagen);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var filter = Builders<ProductoImagenMongo>.Filter.Eq(img => img.Id, id);
            var result = await _imagenes.DeleteOneAsync(filter);
            return result.DeletedCount > 0;
        }

        public async Task<long> DeleteByProductoIdAsync(long productoId)
        {
            var filter = Builders<ProductoImagenMongo>.Filter.Eq(img => img.ProductoId, productoId);
            var result = await _imagenes.DeleteManyAsync(filter);
            return result.DeletedCount;
        }

        public async Task<long> CountByProductoIdAsync(long productoId)
        {
            var filter = Builders<ProductoImagenMongo>.Filter.Eq(img => img.ProductoId, productoId);
            return await _imagenes.CountDocumentsAsync(filter);
        }

        public async Task<bool> ExistsOrdenAsync(long productoId, int orden, string? excludeId = null)
        {
            var filterBuilder = Builders<ProductoImagenMongo>.Filter;
            var filter = filterBuilder.And(
                filterBuilder.Eq(img => img.ProductoId, productoId),
                filterBuilder.Eq(img => img.Orden, orden)
            );

            if (!string.IsNullOrEmpty(excludeId))
            {
                filter = filterBuilder.And(filter, filterBuilder.Ne(img => img.Id, excludeId));
            }

            var count = await _imagenes.CountDocumentsAsync(filter);
            return count > 0;
        }

        public async Task QuitarPrincipalAsync(long productoId, string? excludeId = null)
        {
            var filterBuilder = Builders<ProductoImagenMongo>.Filter;
            var filter = filterBuilder.And(
                filterBuilder.Eq(img => img.ProductoId, productoId),
                filterBuilder.Eq(img => img.EsPrincipal, true)
            );

            if (!string.IsNullOrEmpty(excludeId))
            {
                filter = filterBuilder.And(filter, filterBuilder.Ne(img => img.Id, excludeId));
            }

            var update = Builders<ProductoImagenMongo>.Update.Set(img => img.EsPrincipal, false);
            await _imagenes.UpdateManyAsync(filter, update);
        }
    }
}
