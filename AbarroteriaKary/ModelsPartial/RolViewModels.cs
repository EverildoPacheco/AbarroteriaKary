using System;
using System.ComponentModel.DataAnnotations;
using AbarroteriaKary.ModelsPartial.Commons;

namespace AbarroteriaKary.ModelsPartial
{
    
    public class RolViewModel
    {
        // Identidad y campos del rol
        [Display(Name = "Código")]

        // Opcional: restringir a mayúsculas/números/guiones/guion_bajo
        //[RegularExpression(@"^[A-Z0-9_-]+$", ErrorMessage = "Use mayúsculas, números, guion (-) o guion bajo (_).")]
        public string IdRol { get; set; } = default!;


        [Display(Name = "Nombre")]
        [Required(ErrorMessage = "El nombre del rol es obligatorio.")]
        [StringLength(100, ErrorMessage = "Máximo 100 caracteres.")]
        public string NombreRol
        {
            get => _nombreRol;
            set => _nombreRol = (value ?? string.Empty).Trim();
        }
        private string _nombreRol = string.Empty;

        [Display(Name = "Descripción")]
        [StringLength(250, ErrorMessage = "Máximo 250 caracteres.")]
        public string? DescripcionRol { get; set; }


        // Estado (string) <-> Checkbox

        [Display(Name = "Estado")]
        public string ESTADO
        {
            get => EstadoActivo ? "ACTIVO" : "INACTIVO";
            set => EstadoActivo = string.Equals(value, "ACTIVO", StringComparison.OrdinalIgnoreCase);
        }

        /// Checkbox en Create/Edit. Se mapea con ESTADO.
        /// </summary>
        [Display(Name = "Activo")]
        public bool EstadoActivo { get; set; } = true;


        // Auditoría (para la tarjeta)

        [Display(Name = "Fecha de creación")]
        public DateTime? FechaCreacion { get; set; }

        // Utilidades (opcional)

        /// <summary>
        /// Clase CSS auxiliar para mostrar el badge de estado en listados/detalles.
        /// </summary>
        public string EstadoBadgeCss => EstadoActivo ? "badge-estado--activo" : "badge-estado--inactivo";
    }
}
