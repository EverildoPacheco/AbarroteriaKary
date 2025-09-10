using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AbarroteriaKary.ModelsPartial
{
    public class AreasViewModel
    {
        [Display(Name = "Id área")]
        public string areaId { get; set; }

        [Required(ErrorMessage = "El campo área es obligatorio.")]
        [Display(Name = "Área")]
        [MaxLength(100, ErrorMessage = "Máximo 100 caracteres.")]
        public string areaNombre { get; set; }

        [Display(Name = "Descripción")]
        [MaxLength(250, ErrorMessage = "Máximo 250 caracteres.")]
        public string? areaDescripcion { get; set; }


        // String que ya usas en listados / consultas
        [ValidateNever]
        [Display(Name = "Estado")]
        public string ESTADO { get; set; } = "ACTIVO";

        // Checkbox en Create/Edit. Se mapea ↔ ESTADO.
        [Display(Name = "Activo")]
        public bool EstadoActivo { get; set; } = true;


        [Display(Name = "Fecha de creación")]
        [Column(TypeName = "datetime")]
        public DateTime FechaCreacion { get; set; }
    }
}
