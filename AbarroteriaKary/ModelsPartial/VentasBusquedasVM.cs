using System;

namespace AbarroteriaKary.ModelsPartial
{
    public class ClienteBusquedaItemVM
    {
        public string ClienteId { get; set; } = string.Empty;
        public string? Nombre { get; set; }
        public string? Nit { get; set; }
        public string? Info { get; set; }   // texto corto extra p/ pintar en el dropdown
        public bool EsCF { get; set; } = false; // marcado para “Consumidor Final”
    }

    public class ProductoInventarioBusquedaVM
    {
        public string ProductoId { get; set; } = string.Empty;
        public string CodigoProducto { get; set; } = string.Empty;
        public string NombreProducto { get; set; } = string.Empty;
        public string? ImagenUrl { get; set; }

        public decimal StockDisponible { get; set; }     // Σ INVENTARIO.STOCK_ACTUAL
        public decimal PrecioVigente { get; set; }       // PRECIO_HISTORICO (HASTA = NULL)
        public DateTime? ProximoVencimiento { get; set; } // Min(INVENTARIO.FECHA_VENCIMIENTO)

        public string? InfoRapida { get; set; } // UM, “vence: dd/MM”, etc.
    }

    // Para pintar el modal “agregar a venta”
    public class ProductoInventarioPreviewVM : ProductoInventarioBusquedaVM { }
}
