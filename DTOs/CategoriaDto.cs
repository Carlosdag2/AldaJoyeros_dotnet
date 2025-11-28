using System.ComponentModel.DataAnnotations;

namespace AldaJoyeros.DTOs
{
    public class CategoriaDto
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int CantidadProductos { get; set; }
    }

    public class CategoriaCreateDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(255, ErrorMessage = "El nombre no puede exceder 255 caracteres")]
        public string Nombre { get; set; } = string.Empty;
    }

    public class CategoriaUpdateDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(255, ErrorMessage = "El nombre no puede exceder 255 caracteres")]
        public string Nombre { get; set; } = string.Empty;
    }
}
