using System;
using System.Collections.Generic;

namespace AbarroteriaKary.ModelsPartial
{
    public class ReciboLineaVM
    {
        public string ProductoId { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string? Descripcion { get; set; }  // <--- NUEVO
        public decimal Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }

        public decimal Subtotal => Math.Round(Cantidad * PrecioUnitario, 2);
    }

    public class ReciboPdfVM
    {
        public string VentaId { get; set; } = "";
        public DateTime Fecha { get; set; }

        public string? ClienteId { get; set; }
        public string? ClienteNombre { get; set; }
        public string? ClienteNit { get; set; }

        public string? UsuarioId { get; set; }
        public string? UsuarioNombre { get; set; }

        public string? ReciboId { get; set; }
        public string? MetodoPagoNombre { get; set; }

        public decimal Total { get; set; }
        public decimal? EfectivoRecibido { get; set; }
        public decimal? Cambio { get; set; }

        public List<ReciboLineaVM> Lineas { get; set; } = new();

        // Cabecera tienda
        public string? SucursalNombre { get; set; }
        public string? SucursalDireccion { get; set; }
        public string? SucursalNit { get; set; }
    }



    public sealed class ClienteUiDto
    {
        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
        public string? Nit { get; set; }
    }



}
