using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{
    public class TipoProductoViewModel
    {

        // === Clave ===
        [Display(Name = "Tipo Producto (ID)")]
        public string? TipoProductoId { get; set; }  // 

        // === Nombre ===
        [Required(ErrorMessage = "El campo Tipo Producto es obligatorio.")]
        [Display(Name = "Tipo Produ")]
        [StringLength(100, ErrorMessage = "Máximo 100 caracteres.")]
        public string TipoProductoNomre
        {
            get => _tipoProductoNombre;
            set => _tipoProductoNombre = (value ?? string.Empty).Trim();
        }
        private string _tipoProductoNombre = string.Empty;

        // === Descripción ===
        [Display(Name = "Descripción")]
        [StringLength(255, ErrorMessage = "Máximo 255 caracteres.")]
        public string? TipoProductoDescripcion
        {
            get => _tipoProductoDescripcion;
            set => _tipoProductoDescripcion = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        private string? _tipoProductoDescripcion;

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
