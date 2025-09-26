namespace AbarroteriaKary.ModelsPartial
{
    public class PedidoReporteViewModel
    {
        public string PedidoId { get; set; } = string.Empty;
        public DateTime? FechaPedido { get; set; }
        public DateTime? FechaEntregaPosible { get; set; }
        public string EstadoNombre { get; set; } = string.Empty;
        public string? Observacion { get; set; }
        public string? CreadoPor { get; set; }

        public PedidoReporteProveedorVM Proveedor { get; set; } = new();
        public List<PedidoReporteLineaVM> Lineas { get; set; } = new();
    }

    public class PedidoReporteProveedorVM
    {
        public string? ProveedorId { get; set; }
        public string? Empresa { get; set; }
        public string? Nit { get; set; }
        public string? Direccion { get; set; }
        public string? Telefono { get; set; }
        public string? Correo { get; set; }
    }

    public class PedidoReporteLineaVM
    {
        public string? ProductoId { get; set; }
        public string? Codigo { get; set; }
        public string? Nombre { get; set; }
        public string? Descripcion { get; set; }
        public int Cantidad { get; set; }
    }
}
