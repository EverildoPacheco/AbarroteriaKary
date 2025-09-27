using System.Threading;
using System.Threading.Tasks;

namespace AbarroteriaKary.Services.Inventario
{
    // Resultado útil para métricas en UI/log
    public record PosteoPedidoResultado(int LineasPosteadas, int LineasOmitidas, bool PedidoCerrado);

    public interface IInventarioPostingService
    {
        /// <summary>
        /// Postea (aplica a inventario) todas las líneas NO posteadas (POSTEADO=0) del pedido.
        /// Reglas:
        ///  - Suma/crea renglón en INVENTARIO por (PRODUCTO_ID, FECHA_VENCIMIENTO, LOTE_CODIGO).
        ///  - Inserta KARDEX (ENTRADA) con FECHA = ahora.
        ///  - Cierra/abre PRECIO_HISTORICO si cambió PRECIO_VENTA.
        ///  - Marca DETALLE_PEDIDO.POSTEADO=1 / POSTEADO_EN=ahora.
        ///  - Si todo quedó posteado -> cambia PEDIDO a CERRADO.
        /// </summary>
        Task<PosteoPedidoResultado> PostearPedidoAsync(string pedidoId, string usuario, CancellationToken ct = default);
    }
}
