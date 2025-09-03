using System.Threading.Tasks;

namespace AbarroteriaKary.Services.Auditoria
{
    /// <summary>
    /// Servicio para obtener el nombre del usuario actual a registrar en auditoría.
    /// </summary>
    public interface IAuditoriaService
    {
        /// <summary>
        /// Devuelve el nombre del usuario actual a usar en auditoría (CREADO_POR, MODIFICADO_POR, ELIMINADO_POR).
        /// Prioriza claims, luego DB USUARIO y por último User.Identity.Name o "Sistema".
        /// Implementa micro-caché por request.
        /// </summary>
        Task<string> GetUsuarioNombreAsync();
    }
}
