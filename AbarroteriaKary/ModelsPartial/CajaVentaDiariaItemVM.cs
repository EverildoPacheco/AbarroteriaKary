namespace AbarroteriaKary.ModelsPartial
{
    public class CajaVentaDiariaItemVM
    {
        public string VentaId { get; set; } = "";
        public DateTime Fecha { get; set; }
        public string ClienteId { get; set; } = "";
        public string? ClienteNombre { get; set; }
        public decimal Total { get; set; }
    }

    //public class CajaVentaDiariaPdfVM
    //{
    //    public string SesionId { get; set; } = "";
    //    public string CajaId { get; set; } = "";
    //    public string CajaNombre { get; set; } = "";
    //    public DateTime FechaApertura { get; set; }
    //    public DateTime FechaCorte { get; set; }
    //    public string UsuarioNombre { get; set; } = "";
    //    public decimal Total { get; set; }
    //    public IList<CajaVentaDiariaItemVM> Ventas { get; set; } = new List<CajaVentaDiariaItemVM>();
    //}


    public class VentaDiariaDetLineaVM
    {
        public string ProductoId { get; set; } = "";
        public string Nombre { get; set; } = "";
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal => Math.Round(Cantidad * PrecioUnitario, 2);
    }

    public class VentaDiariaDetVentaVM
    {
        public string VentaId { get; set; } = "";
        public DateTime Fecha { get; set; }

        public string ClienteId { get; set; } = "";
        public string? ClienteNombre { get; set; }

        public string UsuarioId { get; set; } = "";
        public string? UsuarioNombre { get; set; }

        public decimal Total { get; set; }
        public IList<VentaDiariaDetLineaVM> Lineas { get; set; } = new List<VentaDiariaDetLineaVM>();
    }

    public class VentaDiariaDetPdfVM
    {
        public string SesionId { get; set; } = "";
        public string CajaId { get; set; } = "";
        public string CajaNombre { get; set; } = "";
        public DateTime FechaApertura { get; set; }
        public DateTime FechaCorte { get; set; }
        public string UsuarioNombre { get; set; } = ""; // quien genera el reporte
        public decimal Total { get; set; }
        public string? LogoUrl { get; set; }      // p.ej. Url.Content("~/img/logo-mariposa.png")

        public IList<VentaDiariaDetVentaVM> Ventas { get; set; } = new List<VentaDiariaDetVentaVM>();
    }
}
