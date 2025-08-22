using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{
    public class LoginViewModel
    {
        [Display(Name = "Usuario")]
        [Required(ErrorMessage = "El usuario es obligatorio")]
        [StringLength(50, MinimumLength = 3)]
        public string Usuario { get; set; }

        [Display(Name = "Contraseña")]
        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        public bool Recordarme { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required]
        public string Usuario { get; set; } = string.Empty;

        [Display(Name = "Contraseña actual")]
        [DataType(DataType.Password)]
        public string? PasswordActual { get; set; } // opcional cuando es cambio inicial

        [Display(Name = "Nueva contraseña")]
        [Required, DataType(DataType.Password)]
        [StringLength(64, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres.")]
        public string PasswordNuevo { get; set; }

        [Display(Name = "Confirmar contraseña")]
        [Required, DataType(DataType.Password)]
        [Compare(nameof(PasswordNuevo), ErrorMessage = "Las contraseñas no coinciden.")]
        public string PasswordConfirmacion { get; set; }
    }
}
