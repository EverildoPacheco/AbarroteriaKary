using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AbarroteriaKary.ModelsPartial
{
    public class HistorialSesionItemVM
    {
        [Display(Name = "Sesión")] 
        public string SesionId { get; set; } = "";

        [Display(Name = "Caja")] 
        public string? CajaNombre { get; set; }

        [Display(Name = "Apertura")]
        public DateTime FechaApertura { get; set; }

        [Display(Name = "Cierre")] 
        public DateTime? FechaCierre { get; set; }

        [Display(Name = "Usuario cierre")] 
        public string? UsuarioCierreNombre { get; set; }

        [Display(Name = "Total ventas")] 
        public decimal TotalVentas { get; set; }
    }

    public class HistorialVentaItemVM
    {
        [Display(Name = "Venta")] 
        public string VentaId { get; set; } = "";
        [Display(Name = "Fecha/Hora")] 

        public DateTime Fecha { get; set; }
        [Display(Name = "Cliente")] 
        public string? ClienteId { get; set; }

        public string? ClienteNombre { get; set; } 

        [Display(Name = "Vendedor")] 
        public string? UsuarioId { get; set; }

        public string? UsuarioNombre { get; set; }   

        [Display(Name = "Total")] 
        public decimal Total { get; set; }
    }

    public class HistorialVentaDetalleLineaVM
    {
        [Display(Name = "Producto")] public string? ProductoId { get; set; }
        public string? Nombre { get; set; }
        public int Cantidad { get; set; }
        [Display(Name = "Precio")] public decimal PrecioUnitario { get; set; }
        public decimal Subtotal => Math.Round(Cantidad * PrecioUnitario, 2);
    }

    public class HistorialVentaDetalleVM
    {

        public string SesionId { get; set; } = "";

        public string VentaId { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string? ClienteId { get; set; }
        public string? ClienteNombre { get; set; }

        public string? UsuarioId { get; set; }
        public string? UsuarioNombre { get; set; }
        public decimal Total { get; set; }
        public List<HistorialVentaDetalleLineaVM> Lineas { get; set; } = new();
    }
}
