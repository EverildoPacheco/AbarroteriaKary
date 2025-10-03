// Services/Ventas/IVentaTxService.cs
using System.Threading;
using System.Threading.Tasks;
using AbarroteriaKary.ModelsPartial;

namespace AbarroteriaKary.Services.Ventas
{
    public record VentaTxResult(string VentaId, decimal Total);

    public interface IVentaTxService
    {
        Task<VentaTxResult> ConfirmarVentaAsync(
            VentaViewModel vm, VentaPagoViewModel pago, string usuarioNombre,
            CancellationToken ct = default);
    }
}
