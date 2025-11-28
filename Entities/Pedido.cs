using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AldaJoyeros.Entities
{
    public enum EstadoPedido
    {
        PENDIENTE,
        EN_PROCESO,
        ENVIADO,
        ENTREGADO,
        CANCELADO
    }

    [Table("pedido")]
    public class Pedido
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Column("fecha")]
        public DateTime? Fecha { get; set; }

        [Column("estado")]
        public EstadoPedido? Estado { get; set; }

        [Column("fecha_entrega_estimada")]
        public DateTime? FechaEntregaEstimada { get; set; }

        [Column("fecha_entrega_real")]
        public DateTime? FechaEntregaReal { get; set; }

        [Column("usuario_id")]
        public long? UsuarioId { get; set; }

        [Column("direccion_id")]
        public long? DireccionId { get; set; }

        // Relaciones
        [ForeignKey("UsuarioId")]
        public virtual Usuario? Usuario { get; set; }

        [ForeignKey("DireccionId")]
        public virtual Direccion? Direccion { get; set; }

        public virtual ICollection<LineaPedido> LineasPedido { get; set; } = new List<LineaPedido>();
    }
}
