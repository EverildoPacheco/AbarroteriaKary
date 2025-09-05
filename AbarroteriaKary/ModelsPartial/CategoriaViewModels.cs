using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{
    /// <summary>
    /// ViewModel para CATÁLOGO: CATEGORIA
    /// Se alinea a la tabla CATEGORIA (CATEGORIA_ID, CATEGORIA_NOMBRE, CATEGORIA_DESCRIPCION, ESTADO, FECHA_CREACION).
    /// Incluye helpers para mapear checkbox ↔ ESTADO.
    /// </summary>
    public class CategoriaViewModel
    {
        // === Clave ===
        [Display(Name = "Categoría (ID)")]
        public string? CategoriaID { get; set; }  // 

        // === Nombre ===
        [Required(ErrorMessage = "El campo categoría es obligatorio.")]
        [Display(Name = "Categoría")]
        [StringLength(100, ErrorMessage = "Máximo 100 caracteres.")]
        public string CategoriaNombre
        {
            get => _categoriaNombre;
            set => _categoriaNombre = (value ?? string.Empty).Trim();
        }
        private string _categoriaNombre = string.Empty;

        // === Descripción ===
        [Display(Name = "Descripción")]
        [StringLength(255, ErrorMessage = "Máximo 255 caracteres.")]
        public string? CategoriaDescripcion
        {
            get => _categoriaDescripcion;
            set => _categoriaDescripcion = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        private string? _categoriaDescripcion;

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
