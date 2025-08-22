using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsMetadata
{
    public class UsuarioMetadata
    {
        [Display(Name = "Usuario")]
        [Required, StringLength(50, MinimumLength = 3)]
        public string USUARIO_NOMBRE { get; set; }

        [Display(Name = "Correo")]
        [EmailAddress]
        public string? USUARIO_CORREO { get; set; }

        [Display(Name = "Cambio inicial requerido")]
        public bool USUARIO_CAMBIOINICIAL { get; set; }
    }
}
