namespace AbarroteriaKary.ModelsPartial
{
    public class PedidoReporteDetalleViewModel
    {
        public string CorrelativoDetalle { get; set; } = "";   // DETALLE_PEDIDO_ID
        public string? CodigoProducto { get; set; }
        public string? idproducto { get; set; }

        public string? NombreProducto { get; set; }
        public string? DescripcionProducto { get; set; }
        public string? ImagenUrl { get; set; }                 // PRODUCTO.PRODUCTO_IMG
        public int Cantidad { get; set; }
    }
}
