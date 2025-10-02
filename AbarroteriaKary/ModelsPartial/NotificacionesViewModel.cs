namespace AbarroteriaKary.ModelsPartial
{
    public class NotificacionVM
    {
        public int NotificacionId { get; set; }
        public string? Titulo { get; set; }
        public string? Mensaje { get; set; }
        public string Tipo { get; set; } = "INFO";   // STOCK_BAJO, POR_VENCER, etc.
        public byte? Nivel { get; set; }            // 1..5
        public DateTime Fecha { get; set; }           // para ordenar en UI
        public string? Url { get; set; }
        public string Estado { get; set; } = "PENDIENTE";

        // Info inventario/producto (opcional)
        public string? InventarioId { get; set; }
        public string? ProductoId { get; set; }
        public string? CodigoProducto { get; set; }
        public string? NombreProducto { get; set; }
        public string? LoteCodigo { get; set; }
        public DateTime? FechaVencimiento { get; set; }

        public int? StockTotal { get; set; }

    }

    // DTO minimal para “poll” (toasts)
    public class NotificacionToastDTO
    {
        public int Id { get; set; }
        public string? Titulo { get; set; }
        public string? Mensaje { get; set; }
        public string Tipo { get; set; } = "INFO";
        public byte? Nivel { get; set; }
        public string? Url { get; set; }
    }
}
