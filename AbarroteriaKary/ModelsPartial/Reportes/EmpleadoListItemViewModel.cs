using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{
    public class EmpleadoListItemViewModel
    {
        [Display(Name = "Código")]
        public string EmpleadoId { get; set; } = string.Empty;

        [Display(Name = "Empleado")]
        public string EmpleadoNombre { get; set; } = string.Empty;

        [Display(Name = "Puesto")]
        public string PuestoNombre { get; set; } = string.Empty;

        [Display(Name = "CUI")]
        public string CUI { get; set; } = string.Empty;

        [Display(Name = "Teléfono")]
        public string Telefono { get; set; } = string.Empty;

        [Display(Name = "Género")]
        public string Genero { get; set; } = string.Empty;

        [Display(Name = "Fecha Ingreso")]
        public DateOnly FechaIngreso { get; set; }   // <-- antes era DateTime

        [Display(Name = "Estado")]
        public string ESTADO { get; set; } = "ACTIVO";

        public bool EstadoActivo =>
            string.Equals(ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase);
    }
}
