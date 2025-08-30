using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AbarroteriaKary.Data;   // su DbContext
using AbarroteriaKary.Models; // sus entidades (AREA, PUESTO, etc.)

namespace AbarroteriaKary.Services.Correlativos
{
    /// <summary>
    /// Lógica de correlativos con prefijo + dígitos.
    /// Núcleo reutilizable + métodos por entidad para mantener tipado y claridad.
    /// </summary>
    public class CorrelativoService : ICorrelativoService
    {
        private readonly KaryDbContext _context;
        public CorrelativoService(KaryDbContext context) => _context = context;

        // ----------------- Área -----------------
        public async Task<string> PeekNextAreaIdAsync(CancellationToken ct = default)
            => await PeekAsync("AREA", 6, _context.AREA.Select(a => a.AREA_ID), ct);

        public async Task<string> NextAreaIdAsync(CancellationToken ct = default)
            => await NextAsync("AREA", 6, _context.AREA.Select(a => a.AREA_ID), ct);

        // ----------------- Puesto -----------------
        public async Task<string> PeekNextPuestoIdAsync(CancellationToken ct = default)
            => await PeekAsync("PUE", 7, _context.PUESTO.Select(p => p.PUESTO_ID), ct);

        public async Task<string> NextPuestoIdAsync(CancellationToken ct = default)
            => await NextAsync("PUE", 7, _context.PUESTO.Select(p => p.PUESTO_ID), ct);








        // ----------------- Núcleo reutilizable -----------------
        /// <summary>
        /// Obtiene un “siguiente” tentativo (solo mostrar), sin bloquear.
        /// </summary>
        private static async Task<string> PeekAsync(
            string prefix, int digits, IQueryable<string> idSource, CancellationToken ct)
        {
            var last = await idSource
                .Where(id => id.StartsWith(prefix))
                .OrderByDescending(id => id)
                .FirstOrDefaultAsync(ct);

            return FormatNext(prefix, digits, last);
        }

        /// <summary>
        /// Obtiene el “siguiente” para insertar (llamar dentro de la transacción del POST).
        /// </summary>
        private static async Task<string> NextAsync(
            string prefix, int digits, IQueryable<string> idSource, CancellationToken ct)
        {
            var last = await idSource
                .Where(id => id.StartsWith(prefix))
                .OrderByDescending(id => id)
                .FirstOrDefaultAsync(ct);

            return FormatNext(prefix, digits, last);
        }

        /// <summary>
        /// Formatea: PREFIJO + número con ceros a la izquierda.
        /// </summary>
        private static string FormatNext(string prefix, int digits, string lastId)
        {
            var totalLen = prefix.Length + digits;
            var next = 1;

            if (!string.IsNullOrWhiteSpace(lastId) && lastId.Length == totalLen)
            {
                var numPart = lastId.Substring(prefix.Length);
                if (int.TryParse(numPart, out var n))
                    next = n + 1;
            }

            return $"{prefix}{next.ToString($"D{digits}")}";
        }
    }
}
