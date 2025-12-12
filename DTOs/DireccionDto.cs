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
        [StringLength(100, MinimumLength = 3, ErrorMessage = "La calle debe tener entre 3 y 100 caracteres")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ0-9\s\.,\-\/]+$", 
            ErrorMessage = "La calle solo puede contener letras, números, espacios y caracteres como . , - /")]
        public string Calle { get; set; } = string.Empty;

        [Required(ErrorMessage = "El número es obligatorio")]
        [StringLength(10, MinimumLength = 1, ErrorMessage = "El número debe tener entre 1 y 10 caracteres")]
        [RegularExpression(@"^[0-9]+[a-zA-Z]?(-[0-9]+)?$", 
            ErrorMessage = "El número debe ser válido (ej: 15, 15A, 15-17)")]
        public string Numero { get; set; } = string.Empty;

        [StringLength(20, ErrorMessage = "El piso no puede exceder 20 caracteres")]
        [RegularExpression(@"^$|^([0-9]{1,2}º?\s?[a-zA-Z]?|[Ss]\/[Nn]|[Bb]ajo|[Ee]ntresuelo|[Pp]rincipal|[Áá]tico)(\s?[a-zA-Z])?$", 
            ErrorMessage = "El piso debe tener un formato válido (ej: 2º A, Bajo, Ático, S/N)")]
        public string? Piso { get; set; }

        [Required(ErrorMessage = "El código postal es obligatorio")]
        [RegularExpression(@"^(0[1-9]|[1-4][0-9]|5[0-2])[0-9]{3}$", 
            ErrorMessage = "El código postal debe ser válido para España (5 dígitos, ej: 28001)")]
        public string CodigoPostal { get; set; } = string.Empty;

        [Required(ErrorMessage = "La ciudad es obligatoria")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "La ciudad debe tener entre 2 y 50 caracteres")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s\-\.]+$", 
            ErrorMessage = "La ciudad solo puede contener letras, espacios, guiones y puntos")]
        public string Ciudad { get; set; } = string.Empty;

        [Required(ErrorMessage = "La provincia es obligatoria")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "La provincia debe tener entre 2 y 50 caracteres")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s\-\/\.]+$", 
            ErrorMessage = "La provincia solo puede contener letras, espacios, guiones y puntos")]
        public string Provincia { get; set; } = string.Empty;
    }

    public class DireccionUpdateDto
    {
        [Required(ErrorMessage = "La calle es obligatoria")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "La calle debe tener entre 3 y 100 caracteres")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ0-9\s\.,\-\/]+$", 
            ErrorMessage = "La calle solo puede contener letras, números, espacios y caracteres como . , - /")]
        public string Calle { get; set; } = string.Empty;

        [Required(ErrorMessage = "El número es obligatorio")]
        [StringLength(10, MinimumLength = 1, ErrorMessage = "El número debe tener entre 1 y 10 caracteres")]
        [RegularExpression(@"^[0-9]+[a-zA-Z]?(-[0-9]+)?$", 
            ErrorMessage = "El número debe ser válido (ej: 15, 15A, 15-17)")]
        public string Numero { get; set; } = string.Empty;

        [StringLength(20, ErrorMessage = "El piso no puede exceder 20 caracteres")]
        [RegularExpression(@"^$|^([0-9]{1,2}º?\s?[a-zA-Z]?|[Ss]\/[Nn]|[Bb]ajo|[Ee]ntresuelo|[Pp]rincipal|[Áá]tico)(\s?[a-zA-Z])?$", 
            ErrorMessage = "El piso debe tener un formato válido (ej: 2º A, Bajo, Ático, S/N)")]
        public string? Piso { get; set; }

        [Required(ErrorMessage = "El código postal es obligatorio")]
        [RegularExpression(@"^(0[1-9]|[1-4][0-9]|5[0-2])[0-9]{3}$", 
            ErrorMessage = "El código postal debe ser válido para España (5 dígitos, ej: 28001)")]
        public string CodigoPostal { get; set; } = string.Empty;

        [Required(ErrorMessage = "La ciudad es obligatoria")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "La ciudad debe tener entre 2 y 50 caracteres")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s\-\.]+$", 
            ErrorMessage = "La ciudad solo puede contener letras, espacios, guiones y puntos")]
        public string Ciudad { get; set; } = string.Empty;

        [Required(ErrorMessage = "La provincia es obligatoria")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "La provincia debe tener entre 2 y 50 caracteres")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑüÜ\s\-\/\.]+$", 
            ErrorMessage = "La provincia solo puede contener letras, espacios, guiones y puntos")]
        public string Provincia { get; set; } = string.Empty;
    }
}
