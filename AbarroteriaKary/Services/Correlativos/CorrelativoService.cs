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


        // ----------------- empleado -----------------
        public async Task<string> PeekNextPersonaIdAsync(CancellationToken ct = default)
          => await PeekAsync("PER", 7, _context.PERSONA.Select(x => x.PERSONA_ID), ct);

        public async Task<string> NextPersonaIdAsync(CancellationToken ct = default)
            => await NextAsync("PER", 7, _context.PERSONA.Select(x => x.PERSONA_ID), ct);

        // ----------------- ROL -----------------
        public async Task<string> PeekNextRolIdAsync(CancellationToken ct = default)
          => await PeekAsync("ROL", 7, _context.ROL.Select(x => x.ROL_ID), ct);

        public async Task<string> NextRolIdAsync(CancellationToken ct = default)
            => await NextAsync("ROL", 7, _context.ROL.Select(x => x.ROL_ID), ct);

        // ----------------- USUARIO -----------------
        public async Task<string> PeekNextUsuarioIdAsync(CancellationToken ct = default)
          => await PeekAsync("USU", 7, _context.USUARIO.Select(x => x.USUARIO_ID), ct);

        public async Task<string> NextUsuarioIdAsync(CancellationToken ct = default)
            => await NextAsync("USU", 7, _context.USUARIO.Select(x => x.USUARIO_ID), ct);

        // ----------------- PROVEEDOR -----------------

        public async Task<string> PeekNextProveedorIdAsync(CancellationToken ct = default)
         => await PeekAsync("PRO", 7, _context.PROVEEDOR.Select(x => x.PROVEEDOR_ID), ct);

        public async Task<string> NextProveedorIdAsync(CancellationToken ct = default)
            => await NextAsync("PRO", 7, _context.PROVEEDOR.Select(x => x.PROVEEDOR_ID), ct);

        // ----------------- CLIENTE -----------------


        public async Task<string> PeekNextClienteIdIdAsync(CancellationToken ct = default)
             => await PeekAsync("CLI", 7, _context.CLIENTE.Select(x => x.CLIENTE_ID), ct);

        public async Task<string> NextClienteIdIdAsync(CancellationToken ct = default)
            => await NextAsync("CLI", 7, _context.CLIENTE.Select(x => x.CLIENTE_ID), ct);
        //-----------------------------------------------------
        public async Task<string> PeekNextCategoriaIdAsync(CancellationToken ct = default)
             => await PeekAsync("CAT", 7, _context.CATEGORIA.Select(x => x.CATEGORIA_ID), ct);

        public async Task<string> NextCategoriaIdAsync(CancellationToken ct = default)
            => await NextAsync("CAT", 7, _context.CATEGORIA.Select(x => x.CATEGORIA_ID), ct);

        //-----------------------------------------------------
        public async Task<string> PeekNextSubCategoriaIdAsync(CancellationToken ct = default)
     => await PeekAsync("SUB", 7, _context.SUBCATEGORIA.Select(x => x.SUBCATEGORIA_ID), ct);

        public async Task<string> NextSubCategoriaIdAsync(CancellationToken ct = default)
            => await NextAsync("SUB", 7, _context.SUBCATEGORIA.Select(x => x.SUBCATEGORIA_ID), ct);

        //-----------------------------------------------------
        public async Task<string> PeekNextTipoProductoIdAsync(CancellationToken ct = default)
            => await PeekAsync("TIP", 7, _context.TIPO_PRODUCTO.Select(x => x.TIPO_PRODUCTO_ID), ct);

        public async Task<string> NextSubTipoProductoIdAsync(CancellationToken ct = default)
            => await NextAsync("TIP", 7, _context.TIPO_PRODUCTO.Select(x => x.TIPO_PRODUCTO_ID), ct);

        //-----------------------------------------------------
        public async Task<string> PeekNextMateriaEnvaseIdAsync(CancellationToken ct = default)
            => await PeekAsync("MAT", 7, _context.MATERIAL_ENVASE.Select(x => x.MATERIAL_ENVASE_ID), ct);

        public async Task<string> NextMateriaEnvaseIdAsync(CancellationToken ct = default)
            => await NextAsync("MAT", 7, _context.MATERIAL_ENVASE.Select(x => x.MATERIAL_ENVASE_ID), ct);
        //-----------------------------------------------------
        public async Task<string> PeekNextTipoEmpaqueIdAsync(CancellationToken ct = default)
                 => await PeekAsync("TEM", 7, _context.TIPO_EMPAQUE.Select(x => x.TIPO_EMPAQUE_ID), ct);

        public async Task<string> NextTipoEmpaqueIdAsync(CancellationToken ct = default)
            => await NextAsync("TEM", 7, _context.TIPO_EMPAQUE.Select(x => x.TIPO_EMPAQUE_ID), ct);

        //-----------------------------------------------------
        public async Task<string> PeekNextUnidadDeMedidaIdAsync(CancellationToken ct = default)
         => await PeekAsync("MED", 7, _context.UNIDAD_MEDIDA.Select(x => x.UNIDAD_MEDIDA_ID), ct);

        public async Task<string> NextUnidadDeMedidaIdAsync(CancellationToken ct = default)
            => await NextAsync("MED", 7, _context.UNIDAD_MEDIDA.Select(x => x.UNIDAD_MEDIDA_ID), ct);


        public async Task<string> PeekNextMarcaIdAsync(CancellationToken ct = default)
            => await PeekAsync("MAR", 7, _context.MARCA.Select(x => x.MARCA_ID), ct);

        public async Task<string> NextMarcaIdAsync(CancellationToken ct = default)
            => await NextAsync("MAR", 7, _context.MARCA.Select(x => x.MARCA_ID), ct);

        //----------------------------------------------------------
        public async Task<string> PeekNextProductoIdAsync(CancellationToken ct = default)
         => await PeekAsync("PRD", 7, _context.PRODUCTO.Select(x => x.PRODUCTO_ID), ct);

        public async Task<string> NextProductoIdAsync(CancellationToken ct = default)
            => await NextAsync("PRD", 7, _context.PRODUCTO.Select(x => x.PRODUCTO_ID), ct);



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
