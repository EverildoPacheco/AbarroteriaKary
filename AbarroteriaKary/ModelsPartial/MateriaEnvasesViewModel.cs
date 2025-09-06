using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{
    public class MateriaEnvasesViewModel
    {
        // === Clave ===
        [Display(Name = "Categoría (ID)")]
        public string? MateriaEnvaseId { get; set; }  // 

        // === Nombre ===
        [Required(ErrorMessage = "El campo categoría es obligatorio.")]
        [Display(Name = "Categoría")]
        [StringLength(100, ErrorMessage = "Máximo 100 caracteres.")]
        public string MateriaEnvaseNombre
        {
            get => _materiaEnvaseNombre;
            set => _materiaEnvaseNombre = (value ?? string.Empty).Trim();
        }
        private string _materiaEnvaseNombre = string.Empty;

        // === Descripción ===
        [Display(Name = "Descripción")]
        [StringLength(255, ErrorMessage = "Máximo 255 caracteres.")]
        public string? MateriaEnvaseDescripcion
        {
            get => _materiaEnvaseDescripcion;
            set => _materiaEnvaseDescripcion = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        private string? _materiaEnvaseDescripcion;

        // === Estado (string en DB) + Checkbox en UI ===
        [ValidateNever]
        [Display(Name = "Estado")]
        public string ESTADO { get; set; } = "ACTIVO";

        [Display(Name = "Activo")]
        public bool EstadoActivo { get; set; } = true;

        // === Auditoría para mostrar en la tarjeta ===
        [Display(Name = "Fecha de creación")]
        [ValidateNever]
        public DateTime? FechaCreacion { get; set; }


        /// <summary>Refleja el checkbox en la cadena ESTADO (ACTIVO/INACTIVO).</summary>
        public void RefrescarEstadoDesdeBool() => ESTADO = EstadoActivo ? "ACTIVO" : "INACTIVO";

        /// <summary>Carga el checkbox desde la cadena ESTADO.</summary>
        public void CargarBoolDesdeEstado() =>
            EstadoActivo = string.Equals(ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase);
    }
}
