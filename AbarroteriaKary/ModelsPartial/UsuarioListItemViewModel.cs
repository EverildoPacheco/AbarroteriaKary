// Ruta: ModelsPartial/UsuarioListItemViewModel.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{
    public class UsuarioListItemViewModel
    {
        [Display(Name = "ID Usuario")]
        public string UsuarioId { get; set; } = string.Empty;

        [Display(Name = "Usuario")]
        public string NombreUsuario { get; set; } = string.Empty;

        [Display(Name = "Empleado")]
        public string EmpleadoNombre { get; set; } = string.Empty;

        [Display(Name = "Rol")]
        public string RolNombre { get; set; } = string.Empty;

        [Display(Name = "Fecha creación")]
        public DateTime FechaCreacion { get; set; }

        [Display(Name = "Estado")]
        public string ESTADO { get; set; } = "ACTIVO";

        // Útil para badge/checkbox en la vista
        public bool EstadoActivo => string.Equals(ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase);
    }
}
