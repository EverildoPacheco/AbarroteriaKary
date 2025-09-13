namespace AbarroteriaKary.ModelsPartial
{
    public class ReporteDropdownVM
    {
        public string Controller { get; set; } = string.Empty;   // ej. "Areas"
        public string ExportAction { get; set; } = "Exportar";   // default
        public string? Estado { get; set; }
        public string? Q { get; set; }
        public string? FDesde { get; set; }  // yyyy-MM-dd
        public string? FHasta { get; set; }  // yyyy-MM-dd
        public string Label { get; set; } = "Exportar";
        public string SizeCss { get; set; } = "btn btn-success"; // estilo del botón
    }
}
