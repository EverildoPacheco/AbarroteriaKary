using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{   
    public class ClienteViewModel
    {
        // Identificador compartido
        [Display(Name = "Código (ID)")]
        public string? Id { get; set; }  // = PERSONA_ID = CLIENTE_ID

        [ValidateNever]
        public string ESTADO { get; set; } = "ACTIVO";

        [Display(Name = "Activo")]
        public bool EstadoActivo { get; set; } = true;

        // PERSONA block
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



        [Display(Name = "ClienteNombre")]
        public string ClienteNombre { get; set; } = string.Empty;


        [StringLength(13)]
        [Display(Name = "NIT")]
        [RegularExpression(@"^[0-9]{1,8}-?[0-9Kk]$", ErrorMessage = "NIT inválido (ej: 1234567-8 o 1234567K).")]
        public string? NIT { get; set; }

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

        // CLIENTE block
        public string? clienteNota { get; set; }

        [Display(Name = "Fecha Ingreso")]
        public DateOnly FechaRegistro { get; set; }   // <-- antes era DateTime
    }
}