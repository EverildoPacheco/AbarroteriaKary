namespace AbarroteriaKary.ModelsPartial
{
    public class KardexViewModel
    {
        public string KardexId { get; set; } = default!;
        public DateTime Fecha { get; set; }
        public string ProductoId { get; set; } = default!;
        public string? CodigoProducto { get; set; }
        public string? NombreProducto { get; set; }
        public string TipoMovimiento { get; set; } = default!;
        public int Cantidad { get; set; }
        public decimal? CostoUnitario { get; set; }
        public string? LoteCodigo { get; set; }
        public string? Referencia { get; set; }
        public string? Motivo { get; set; }
        public string? Estado { get; set; }
    }
}
