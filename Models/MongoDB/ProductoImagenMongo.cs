using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AldaJoyeros.Models.MongoDB
{
    /// <summary>
    /// Modelo de imagen de producto almacenado en MongoDB
    /// </summary>
    public class ProductoImagenMongo
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// ID del producto en MySQL
        /// </summary>
        [BsonElement("producto_id")]
        public long ProductoId { get; set; }

        /// <summary>
        /// Datos binarios de la imagen
        /// </summary>
        [BsonElement("imagen_data")]
        public byte[] ImagenData { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Tipo MIME de la imagen
        /// </summary>
        [BsonElement("tipo_mime")]
        public string TipoMime { get; set; } = "image/jpeg";

        /// <summary>
        /// Tamaño en bytes
        /// </summary>
        [BsonElement("tamano_bytes")]
        public int TamanoBytes { get; set; }

        /// <summary>
        /// Orden de visualización
        /// </summary>
        [BsonElement("orden")]
        public int Orden { get; set; } = 0;

        /// <summary>
        /// Si es la imagen principal
        /// </summary>
        [BsonElement("es_principal")]
        public bool EsPrincipal { get; set; } = false;

        /// <summary>
        /// Fecha de creación
        /// </summary>
        [BsonElement("fecha_creacion")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Nombre del archivo original (opcional)
        /// </summary>
        [BsonElement("nombre_archivo")]
        public string? NombreArchivo { get; set; }

        /// <summary>
        /// Conversión a Base64 para usar en vistas
        /// </summary>
        [BsonIgnore]
        public string ImagenBase64 => ImagenData != null && ImagenData.Length > 0
            ? $"data:{TipoMime};base64,{Convert.ToBase64String(ImagenData)}"
            : string.Empty;
    }
}
