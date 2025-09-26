using System;
using System.Collections.Generic;

namespace AbarroteriaKary.ModelsPartial
{
    public class PedidoCerradoVM
    {
        public string PedidoId { get; set; } = string.Empty;
        public string ProveedorNombre { get; set; } = string.Empty;
        public DateTime FechaPedido { get; set; }
        public DateTime? FechaRecibido { get; set; }
        public string? Observacion { get; set; }
        public string EstadoNombre { get; set; } = string.Empty;

        public List<PedidoCerradoLineaVM> Lineas { get; set; } = new();
        public decimal Total { get; set; }
        public int CantLineas { get; set; }
    }

    public class PedidoCerradoLineaVM
    {
        public string ProductoId { get; set; } = string.Empty;
        public string? Codigo { get; set; }
        public string Nombre { get; set; } = string.Empty;

        public int Cantidad { get; set; }
        public decimal PrecioCompra { get; set; }
        public decimal Subtotal { get; set; }

        public DateOnly? FechaVencimiento { get; set; }
        public decimal? PrecioVenta { get; set; }
    }

    // Cotización reusa casi lo mismo (sin total obligatorio)
    public class CotizacionVM
    {
        public string PedidoId { get; set; } = string.Empty;
        public string ProveedorNombre { get; set; } = string.Empty;
        public DateTime FechaPedido { get; set; }
        public string? Observacion { get; set; }

        public List<PedidoCerradoLineaVM> Lineas { get; set; } = new();
    }
}
