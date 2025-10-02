using System.Threading;
using System.Threading.Tasks;

namespace AbarroteriaKary.Services
{
    public interface INotificacionService
    {
        /// <summary>
        /// Verifica stock consolidado del producto y crea/actualiza o resuelve NOTIFICACION tipo STOCK_BAJO.
        /// </summary>
        Task<int> UpsertStockBajoAsync(string productoId, CancellationToken ct = default);

        /// <summary>
        /// Revisa todas las líneas (por lote/vencimiento) del producto y crea/actualiza/resolve
        /// NOTIFICACION tipo POR_VENCER / VENCIDO según días al vencimiento.
        /// </summary>
        Task<int> UpsertVencimientosAsync(string productoId, int diasUmbral = 15, CancellationToken ct = default);

        /// <summary>
        /// Barrido global (opcional) para POR_VENCER/VENCIDO.
        /// </summary>
        Task<int> RebuildVencimientosGlobalAsync(int diasUmbral = 15, CancellationToken ct = default);
    }
}
