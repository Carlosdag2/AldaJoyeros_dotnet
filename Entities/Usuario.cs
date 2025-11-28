using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AldaJoyeros.Entities
{
    [Table("usuario")]
    public class Usuario
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("email")]
        [StringLength(255)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column("password")]
        [StringLength(255)]
        public string Password { get; set; } = string.Empty;

        [Column("rol")]
        [StringLength(255)]
        public string? Rol { get; set; }

        // Relaciones
        public virtual ICollection<CarritoItem> CarritoItems { get; set; } = new List<CarritoItem>();
        public virtual ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
        public virtual ICollection<Direccion> Direcciones { get; set; } = new List<Direccion>();
    }
}
