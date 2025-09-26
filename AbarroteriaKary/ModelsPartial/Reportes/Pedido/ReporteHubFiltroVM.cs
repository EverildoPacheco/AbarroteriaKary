using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{
    // VM de filtros del HUB (y reusado en ComprasProveedor, Cotización y PedidoCerrado)
    public class ReporteHubFiltroVM
    {
        // "pedido" | "recibido"
        public string? BaseFecha { get; set; }

        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }

        // Estados seleccionados (BORRADOR, PENDIENTE, ENVIADO, RECIBIDO, CERRADO, ANULADO)
        public List<string> Estados { get; set; } = new();

        // Proveedores seleccionados (PROVEEDOR_ID)
        public List<string> Proveedores { get; set; } = new();

        // Activo/INACTIVO/TODOS (para PEDIDO.ESTADO)
        public string? Activo { get; set; }

        // Búsqueda libre (PedidoId/Observación/Producto, etc.)
        public string? Q { get; set; }

        // === NUEVO: usado por _FiltrosCotizacion y _FiltrosPedidoCerrado ===
        [Display(Name = "No. Pedido")]
        public string? PedidoId
        {
            get => _pedidoId;
            set => _pedidoId = string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpper();
        }
        private string? _pedidoId;

        // Llenado de combos
        public List<FiltroOpcionVM> EstadosOpciones { get; set; } = new();
        public List<FiltroOpcionVM> ProveedoresOpciones { get; set; } = new();
    }

    public class FiltroOpcionVM
    {
        public string Id { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
    }
}
