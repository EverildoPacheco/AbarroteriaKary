using System;
using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{
    public class UsuarioDetailsViewModel
    {
        [Display(Name = "Código usuario")]
        public string UsuarioId { get; set; } = string.Empty;

        [Display(Name = "Usuario (login)")]
        public string NombreUsuario { get; set; } = string.Empty;

        [Display(Name = "Rol")]
        public string? RolId { get; set; }
        public string? RolNombre { get; set; }

        [Display(Name = "Empleado")]
        public string EmpleadoId { get; set; } = string.Empty;
        public string EmpleadoNombre { get; set; } = string.Empty;

        [Display(Name = "Estado")]
        public string ESTADO { get; set; } = "ACTIVO";

        [Display(Name = "Activo")]
        public bool EstadoActivo { get; set; }

        [Display(Name = "Cambio inicial")]
        public bool CambioInicial { get; set; }

        [Display(Name = "Fecha registro")]
        [DataType(DataType.DateTime)]
        public DateTime? FechaRegistro { get; set; }

        [Display(Name = "Último cambio de contraseña")]
        [DataType(DataType.DateTime)]
        public DateTime? UltimoCambioPwd { get; set; }
    }
}
