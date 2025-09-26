using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AbarroteriaKary.ModelsPartial
{
    /// <summary>
    /// VM principal para crear/editar/visualizar un Pedido (encabezado + detalle).
    /// </summary>
    public class PedidoViewModel : IValidatableObject
    {
        [Display(Name = "No. Pedido")]
        public string PedidoId
        {
            get => _pedidoId;
            set => _pedidoId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _pedidoId = string.Empty;

        // (Opcional) Descomentar solo si existe columna NO_ORDEN/folio en PEDIDO
        /*
        [Display(Name = "No. Orden")]
        [StringLength(30)]
        public string NumeroOrden
        {
            get => _numeroOrden;
            set => _numeroOrden = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _numeroOrden = string.Empty;
        */

        [Required(ErrorMessage = "Seleccione un proveedor.")]
        [Display(Name = "Proveedor")]
        public string ProveedorId
        {
            get => _proveedorId;
            set => _proveedorId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _proveedorId = string.Empty;

        [ValidateNever]
        public List<SelectListItem> Proveedores { get; set; } = new();

        [Display(Name = "Creado por (usuario)")]
        public string UsuarioCreadorId
        {
            get => _usuarioCreadorId;
            set => _usuarioCreadorId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _usuarioCreadorId = string.Empty;

        //[Display(Name = "Fecha del pedido")]
        //[DataType(DataType.Date)]
        //public DateTime? FechaPedido { get; set; }


        [Display(Name = "Fecha del pedido")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? FechaPedido { get; set; }

        //[Display(Name = "Fecha posible de entrega")]
        //[DataType(DataType.Date)]
        //public DateTime? FechaPosibleEntrega { get; set; }

        [Display(Name = "Fecha posible de entrega")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? FechaPosibleEntrega { get; set; }




        // --- Estado del Pedido (catálogo ESTADO_PEDIDO) ---
        [Required(ErrorMessage = "Seleccione el estado del pedido.")]
        [Display(Name = "Estado del pedido")]
        [StringLength(10)]
        public string EstadoPedidoId
        {
            get => _estadoPedidoId;
            set => _estadoPedidoId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _estadoPedidoId = string.Empty; // ej. ESTP00001

        [Display(Name = "Estado (nombre)")]
        public string? EstadoPedidoNombre { get; set; }

        [ValidateNever]
        public List<SelectListItem> Estados { get; set; } = new();



        //[Display(Name = "Observación")]
        //[StringLength(500)]
        //public string Observacion
        //{
        //    get => _observacion;
        //    set => _observacion = (value ?? string.Empty).Trim();
        //}
        //private string _observacion = string.Empty;


        [Display(Name = "Observación")]
        [StringLength(500)]
        public string? Observacion
        {
            get => _observacion;
            set => _observacion = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        private string? _observacion;


        [Display(Name = "¿Activo?")]
        public bool EstadoActivo { get; set; } = true;

        [ValidateNever]
        public AuditoriaViewModel Auditoria { get; set; } = new();

        [Display(Name = "Buscar producto")]
        public string? Q { get; set; }

        public ProductoSeleccionadoVM? ProductoSeleccionado { get; set; }

        [MinLength(0)]
        [ValidateNever]
        public List<PedidoDetalleItemVM> Lineas { get; set; } = new();

        [Display(Name = "Total estimado")]
        [DataType(DataType.Currency)]
        public decimal TotalEstimado => CalcularTotal();

        private decimal CalcularTotal()
        {
            decimal total = 0m;
            foreach (var it in Lineas) total += it.Subtotal;
            return total;
        }

        /// <summary>
        /// Validaciones de negocio del encabezado + detalle.
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // 1) Debe existir al menos 1 línea
            if (Lineas == null || Lineas.Count == 0)
                yield return new ValidationResult("Debe agregar al menos un producto al pedido.", new[] { nameof(Lineas) });

            // 2) Cantidades > 0 y ProductoId presente
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

            // 3) Fechas coherentes
            if (FechaPedido.HasValue && FechaPosibleEntrega.HasValue && FechaPosibleEntrega.Value.Date < FechaPedido.Value.Date)
                yield return new ValidationResult("La fecha posible de entrega no puede ser anterior a la fecha del pedido.", new[] { nameof(FechaPosibleEntrega) });
        }
    }

    public class PedidoDetalleItemVM
    {
        [ValidateNever]
        public string? DetalleId { get; set; } // se genera con Correlativo (DET…)

        [ValidateNever]
        public string PedidoId
        {
            get => _pedidoId;
            set => _pedidoId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _pedidoId = string.Empty;

        [Required(ErrorMessage = "Seleccione un producto.")]
        [Display(Name = "Producto")]
        public string ProductoId
        {
            get => _productoId;
            set => _productoId = (value ?? string.Empty).Trim().ToUpper();
        }
        private string _productoId = string.Empty;

        [Display(Name = "Código producto")]
        public string? CodigoProducto { get; set; }

        [Display(Name = "Nombre")]
        public string? NombreProducto { get; set; }

        [Display(Name = "Descripción")]
        public string? DescripcionProducto { get; set; }

        [Display(Name = "Imagen")]
        public string? ImagenUrl { get; set; }

        [Required]
        [Range(0.01, 9999999, ErrorMessage = "Cantidad inválida.")]
        [Display(Name = "Cantidad")]
        public decimal Cantidad { get; set; }

        [Display(Name = "Precio unitario (ref.)")]
        [Range(0, 9999999)]
        [DataType(DataType.Currency)]
        public decimal PrecioUnitario { get; set; }

        [Display(Name = "Subtotal")]
        [DataType(DataType.Currency)]
        public decimal Subtotal => Math.Round(Cantidad * PrecioUnitario, 2);

        [ValidateNever]
        public bool MarcadoEliminar { get; set; } = false;

        [Display(Name = "Estado (línea)")]
        public string? ESTADO { get; set; }




        // NUEVOS para Edit:
        public string? DetallePedidoId { get; set; }       // null -> línea nueva
        public decimal? PrecioPedido { get; set; }      // PRECIO_PEDIDO (compra)
        public decimal? PrecioVenta { get; set; }       // PRECIO_VENTA
        public DateOnly? FechaVencimiento { get; set; } // FECHA_VENCIMIENTO (DATE en SQL; mapea a DateTime? o DateOnly? según tu modelo)







    }

    public class ProductoSeleccionadoVM
    {
        public string ProductoId { get; set; } = string.Empty;
        public string CodigoProducto { get; set; } = string.Empty;
        public string NombreProducto { get; set; } = string.Empty;
        public string? DescripcionProducto { get; set; }
        public string? ImagenUrl { get; set; }

        [Display(Name = "Cantidad a agregar")]
        [Range(0.01, 9999999)]
        public decimal Cantidad { get; set; } = 1;

        [Display(Name = "Precio unitario (ref.)")]
        [Range(0, 9999999)]
        [DataType(DataType.Currency)]
        public decimal PrecioUnitario { get; set; } = 0;
    }

    /// <summary>
    /// DTO de resultados para el buscador (endpoint JSON).
    /// </summary>
    public class ProductoBusquedaItemVM
    {
        public string ProductoId { get; set; } = string.Empty;
        public string CodigoProducto { get; set; } = string.Empty;
        public string NombreProducto { get; set; } = string.Empty;
        public string? DescripcionProducto { get; set; }
        public string? ImagenUrl { get; set; }
        public string? InfoRapida { get; set; } // UM, stock, etc. opcional
    }
}
