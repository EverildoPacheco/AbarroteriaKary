using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AbarroteriaKary.ModelsPartial
{
    /// <summary>
    /// Base con campos comunes entre Create y Edit.
    /// </summary>
    public class UsuarioFormBase
    {
        // ==== Identificador (lo genera su correlativo; en Create se muestra solo lectura) ====
        [Display(Name = "Código usuario")]
        public string UsuarioId { get; set; } = string.Empty;


        // ==== Login / nombre de usuario (único) ====
        [Required(ErrorMessage = "El nombre de usuario es obligatorio.")]
        [StringLength(50, ErrorMessage = "Máximo 50 caracteres.")]
        [Display(Name = "Usuario (login)")]
        // Sugerencia: solo letras, números, punto, guion y guion bajo.
        [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Use solo letras, números, punto, guion y guion bajo.")]
        public string NombreUsuario
        {
            get => _nombreUsuario;
            set => _nombreUsuario = (value ?? string.Empty).Trim();
        }
        private string _nombreUsuario = string.Empty;

  
        // ==== Rol y Empleado ====
        [Required(ErrorMessage = "Seleccione un rol.")]
        [Display(Name = "Rol")]
        public string RolId { get; set; } = string.Empty;

        [ValidateNever]
        [Display(Name = "Rol")]
        public string? nombreRol { get; set; }


        [Required(ErrorMessage = "Seleccione un empleado.")]
        [Display(Name = "Empleado")]
        public string EmpleadoId
        {
            get => _empleadoId;
            set => _empleadoId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _empleadoId = string.Empty;

        // ==== Estado (string en DB + checkbox en UI) ====
        [ValidateNever]
        [Display(Name = "Estado")]
        public string ESTADO { get; set; } = "ACTIVO";

        [Display(Name = "Activo")]
        public bool EstadoActivo { get; set; } = true;

        // ==== Cambio inicial (primer login obliga cambiar password) ====
        [Display(Name = "Cambio inicial")]
        public bool CambioInicial { get; set; } = true;  // por defecto TRUE

        // ==== Solo visualización ====
        [Display(Name = "Fecha registro")]
        [DataType(DataType.DateTime)]
        public DateTime? FechaRegistro { get; set; }  // en Create no se envía; DB pone GETDATE()

        // ==== Combos (no se validan) ====
        [ValidateNever]
        public IEnumerable<SelectListItem> Roles { get; set; } = Enumerable.Empty<SelectListItem>();

        [ValidateNever]
        public IEnumerable<SelectListItem> EmpleadosDisponibles { get; set; } = Enumerable.Empty<SelectListItem>();

        // ==== Utilitario para mapear EstadoActivo <-> ESTADO ====
        public void SincronizarEstado()
        {
            ESTADO = EstadoActivo ? "ACTIVO" : "INACTIVO";
        }
        public void CargarEstadoDesdeCadena()
        {
            EstadoActivo = string.Equals(ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// VM para CREAR usuario.
    /// </summary>
    public class UsuarioCreateViewModel : UsuarioFormBase
    {
        // Contraseña temporal SIN caracteres especiales (solo letras y números).
        [Required(ErrorMessage = "La contraseña temporal es obligatoria.")]
        [Display(Name = "Contraseña temporal")]
        [DataType(DataType.Password)]
        [StringLength(50, MinimumLength = 6, ErrorMessage = "De 6 a 50 caracteres.")]
        [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Use solo letras y números (sin caracteres especiales).")]
        public string ContraseñaTemporal { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirme la contraseña.")]
        [Display(Name = "Confirmar contraseña")]
        [DataType(DataType.Password)]
        [Compare(nameof(ContraseñaTemporal), ErrorMessage = "La confirmación no coincide.")]
        public string ConfirmarContraseña { get; set; } = string.Empty;

        // Por las reglas del negocio, al crear debe quedar en CAMBIO_INICIAL = true (heredado de base).
        public UsuarioCreateViewModel()
        {
            CambioInicial = true;
        }
    }

    /// <summary>
    /// VM para EDITAR usuario.
    /// La contraseña es opcional; se controla con el flag RestablecerPassword.
    /// </summary>
    public class UsuarioEditViewModel : UsuarioFormBase
    {
        [Display(Name = "Restablecer contraseña")]
        public bool RestablecerPassword { get; set; } = false;

        [Display(Name = "Nueva contraseña")]
        [DataType(DataType.Password)]
        [StringLength(50, MinimumLength = 6, ErrorMessage = "De 6 a 50 caracteres.")]
        [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Use solo letras y números (sin caracteres especiales).")]
        public string? NuevaContraseña { get; set; }

        [Display(Name = "Confirmar nueva contraseña")]
        [DataType(DataType.Password)]
        [Compare(nameof(NuevaContraseña), ErrorMessage = "La confirmación no coincide.")]
        public string? ConfirmarNuevaContraseña { get; set; }

        // Validación condicional (si marca RestablecerPassword, obliga llenar las contraseñas)
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (RestablecerPassword)
            {
                if (string.IsNullOrWhiteSpace(NuevaContraseña))
                    yield return new ValidationResult("Ingrese la nueva contraseña.", new[] { nameof(NuevaContraseña) });
                if (string.IsNullOrWhiteSpace(ConfirmarNuevaContraseña))
                    yield return new ValidationResult("Confirme la nueva contraseña.", new[] { nameof(ConfirmarNuevaContraseña) });
            }
        }
    }
}
