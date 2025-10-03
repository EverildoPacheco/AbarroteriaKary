using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AbarroteriaKary.ModelsPartial
{
    // ============================================================
    // VENTA - ViewModels (Encabezado + Detalle + Cobro)
    // Tablas: VENTA, DETALLE_VENTA, RECIBO
    // Relación operacional con caja: SESION (no FK en BD, pero se exige abierta)
    // Precio: PVP vigente (PRECIO_HISTORICO: HASTA IS NULL o <= FECHA venta)
    // Stock/Costeo: FEFO (si vence) / FIFO (si no), reflejado en KARDEX por lote
    // ============================================================

    /// <summary>
    /// VM principal para crear una venta desde la pantalla de “Nueva Venta”.
    /// </summary>
    public class VentaViewModel : IValidatableObject
    {
        // ---------- Identificadores y contexto ----------
        [Display(Name = "No. Venta")]
        public string VentaId
        {
            get => _ventaId;
            set => _ventaId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _ventaId = string.Empty;

        [Required(ErrorMessage = "No hay caja aperturada. Abra una sesión de caja para vender.")]
        [Display(Name = "Sesión de caja")]
        public string SesionId
        {
            get => _sesionId;
            set => _sesionId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _sesionId = string.Empty;

        // ---------- Encabezado (tabla VENTA) ----------
        [Display(Name = "Fecha")]
        public DateTime FechaVenta { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Seleccione un cliente.")]
        [Display(Name = "Cliente")]
        public string ClienteId
        {
            get => _clienteId;
            set => _clienteId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _clienteId = string.Empty;

        [Required]
        [Display(Name = "Vendedor (usuario)")]
        public string UsuarioId
        {
            get => _usuarioId;
            set => _usuarioId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _usuarioId = string.Empty;

        // ---------- Detalle ----------
        [MinLength(0)]
        [ValidateNever]
        public List<VentaDetalleItemVM> Lineas { get; set; } = new();

        // ========= NUEVO: respaldo del total leído desde la BD (para listados) =========
        [ValidateNever]
        public decimal? TotalDb { get; set; }

        // Totales visibles
        [Display(Name = "Total")]
        [DataType(DataType.Currency)]
        public decimal Total
            => TotalDb.HasValue ? Math.Round(TotalDb.Value, 2) : CalcularTotal();

        private decimal CalcularTotal()
        {
            decimal total = 0m;
            foreach (var it in Lineas) total += it.Subtotal;
            return Math.Round(total, 2);
        }


















        // ---------- Cobro (RECIBO) ----------
        /// <summary>
        /// Lista de recibos (permite múltiples métodos de pago).
        /// Para flujo básico: 1 recibo en efectivo por el total.
        /// </summary>
        [ValidateNever]
        public List<ReciboItemVM> Recibos { get; set; } = new();

        // Conveniencia para tu modal de “pagar venta” (efectivo recibido/cambio)
        [Display(Name = "Efectivo recibido")]
        [DataType(DataType.Currency)]
        [Range(0, 999999999999.99)]
        public decimal? EfectivoRecibido { get; set; }

        [Display(Name = "Cambio")]
        [DataType(DataType.Currency)]
        public decimal Cambio => Math.Max(0, Math.Round((EfectivoRecibido ?? 0) - Total, 2));

        // ---------- Búsquedas/UI ----------
        [Display(Name = "Buscar producto")]
        public string? Q { get; set; }

        [ValidateNever]
        public ProductoVentaSeleccionadoVM? ProductoSeleccionado { get; set; }

        // ---------- Auditoría visual ----------
        [ValidateNever]
        public AuditoriaViewModel Auditoria { get; set; } = new();

        // ---------- Validaciones de negocio del encabezado + detalle + cobro ----------
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Caja abierta obligatoria
            if (string.IsNullOrWhiteSpace(SesionId))
                yield return new ValidationResult("No hay caja aperturada. No se puede registrar la venta.", new[] { nameof(SesionId) });

            // Debe existir al menos 1 línea
            if (Lineas == null || Lineas.Count == 0)
                yield return new ValidationResult("Debe agregar al menos un producto a la venta.", new[] { nameof(Lineas) });

            // Validar líneas
            if (Lineas != null)
            {
                for (int i = 0; i < Lineas.Count; i++)
                {
                    var ln = Lineas[i];
                    if (ln == null)
                    {
                        yield return new ValidationResult($"La línea {i + 1} es inválida.", new[] { nameof(Lineas) });
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(ln.ProductoId))
                        yield return new ValidationResult($"Seleccione el producto en la línea {i + 1}.", new[] { nameof(Lineas) });

                    if (ln.Cantidad <= 0)
                        yield return new ValidationResult($"La cantidad de la línea {i + 1} debe ser mayor a cero.", new[] { nameof(Lineas) });

                    if (ln.PrecioUnitario < 0)
                        yield return new ValidationResult($"El precio unitario de la línea {i + 1} no puede ser negativo.", new[] { nameof(Lineas) });
                }
            }

            // Cobro básico en efectivo (si se usa el modal simple)
            if (EfectivoRecibido.HasValue && EfectivoRecibido.Value < Total)
                yield return new ValidationResult("El efectivo recibido no cubre el total de la venta.", new[] { nameof(EfectivoRecibido) });

            // Si se usan Recibos múltiples, validar sumatoria igual al total
            if (Recibos != null && Recibos.Count > 0)
            {
                decimal suma = 0m;
                foreach (var r in Recibos) suma += Math.Max(0, r.Monto);
                if (Math.Round(suma, 2) != Math.Round(Total, 2))
                    yield return new ValidationResult("La suma de los recibos debe ser igual al total de la venta.", new[] { nameof(Recibos) });
            }
        }
    }

    /// <summary>
    /// Línea de venta (detalle). Precio = PVP vigente para la fecha, no por lote.
    /// El costeo/consumo por lote (FEFO/FIFO) viaja en LotesConsumidos para KARDEX.
    /// </summary>
    public class VentaDetalleItemVM
    {
        [ValidateNever] public string? DetalleVentaId { get; set; } // correlativo DETV…

        [ValidateNever]
        public string VentaId
        {
            get => _ventaId;
            set => _ventaId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _ventaId = string.Empty;

        [Required(ErrorMessage = "Seleccione un producto.")]
        [Display(Name = "Producto")]
        public string ProductoId
        {
            get => _productoId;
            set => _productoId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _productoId = string.Empty;

        [Display(Name = "Código")]
        public string? CodigoProducto { get; set; }

        [Display(Name = "Nombre")]
        public string? NombreProducto { get; set; }

        [Display(Name = "Imagen")]
        public string? ImagenUrl { get; set; }

        [Required]
        [Range(0.01, 9999999, ErrorMessage = "Cantidad inválida.")]
        [Display(Name = "Cantidad")]
        public decimal Cantidad { get; set; } = 1;

        /// <summary>
        /// PVP vigente (PRECIO_HISTORICO con HASTA = NULL o <= FECHA).
        /// No depende del lote. Es lo que “ve” y paga el cliente.
        /// </summary>
        [Required]
        [Range(0, 9999999)]
        [DataType(DataType.Currency)]
        [Display(Name = "Precio unitario (PVP)")]
        public decimal PrecioUnitario { get; set; } = 0;

        [DataType(DataType.Currency)]
        [Display(Name = "Subtotal")]
        public decimal Subtotal => Math.Round(Cantidad * PrecioUnitario, 2);

        // -------- Costeo/Consumo (interno, para KARDEX) --------
        /// <summary>
        /// Resultado del algoritmo FEFO/FIFO: de qué lotes salió la cantidad.
        /// Con esto generas SALIDAS en KARDEX y actualizas INVENTARIO.
        /// </summary>
        [ValidateNever]
        public List<LoteConsumoVM> LotesConsumidos { get; set; } = new();

        /// <summary>
        /// COGS de la línea (opcional almacenar en detalle; útil para margen).
        /// </summary>
        [DataType(DataType.Currency)]
        [Display(Name = "Costo de la línea")]
        public decimal? CostoTotal { get; set; }




        [Display(Name = "Stock disponible")]
        public decimal StockDisponible { get; set; } = 0m;

        [Display(Name = "Próximo a vencer")]
        public DateTime? ProximoVencimiento { get; set; }


       

    }

    /// <summary>
    /// Un recibo (pago) aplicado a la venta (tabla RECIBO).
    /// </summary>
    public class ReciboItemVM
    {
        [ValidateNever] public string? ReciboId { get; set; } // correlativo REC…
        [ValidateNever] public string? VentaId { get; set; }

        [Required]
        [Display(Name = "Método de pago")]
        public string MetodoPagoId
        {
            get => _metodoPagoId;
            set => _metodoPagoId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _metodoPagoId = string.Empty;

        [Required]
        [Range(0.01, 999999999999.99)]
        [DataType(DataType.Currency)]
        [Display(Name = "Monto")]
        public decimal Monto { get; set; }

        [Display(Name = "Fecha")]
        public DateTime Fecha { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// DTO para devolver al frontend opciones de producto en el buscador de venta.
    /// </summary>
    public class ProductoVentaBusquedaItemVM
    {
        public string ProductoId { get; set; } = string.Empty;
        public string CodigoProducto { get; set; } = string.Empty;
        public string NombreProducto { get; set; } = string.Empty;
        public string? DescripcionProducto { get; set; }
        public string? ImagenUrl { get; set; }
        public string? InfoRapida { get; set; } // stock, UM, precio, etc.
    }

    /// <summary>
    /// UI de producto seleccionado para la ventanita “Agregar a venta”.
    /// </summary>
    public class ProductoVentaSeleccionadoVM
    {
        public string ProductoId { get; set; } = string.Empty;
        public string CodigoProducto { get; set; } = string.Empty;
        public string NombreProducto { get; set; } = string.Empty;
        public string? ImagenUrl { get; set; }

        [Display(Name = "Cantidad a vender")]
        [Range(0.01, 9999999)]
        public decimal Cantidad { get; set; } = 1;

        [Display(Name = "Precio unitario (PVP)")]
        [Range(0, 9999999)]
        [DataType(DataType.Currency)]
        public decimal PrecioUnitario { get; set; } = 0;
    }

    /// <summary>
    /// Resultado de selección FEFO/FIFO: por cada lote consumido en la línea.
    /// </summary>
    public class LoteConsumoVM
    {
        [Display(Name = "Inventario")]
        public string InventarioId { get; set; } = string.Empty;

        [Display(Name = "Producto")]
        public string ProductoId { get; set; } = string.Empty;

        [Display(Name = "Lote")]
        public string? LoteCodigo { get; set; }

        [Display(Name = "Vence")]
        public DateTime? FechaVencimiento { get; set; }

        [Display(Name = "Cantidad (sale)")]
        [Range(0.01, 9999999)]
        public decimal Cantidad { get; set; }

        [Display(Name = "Costo unitario (lote)")]
        [DataType(DataType.Currency)]
        public decimal CostoUnitario { get; set; }
    }

    // Reutilizamos el AuditoriaViewModel que ya definimos en CajaSesionViewModels.cs
    public class AuditoriaVM
    {
        [Display(Name = "Creado por")] public string? CreadoPor { get; set; }
        [Display(Name = "Fecha creación")] public DateTime? FechaCreacion { get; set; }
        [Display(Name = "Modificado por")] public string? ModificadoPor { get; set; }
        [Display(Name = "Fecha modificación")] public DateTime? FechaModificacion { get; set; }
        [Display(Name = "Eliminado")] public bool Eliminado { get; set; }
        [Display(Name = "Eliminado por")] public string? EliminadoPor { get; set; }
        [Display(Name = "Fecha eliminación")] public DateTime? FechaEliminacion { get; set; }
        [Display(Name = "Estado")] public string? ESTADO { get; set; }
    }
}
