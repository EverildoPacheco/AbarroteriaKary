using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{
    public class MarcaViewModel
    {
        // === Clave ===
        [Display(Name = "Marca (ID)")]
        public string? MarcaId { get; set; }  // 

        // === Nombre ===
        [Required(ErrorMessage = "El campo categoría es obligatorio.")]
        [Display(Name = "Marca")]
        [StringLength(100, ErrorMessage = "Máximo 100 caracteres.")]
        public string MarcaNombre
        {
            get => _marcaNombre;
            set => _marcaNombre = (value ?? string.Empty).Trim();
        }
        private string _marcaNombre = string.Empty;

        // === Descripción ===
        [Display(Name = "Descripción")]
        [StringLength(255, ErrorMessage = "Máximo 255 caracteres.")]
        public string? MarcaDescripcion
        {
            get => _marcaDescripcion;
            set => _marcaDescripcion = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        private string? _marcaDescripcion;

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
