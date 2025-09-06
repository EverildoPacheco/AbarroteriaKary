using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{
    public class SubCategoriaViewModel
    {
        // === Clave y campos base (según DB) ===
        // PUESTO_ID: VARCHAR(10) NOT NULL
        [Required(ErrorMessage = "El código es obligatorio")]
        [StringLength(10, ErrorMessage = "Máximo 10 caracteres")]
        [Display(Name = "Código")]
        public string SubCategoriaId { get; set; } = default!;

        // PUESTO_NOMBRE: VARCHAR(100) NOT NULL
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, ErrorMessage = "Máximo 100 caracteres")]
        [Display(Name = "Sub Categoria")]
        public string SubCategoriaNombre { get; set; } = default!;

        // PUESTO_DESCRIPCION: VARCHAR(255) NULL
        [StringLength(255, ErrorMessage = "Máximo 255 caracteres")]
        [Display(Name = "Descripción")]
        public string? DescripcionSubCategoria { get; set; }

        // FK -> AREA_ID: VARCHAR(10) NOT NULL
        [Required(ErrorMessage = "Debe seleccionar un área")]
        [StringLength(10)]
        [Display(Name = "Categoria")]
        public string CategoriaID { get; set; } = default!;

        // Solo para mostrar en listados (no se postea al servidor)
        [ValidateNever]
        [Display(Name = "Categoria")]
        public string? NombreCategoria { get; set; }

        // === Estado y utilitarios de UI ===
        // En DB existe ESTADO (ACTIVO/INACTIVO). Usamos checkbox + string.
        [ValidateNever]
        [Display(Name = "Estado")]
        public string ESTADO { get; set; } = "ACTIVO";

        // Checkbox en Create/Edit. Se mapea ↔ ESTADO.
        [Display(Name = "Activo")]
        public bool EstadoActivo { get; set; } = true;

        // Combo para Áreas en Create/Edit
        [ValidateNever]
        public IEnumerable<SelectListItem> CategoriaOpciones { get; set; } = new List<SelectListItem>();

        // === Auditoría (solo lectura en UI) ===
        [Display(Name = "Fecha de creación")]
        public DateTime? FECHA_CREACION { get; set; }

        // --- Helpers de sincronización ---
        public void SincronizarEstadoDesdeString()
        {
            EstadoActivo = string.Equals(ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase);
        }

        public void SincronizarEstadoHaciaString()
        {
            ESTADO = EstadoActivo ? "ACTIVO" : "INACTIVO";
        }
    }
}
