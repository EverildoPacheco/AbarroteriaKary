using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{
    public class CambioContrasenaViewModel
    {


        [Required(ErrorMessage = "La nueva contraseña es obligatoria")]
        [DataType(DataType.Password)]
        //[MinLength(8, ErrorMessage = "La nueva contraseña debe tener al menos 8 caracteres")]
        [Display(Name = "Nueva contraseña")]

        //[RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$",
        //    ErrorMessage = "Debe incluir mayúsculas, minúsculas y números (mín. 8 caracteres)")]
        public string NuevaContrasena { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NuevaContrasena), ErrorMessage = "La confirmación no coincide")]
        [Display(Name = "Confirmar nueva contraseña")]
        public string ConfirmarContrasena { get; set; }
    }
}
