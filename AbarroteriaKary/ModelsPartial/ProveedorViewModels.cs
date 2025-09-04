using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AbarroteriaKary.ModelsPartial
{
    /// <summary>
    /// ViewModel para Crear/Editar un Proveedor.
    /// Une campos de PERSONA y PROVEEDOR.
    /// </summary>
    public class ProveedorFormViewModel
    {
        // ---------------------------
        // PROVEEDOR
        // ---------------------------

        [Display(Name = "Código (ID)")]
        public string? Id { get; set; }  // = PERSONA_ID = EMPLEADO_ID

        [Display(Name = "Observación")]
        [StringLength(250)]
        public string? ProveedorObservacion { get; set; }

        [Display(Name = "Empresa")]
        [StringLength(100)]
        public string? Empresa
        {
            get => _empresa;
            set => _empresa = (value ?? string.Empty).Trim();
        }
        private string? _empresa;

        // Auditoría mínima para Create (el resto se rellena en DB)
        [Required]
        public string CREADO_POR { get; set; } = "SYSTEM";

        [Display(Name = "Fecha creación")]
        public DateTime FECHA_CREACION { get; set; } = DateTime.Now;

        // Estado en DB es string, en UI manejamos checkbox
        [ValidateNever]
        [Display(Name = "Estado (texto)")]
        public string ESTADO { get; set; } = "ACTIVO";

        [Display(Name = "Activo")]
        public bool EstadoActivo { get; set; } = true;

        // ---------------------------
        // PERSONA
        // ---------------------------

        [Required(ErrorMessage = "El primer nombre es obligatorio.")]
        [StringLength(50)]
        [Display(Name = "Primer nombre")]
        public string PrimerNombre
        {
            get => _primerNombre;
            set => _primerNombre = (value ?? string.Empty).Trim();
        }
        private string _primerNombre = string.Empty;

        [StringLength(50)]
        [Display(Name = "Segundo nombre")]
        public string? SegundoNombre { get; set; }

        [StringLength(50)]
        [Display(Name = "Tercer nombre")]
        public string? TercerNombre { get; set; }

        [Required(ErrorMessage = "El primer apellido es obligatorio.")]
        [StringLength(50)]
        [Display(Name = "Primer apellido")]
        public string PrimerApellido
        {
            get => _primerApellido;
            set => _primerApellido = (value ?? string.Empty).Trim();
        }
        private string _primerApellido = string.Empty;

        [StringLength(50)]
        [Display(Name = "Segundo apellido")]
        public string? SegundoApellido { get; set; }

        [StringLength(255)]
        [Required(ErrorMessage = "La dirección es obligatoria.")]
        [Display(Name = "Dirección")]
        public string? Direccion { get; set; }

        [Required(ErrorMessage = "El teléfono es obligatorio.")]
        [StringLength(15)]
        [Display(Name = "Teléfono móvil")]
        [RegularExpression(@"^\d{8}$", ErrorMessage = "Ingrese 8 dígitos para el teléfono móvil.")]
        public string? TelefonoMovil { get; set; }

        [StringLength(15)]
        [Display(Name = "Teléfono Empresa")]
        public string? TelefonoCasa { get; set; }

        [StringLength(150)]
        [EmailAddress(ErrorMessage = "Correo electrónico inválido.")]
        [Display(Name = "Correo")]
        public string? Correo { get; set; }

        //[StringLength(15)]
        //[Display(Name = "NIT")]
        //public string? NIT { get; set; }

        //[StringLength(13)]
        //[Display(Name = "CUI")]
        //public string? CUI { get; set; }

        // ---------------------------
        // Utilitarios de UI
        // ---------------------------

        [Display(Name = "Nombre completo")]
        public string NombreCompleto =>
            string.Join(" ",
                new[] { PrimerNombre, SegundoNombre, TercerNombre, PrimerApellido, SegundoApellido }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        /// <summary>
        /// Convierte EstadoActivo (bool) a ESTADO (string) y viceversa.
        /// Debe llamarse antes de guardar.
        /// </summary>
        public void SincronizarEstado()
        {
            ESTADO = EstadoActivo ? "ACTIVO" : "INACTIVO";
        }
    }

    /// <summary>
    /// ViewModel para listar y detallar proveedores.
    /// </summary>
    public class ProveedorListItemViewModel
    {
        public string ProveedorId { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string? Empresa { get; set; }
        public string? TelefonoMovil { get; set; }
        public string? Correo { get; set; }
        [Display(Name = "Teléfono Empresa")]
        public string? TelefonoCasa { get; set; }

        public string ESTADO { get; set; } = "ACTIVO";
        public bool EstadoActivo => string.Equals(ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase);
        public DateTime FechaCreacion { get; set; }
    }
}
