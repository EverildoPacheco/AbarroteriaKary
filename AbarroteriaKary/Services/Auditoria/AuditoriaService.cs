using AbarroteriaKary.Data;
using AbarroteriaKary.Models; // KaryDbContext, USUARIO
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AbarroteriaKary.Services.Auditoria
{
    /// <summary>
    /// Implementación del servicio de auditoría:
    /// - Lee "UsuarioNombre" desde Claims (recomendado).
    /// - Si no existe, intenta con "USUARIO_ID" para buscar en DB el USUARIO_NOMBRE.
    /// - Fallback: User.Identity.Name o "Sistema".
    /// - Usa HttpContext.Items como cache por request.
    /// </summary>
    public class AuditoriaService : IAuditoriaService
    {
        private readonly IHttpContextAccessor _http;
        private readonly KaryDbContext _context;
        private const string CacheKey = "__Auditoria.UsuarioNombre";

        public AuditoriaService(IHttpContextAccessor http, KaryDbContext context)
        {
            _http = http;
            _context = context;
        }

        public async Task<string> GetUsuarioNombreAsync()
        {
            var httpContext = _http.HttpContext;
            var items = httpContext?.Items;

            // 0) Cache por request (evita buscar varias veces)
            if (items != null && items.TryGetValue(CacheKey, out var cached))
            {
                if (cached is string s && !string.IsNullOrWhiteSpace(s))
                    return s;
            }

            var user = httpContext?.User;

            // 1) Claim "UsuarioNombre" (ideal que su login lo emita)
            var claimNombre = user?.Claims?.FirstOrDefault(c =>
                c.Type == "UsuarioNombre" ||
                c.Type == ClaimTypes.Name ||
                c.Type == "name")?.Value;

            if (!string.IsNullOrWhiteSpace(claimNombre))
            {
                if (items != null) items[CacheKey] = claimNombre;
                return claimNombre;
            }

            // 2) Claim "USUARIO_ID" -> lookup en DB para USUARIO_NOMBRE
            var userId = user?.Claims?.FirstOrDefault(c => c.Type == "USUARIO_ID")?.Value;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var u = await _context.USUARIO
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.USUARIO_ID == userId);

                if (u != null && !string.IsNullOrWhiteSpace(u.USUARIO_NOMBRE))
                {
                    if (items != null) items[CacheKey] = u.USUARIO_NOMBRE;
                    return u.USUARIO_NOMBRE;
                }
            }

            // 3) Fallback
            var name = user?.Identity?.Name;
            var finalName = string.IsNullOrWhiteSpace(name) ? "Sistema" : name;

            if (items != null) items[CacheKey] = finalName;
            return finalName;
        }
    }
}
