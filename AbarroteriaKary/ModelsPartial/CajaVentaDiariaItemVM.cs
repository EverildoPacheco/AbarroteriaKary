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

    public class CajaVentaDiariaPdfVM
    {
        public string SesionId { get; set; } = "";
        public string CajaId { get; set; } = "";
        public string CajaNombre { get; set; } = "";
        public DateTime FechaApertura { get; set; }
        public DateTime FechaCorte { get; set; }
        public string UsuarioNombre { get; set; } = "";
        public decimal Total { get; set; }
        public IList<CajaVentaDiariaItemVM> Ventas { get; set; } = new List<CajaVentaDiariaItemVM>();
    }
}
