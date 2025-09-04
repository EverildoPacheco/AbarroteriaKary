using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{
    /// <summary>
    /// VM para crear/editar Empleado (PERSONA + EMPLEADO).
    /// Estado en UI: checkbox (EstadoActivo). Persistencia: string ESTADO (ACTIVO/INACTIVO).
    /// </summary>
    public class EmpleadoViewModel : IValidatableObject
    {
        // ==========================
        // Identificador compartido
        // ==========================
        [Display(Name = "Código (ID)")]
        public string? Id { get; set; }  // = PERSONA_ID = EMPLEADO_ID

        // ==========================
        // Estado (UI + Persistencia)
        // ==========================

        /// <summary>
        /// Valor de persistencia para PERSONA/EMPLEADO. Se deriva del checkbox.
        /// No se valida porque lo seteamos desde el servidor (Controller).
        /// </summary>
        [ValidateNever]
        public string ESTADO { get; set; } = "ACTIVO";

        /// <summary>
        /// Checkbox en la UI. true => ACTIVO, false => INACTIVO.
        /// </summary>
        [Display(Name = "Activo")]
        public bool EstadoActivo { get; set; } = true;

        // ==============
        // PERSONA block
        // ==============

        [Required(ErrorMessage = "El primer nombre es obligatorio.")]
        [StringLength(50)]
        [Display(Name = "Primer nombre")]
        public string PrimerNombre { get; set; } = null!;

        [StringLength(50)]
        [Display(Name = "Segundo nombre")]
        public string? SegundoNombre { get; set; }

        [StringLength(50)]
        [Display(Name = "Tercer nombre")]
        public string? TercerNombre { get; set; }

        [Required(ErrorMessage = "El primer apellido es obligatorio.")]
        [StringLength(50)]
        [Display(Name = "Primer apellido")]
        public string PrimerApellido { get; set; } = null!;

        [StringLength(50)]
        [Display(Name = "Segundo apellido")]
        public string? SegundoApellido { get; set; }

        [StringLength(50)]
        [Display(Name = "Apellido de casada")]
        public string? ApellidoCasada { get; set; }

        [Required(ErrorMessage = "El NIT es obligatorio.")]
        [StringLength(13)]
        [Display(Name = "NIT")]
        [RegularExpression(@"^[0-9]{1,8}-?[0-9Kk]$", ErrorMessage = "NIT inválido (ej: 1234567-8 o 1234567K).")]
        public string? NIT { get; set; }

        [Required(ErrorMessage = "El CUI es obligatorio.")]
        [StringLength(13)]
        [Display(Name = "CUI")]
        [RegularExpression(@"^\d{13}$", ErrorMessage = "El CUI debe tener 13 dígitos.")]
        public string? CUI { get; set; }

        [Required(ErrorMessage = "La dirección es obligatoria.")]
        [StringLength(255)]
        [Display(Name = "Dirección")]
        public string? Direccion { get; set; }

        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        [StringLength(15)]
        [Display(Name = "Teléfono móvil")]
        [RegularExpression(@"^\d{8}$", ErrorMessage = "Ingrese 8 dígitos para el teléfono móvil.")]
        public string? TelefonoMovil { get; set; }

        [StringLength(150)]
        [EmailAddress(ErrorMessage = "Correo electrónico inválido.")]
        [Display(Name = "Correo")]
        public string? Correo { get; set; }

        // =================
        // EMPLEADO block
        // =================

        [Required(ErrorMessage = "El género es obligatorio.")]
        [Display(Name = "Género")]
        [StringLength(10)]
        public string? Genero { get; set; }

        [Required(ErrorMessage = "El puesto es obligatorio.")]
        [Display(Name = "Puesto")]
        [StringLength(10)]
        public string PuestoId { get; set; } = null!;



        [DataType(DataType.Date)]
        [Display(Name = "Fecha de nacimiento")]
        public DateTime? FechaNacimiento { get; set; } // en Controller se convierte a DateOnly?

        [Required(ErrorMessage = "La fecha de ingreso es obligatoria.")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de ingreso")]
        public DateTime FechaIngreso { get; set; } = DateTime.Today; // en Controller se convierte a DateOnly

        // ==========================
        // Propiedades de apoyo UI
        // ==========================
        [Display(Name = "Nombre completo")]
        public string NombreCompleto =>
            string.Join(" ",
                new[] { PrimerNombre, SegundoNombre, TercerNombre, PrimerApellido, SegundoApellido, ApellidoCasada }
            ).Replace("  ", " ").Trim();

        [Display(Name = "Edad (años)")]
        public int? Edad
        {
            get
            {
                if (!FechaNacimiento.HasValue) return null;
                var hoy = DateTime.Today;
                var edad = hoy.Year - FechaNacimiento.Value.Year;
                if (FechaNacimiento.Value.Date > hoy.AddYears(-edad)) edad--;
                return Math.Max(0, edad);
            }
        }


        [Display(Name = "Puesto")]
         public string? PuestoNombre { get; set; }

        // Combos
        public IEnumerable<SelectListItem> ComboPuestos { get; set; } = Array.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> ComboGeneros { get; set; } = new[]
        {
            new SelectListItem("Masculino", "Masculino"),
            new SelectListItem("Femenino", "Femenino"),
            new SelectListItem("Otro", "Otro")
        };

        // Auditoría opcional
        public AuditoriaViewModel? Auditoria { get; set; }

        // ==========================
        // Validaciones de dominio
        // ==========================
        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            // Fechas
            if (FechaNacimiento.HasValue)
            {
                if (FechaNacimiento.Value.Date > DateTime.Today)
                    yield return new ValidationResult("La fecha de nacimiento no puede ser futura.", new[] { nameof(FechaNacimiento) });

                int? edad = Edad;
                if (edad.HasValue && edad.Value < 14)
                    yield return new ValidationResult("La edad mínima permitida es 14 años.", new[] { nameof(FechaNacimiento) });
            }

            if (FechaIngreso.Date > DateTime.Today)
                yield return new ValidationResult("La fecha de ingreso no puede ser futura.", new[] { nameof(FechaIngreso) });

            if (FechaNacimiento.HasValue)
            {
                var minIngreso = FechaNacimiento.Value.AddYears(14).Date;
                if (FechaIngreso.Date < minIngreso)
                    yield return new ValidationResult(
                        "La fecha de ingreso debe ser posterior a cumplir 14 años.",
                        new[] { nameof(FechaIngreso), nameof(FechaNacimiento) });
            }

            // No validamos ESTADO directamente porque se deriva del checkbox.
            // Si aún quisiera, puede forzar consistencia:
            // var estado = EstadoActivo ? "ACTIVO" : "INACTIVO";
            // if (estado != "ACTIVO" && estado != "INACTIVO")
            //     yield return new ValidationResult("Estado inválido.", new[] { nameof(EstadoActivo) });
        }
    }

    public class AuditoriaViewModel
    {
        [Display(Name = "Creado por")]
        public string? CreadoPor { get; set; }

        [Display(Name = "Fecha creación")]
        public DateTime? FechaCreacion { get; set; }

        [Display(Name = "Modificado por")]
        public string? ModificadoPor { get; set; }

        [Display(Name = "Fecha modificación")]
        public DateTime? FechaModificacion { get; set; }

        [Display(Name = "Eliminado")]
        public bool Eliminado { get; set; }

        [Display(Name = "Eliminado por")]
        public string? EliminadoPor { get; set; }

        [Display(Name = "Fecha eliminación")]
        public DateTime? FechaEliminacion { get; set; }
    }
}
