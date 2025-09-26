using System;
using System.Collections.Generic;

namespace AbarroteriaKary.ModelsPartial
{
    public class ComprasProveedorPageVM
    {
        public ReporteHubFiltroVM Filtros { get; set; } = new();
        public List<ComprasProveedorResumenVM> Items { get; set; } = new();
        public decimal MontoTotalPeriodo { get; set; }
    }

    public class ComprasProveedorResumenVM
    {
        public string ProveedorId { get; set; } = string.Empty;
        public string ProveedorNombre { get; set; } = string.Empty;
        public int CantPedidos { get; set; }
        public int Unidades { get; set; }
        public decimal Monto { get; set; }
        public decimal TicketMedio { get; set; }
        public decimal ParticipacionPorc { get; set; }
    }

    public class ComprasProveedorDetalleVM
    {
        public string PedidoId { get; set; } = string.Empty;
        public DateTime FechaPedido { get; set; }
        public DateTime? FechaRecibido { get; set; }
        public int Lineas { get; set; }
        public decimal Monto { get; set; }
    }
}

