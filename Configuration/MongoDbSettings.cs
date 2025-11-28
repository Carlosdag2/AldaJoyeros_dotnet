namespace AldaJoyeros.Configuration
{
    /// <summary>
    /// Configuración de conexión a MongoDB
    /// </summary>
    public class MongoDbSettings
    {
        /// <summary>
        /// Cadena de conexión a MongoDB
        /// </summary>
        public string ConnectionString { get; set; } = "mongodb://localhost:27017";

        /// <summary>
        /// Nombre de la base de datos
        /// </summary>
        public string DatabaseName { get; set; } = "AldaJoyeros";

        /// <summary>
        /// Nombre de la colección de imágenes
        /// </summary>
        public string ImagenesCollectionName { get; set; } = "ProductoImagenes";
    }
}
