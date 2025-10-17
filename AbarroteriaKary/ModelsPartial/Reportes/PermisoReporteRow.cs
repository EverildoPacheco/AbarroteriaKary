// ModelsPartial/PermisoReporteRow.cs
namespace AbarroteriaKary.ModelsPartial
{
    /// Fila plana para exportes (PDF/Excel)
    public class PermisoReporteRow
    {
        public string RolId { get; set; } = "";
        public string RolNombre { get; set; } = "";

        public string ModuloId { get; set; } = "";
        public string ModuloNombre { get; set; } = "";

        public string? SubmoduloId { get; set; }           // null => permiso a TODO el módulo
        public string SubmoduloNombre { get; set; } = "-";

        public bool PuedeVer { get; set; }
        public bool PuedeCrear { get; set; }
        public bool PuedeEditar { get; set; }
        public bool PuedeEliminar { get; set; }

        public DateTime? FechaCreacion { get; set; }
    }
}
