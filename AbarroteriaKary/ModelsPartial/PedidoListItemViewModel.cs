// ModelsPartial/PedidoListItemViewModel.cs
namespace AbarroteriaKary.ModelsPartial
{
    public class PedidoListItemViewModel
    {
        public string PedidoId { get; set; } = "";
        public string Empresa { get; set; } = "";     // PROVEEDOR.EMPRESA
        public string? FechaPedidoTxt { get; set; }   // dd/MM/yyyy
        public string? FechaEntregaTxt { get; set; }  // dd/MM/yyyy
        public string? EstadoNombre { get; set; }     // ESTADO_PEDIDO.ESTADO_PEDIDO_NOMBRE
        public int Lineas { get; set; }               // # de DETALLE_PEDIDO
        public string ESTADO { get; set; } = "ACTIVO"; // lógico (ACTIVO/INACTIVO)
    }
}
