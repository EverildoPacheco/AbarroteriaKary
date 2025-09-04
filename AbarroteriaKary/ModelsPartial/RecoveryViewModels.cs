using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{
    public class RecoveryRequestViewModel
    {
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingrese un correo válido.")]
        [Display(Name = "Correo electrónico")]
        public string Email { get; set; } = string.Empty;
    }

    //// Si decides usar una acción POST específica para recuperación con token:
    //public class RecoveryPasswordViewModel
    //{
    //    [Required]
    //    public string Token { get; set; } = string.Empty;

    //    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    //    [StringLength(50, MinimumLength = 6, ErrorMessage = "De 6 a 50 caracteres.")]
    //    [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Use solo letras y números (sin caracteres especiales).")]
    //    [Display(Name = "Nueva contraseña")]
    //    public string NuevaContrasena { get; set; } = string.Empty;

    //    [Required(ErrorMessage = "Confirme su contraseña.")]
    //    [Compare(nameof(NuevaContrasena), ErrorMessage = "La confirmación no coincide.")]
    //    [Display(Name = "Confirmar contraseña")]
    //    public string ConfirmarContrasena { get; set; } = string.Empty;
    //}
}
