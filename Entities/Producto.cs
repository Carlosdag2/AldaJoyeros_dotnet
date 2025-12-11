using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AldaJoyeros.Entities
{
    [Table("producto")]
    public class Producto
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("nombre")]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Column("descripcion")]
        [StringLength(1000)]
        public string? Descripcion { get; set; }

        [Required]
        [Column("precio")]
        public double Precio { get; set; }

        [Column("categoria_id")]
        public long? CategoriaId { get; set; }

        /// <summary>
        /// Indica si el producto está eliminado (soft delete).
        /// Los productos eliminados no aparecen en el catálogo pero se mantienen en pedidos.
        /// </summary>
        [Column("eliminado")]
        public bool Eliminado { get; set; } = false;

        /// <summary>
        /// Fecha en que se eliminó el producto (null si no está eliminado)
        /// </summary>
        [Column("fecha_eliminado")]
        public DateTime? FechaEliminado { get; set; }

        // Relaciones
        [ForeignKey("CategoriaId")]
        public virtual Categoria? Categoria { get; set; }

        public virtual ICollection<CarritoItem> CarritoItems { get; set; } = new List<CarritoItem>();
        public virtual ICollection<LineaPedido> LineasPedido { get; set; } = new List<LineaPedido>();

        // Propiedad para imágenes (se cargan desde MongoDB)
        [NotMapped]
        public List<DTOs.ProductoImagenDto> Imagenes { get; set; } = new List<DTOs.ProductoImagenDto>();
    }
}
