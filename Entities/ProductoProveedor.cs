using System.ComponentModel.DataAnnotations.Schema;

namespace AldaJoyeros.Entities
{
    [Table("producto_proveedor")]
    public class ProductoProveedor
    {
        [Column("proveedor")] public string Proveedor { get; set; } = string.Empty;
        [Column("referencia_externa")] public string ReferenciaExterna { get; set; } = string.Empty;
        [Column("producto_id")] public long ProductoId { get; set; }
        [Column("marca")] public string Marca { get; set; } = string.Empty;
        [Column("coleccion")] public string Coleccion { get; set; } = string.Empty;
        [Column("descripcion_completa")] public string DescripcionCompleta { get; set; } = string.Empty;
        [Column("caracteristicas_json")] public string CaracteristicasJson { get; set; } = "{}";
        [Column("creado_por_importacion")] public bool CreadoPorImportacion { get; set; }
        [Column("estado")] public string Estado { get; set; } = string.Empty;
        public virtual Producto Producto { get; set; } = null!;
    }
}
