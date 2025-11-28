using System.ComponentModel.DataAnnotations;

namespace AldaJoyeros.DTOs
{
    public class DireccionDto
    {
        public long Id { get; set; }
        public string Calle { get; set; } = string.Empty;
        public string Numero { get; set; } = string.Empty;
        public string? Piso { get; set; }
        public string CodigoPostal { get; set; } = string.Empty;
        public string Ciudad { get; set; } = string.Empty;
        public string Provincia { get; set; } = string.Empty;
        public long? UsuarioId { get; set; }
    }

    public class DireccionCreateDto
    {
        [Required(ErrorMessage = "La calle es obligatoria")]
        [StringLength(100, ErrorMessage = "La calle no puede exceder 100 caracteres")]
        public string Calle { get; set; } = string.Empty;

        [Required(ErrorMessage = "El número es obligatorio")]
        [StringLength(10, ErrorMessage = "El número no puede exceder 10 caracteres")]
        public string Numero { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "El piso no puede exceder 50 caracteres")]
        public string? Piso { get; set; }

        [Required(ErrorMessage = "El código postal es obligatorio")]
        [StringLength(10, ErrorMessage = "El código postal no puede exceder 10 caracteres")]
        public string CodigoPostal { get; set; } = string.Empty;

        [Required(ErrorMessage = "La ciudad es obligatoria")]
        [StringLength(50, ErrorMessage = "La ciudad no puede exceder 50 caracteres")]
        public string Ciudad { get; set; } = string.Empty;

        [Required(ErrorMessage = "La provincia es obligatoria")]
        [StringLength(50, ErrorMessage = "La provincia no puede exceder 50 caracteres")]
        public string Provincia { get; set; } = string.Empty;
    }

    public class DireccionUpdateDto
    {
        [Required(ErrorMessage = "La calle es obligatoria")]
        [StringLength(100, ErrorMessage = "La calle no puede exceder 100 caracteres")]
        public string Calle { get; set; } = string.Empty;

        [Required(ErrorMessage = "El número es obligatorio")]
        [StringLength(10, ErrorMessage = "El número no puede exceder 10 caracteres")]
        public string Numero { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "El piso no puede exceder 50 caracteres")]
        public string? Piso { get; set; }

        [Required(ErrorMessage = "El código postal es obligatorio")]
        [StringLength(10, ErrorMessage = "El código postal no puede exceder 10 caracteres")]
        public string CodigoPostal { get; set; } = string.Empty;

        [Required(ErrorMessage = "La ciudad es obligatoria")]
        [StringLength(50, ErrorMessage = "La ciudad no puede exceder 50 caracteres")]
        public string Ciudad { get; set; } = string.Empty;

        [Required(ErrorMessage = "La provincia es obligatoria")]
        [StringLength(50, ErrorMessage = "La provincia no puede exceder 50 caracteres")]
        public string Provincia { get; set; } = string.Empty;
    }
}
