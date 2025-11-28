using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AldaJoyeros.Entities
{
    [Table("producto_imagen")]
    public class ProductoImagen
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("producto_id")]
        public long ProductoId { get; set; }

        // Datos binarios de la imagen (BLOB)
        [Required]
        [Column("imagen_data")]
        public byte[] ImagenData { get; set; } = Array.Empty<byte>();

        [Required]
        [StringLength(50)]
        [Column("tipo_mime")]
        public string TipoMime { get; set; } = "image/jpeg";

        [Required]
        [Column("tamano_bytes")]
        public int TamanoBytes { get; set; }

        [Column("orden")]
        public int Orden { get; set; } = 0;

        [Column("es_principal")]
        public bool EsPrincipal { get; set; } = false;

        [Column("fecha_creacion")]
        public DateTime? FechaCreacion { get; set; }

        // Navegación
        [ForeignKey("ProductoId")]
        public virtual Producto? Producto { get; set; }

        // Propiedad calculada para obtener la imagen como Base64
        [NotMapped]
        public string ImagenBase64 => ImagenData != null && ImagenData.Length > 0
            ? $"data:{TipoMime};base64,{Convert.ToBase64String(ImagenData)}"
            : string.Empty;
    }
}
