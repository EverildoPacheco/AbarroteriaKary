namespace AbarroteriaKary.ModelsPartial
{
    public class InventarioViewModel
    {
        /// ====== ViewModels para el Index ======
        /// Detallado: una fila por (producto + vencimiento + lote)
        public class InventarioIndexItemVM
        {
            public string InventarioId { get; set; } = default!;
            public string ProductoId { get; set; } = default!;
            public string? CodigoProducto { get; set; }
            public string? NombreProducto { get; set; }
            public string? LoteCodigo { get; set; }
            public DateTime? FechaVencimiento { get; set; }  // para mostrar fácil en la vista
            public int StockActual { get; set; }
            public int StockMinimo { get; set; }
            public decimal CostoUnitario { get; set; }
            public bool Activo { get; set; }
        }

        /// Consolidado: una fila por producto (StockTotal)
        public class InventarioIndexConsolidadoVM
        {
            public string ProductoId { get; set; } = default!;
            public string? CodigoProducto { get; set; }
            public string? NombreProducto { get; set; }
            public int StockTotal { get; set; }
            public int Umbral { get; set; }          // STOCK_MINIMO (si > 0) o 10
            public bool EsBajo { get; set; }         // para resaltar en UI
        }


        public class InventarioEditVM
        {
            public string InventarioId { get; set; } = default!;
            public string ProductoId { get; set; } = default!;
            public string? CodigoProducto { get; set; }
            public string? NombreProducto { get; set; }
            public string? LoteCodigo { get; set; }
            public DateTime? FechaVencimiento { get; set; }  // solo display

            // Valores actuales (solo lectura en la vista)
            public int StockActual { get; set; }

            // Campos editables
            [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue, ErrorMessage = "El stock debe ser >= 0.")]
            public int NuevoStock { get; set; }

            [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Ingrese el motivo del ajuste.")]
            [System.ComponentModel.DataAnnotations.StringLength(200)]
            public string Motivo { get; set; } = default!;
        }



    }
}
