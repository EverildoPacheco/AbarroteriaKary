namespace AbarroteriaKary.ModelsPartial
{
    public class ReporteMetaVM
    {
        // “ACTIVO” | “INACTIVO” | “TODOS” (raw en mayúsculas)
        public string Estado { get; set; } = "ACTIVO";

        // Texto ya formateado "dd/MM/yyyy HH:mm" (lo armas en la vista/controlador)
        public string FechaHora { get; set; } = "";

        // Usuario que genera el reporte
        public string Usuario { get; set; } = "Admin";

        // Opcionales
        public string? Busqueda { get; set; }  // Q
        public string? Desde { get; set; }     // dd/MM/yyyy o vacío
        public string? Hasta { get; set; }     // dd/MM/yyyy o vacío
    }
}
