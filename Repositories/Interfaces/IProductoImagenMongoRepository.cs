using AldaJoyeros.Models.MongoDB;

namespace AldaJoyeros.Repositories.Interfaces
{
    /// <summary>
    /// Interfaz de repositorio para imágenes en MongoDB
    /// </summary>
    public interface IProductoImagenMongoRepository
    {
        /// <summary>
        /// Obtiene todas las imágenes de un producto
        /// </summary>
        Task<IEnumerable<ProductoImagenMongo>> GetByProductoIdAsync(long productoId);

        /// <summary>
        /// Obtiene una imagen por su ID
        /// </summary>
        Task<ProductoImagenMongo?> GetByIdAsync(string id);

        /// <summary>
        /// Obtiene la imagen principal de un producto
        /// </summary>
        Task<ProductoImagenMongo?> GetPrincipalByProductoIdAsync(long productoId);

        /// <summary>
        /// Crea una nueva imagen
        /// </summary>
        Task<ProductoImagenMongo> CreateAsync(ProductoImagenMongo imagen);

        /// <summary>
        /// Actualiza una imagen existente
        /// </summary>
        Task<bool> UpdateAsync(string id, ProductoImagenMongo imagen);

        /// <summary>
        /// Elimina una imagen
        /// </summary>
        Task<bool> DeleteAsync(string id);

        /// <summary>
        /// Elimina todas las imágenes de un producto
        /// </summary>
        Task<long> DeleteByProductoIdAsync(long productoId);

        /// <summary>
        /// Cuenta el número de imágenes de un producto
        /// </summary>
        Task<long> CountByProductoIdAsync(long productoId);

        /// <summary>
        /// Verifica si existe una imagen con ese orden para el producto
        /// </summary>
        Task<bool> ExistsOrdenAsync(long productoId, int orden, string? excludeId = null);

        /// <summary>
        /// Quita la marca principal de todas las imágenes de un producto
        /// </summary>
        Task QuitarPrincipalAsync(long productoId, string? excludeId = null);
    }
}
