using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;                  // <-- IMPORTANTE
using Microsoft.EntityFrameworkCore;
using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using Microsoft.EntityFrameworkCore.Storage; // <-- necesario para GetDbTransaction()


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


        //----------------------------------------------------------


        public async Task<string> PeekNextPedidosIdAsync(CancellationToken ct = default)
            => await PeekAsync("PED", 7, _context.PEDIDO.Select(x => x.PEDIDO_ID), ct);

        public async Task<string> NextPedidosIdAsync(CancellationToken ct = default)
            => await NextAsync("PED", 7, _context.PEDIDO.Select(x => x.PEDIDO_ID), ct);





        //----------------------------------------------------------

        public async Task<string> PeekNextDetallePedidosIdAsync(CancellationToken ct = default)
            => await PeekAsync("DET", 7, _context.DETALLE_PEDIDO.Select(x => x.DETALLE_PEDIDO_ID), ct);

        // obsoleto
        public async Task<string> NextDetallePedidosIdAsync(CancellationToken ct = default)
            => await NextAsync("DET", 7, _context.DETALLE_PEDIDO.Select(x => x.DETALLE_PEDIDO_ID), ct);











        //  SEQUENCE por rangos para DETALLE_PEDIDO

        /// <summary>
        /// Reserva un rango de IDs "DET0000001" .. "DET00000N" desde el SEQUENCE dbo.SEQ_DETALLE_PEDIDO.
        /// Un solo round-trip a BD. Ideal para insertar muchos DETALLE_PEDIDO con un SaveChanges().
        /// </summary>
        public async Task<IReadOnlyList<string>> NextDetallePedidosRangeAsync(int count, CancellationToken ct = default)
        {
            if (count <= 0) return Array.Empty<string>();

            var db = _context.Database;
            var conn = db.GetDbConnection(); // NO usar await using aquí

            var openedHere = false;
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync(ct);
                openedHere = true;
            }

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "sys.sp_sequence_get_range";
                cmd.CommandType = CommandType.StoredProcedure;

                // Enlazar a la MISMA transacción de EF si existe
                var current = db.CurrentTransaction;
                if (current != null)
                {
                    var dbTx = current.GetDbTransaction();
                    cmd.Transaction = dbTx;
                }

                var pSeqName = new SqlParameter("@sequence_name", SqlDbType.NVarChar, 128) { Value = "dbo.SEQ_DETALLE_PEDIDO" };
                var pSize = new SqlParameter("@range_size", SqlDbType.Int) { Value = count };
                var pFirst = new SqlParameter("@range_first_value", SqlDbType.Variant) { Direction = ParameterDirection.Output };

                cmd.Parameters.Add(pSeqName);
                cmd.Parameters.Add(pSize);
                cmd.Parameters.Add(pFirst);

                await cmd.ExecuteNonQueryAsync(ct);

                var firstVal = Convert.ToInt64(pFirst.Value);
                var ids = new List<string>(count);
                for (int i = 0; i < count; i++)
                {
                    var n = firstVal + i;
                    ids.Add($"DET{n.ToString().PadLeft(7, '0')}");
                }
                return ids;
            }
            finally
            {
                // Si la abriste tú, CIERRALA, pero NO la “disposees”
                if (openedHere && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }



        /// <summary>
        /// Obtiene un solo ID de detalle desde el SEQUENCE (NEXT VALUE FOR).
        /// Úsalo solo si realmente necesitas una unidad (para rangos usa NextDetallePedidosRangeAsync).
        /// </summary>
        public async Task<string> NextDetallePedidoFromSequenceAsync(CancellationToken ct = default)
        {
            var db = _context.Database;
            var conn = db.GetDbConnection();

            var openedHere = false;
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync(ct);
                openedHere = true;
            }

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT NEXT VALUE FOR dbo.SEQ_DETALLE_PEDIDO;";
                cmd.CommandType = CommandType.Text;

                var current = db.CurrentTransaction;
                if (current != null)
                    cmd.Transaction = current.GetDbTransaction();

                var result = await cmd.ExecuteScalarAsync(ct);
                var val = Convert.ToInt64(result);
                return $"DET{val.ToString().PadLeft(7, '0')}";
            }
            finally
            {
                if (openedHere && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }







        // INVENTARIO
        public async Task<string> PeekNextInventarioIdAsync(CancellationToken ct = default)
            => await PeekAsync("INV", 7, _context.INVENTARIO.Select(x => x.INVENTARIO_ID), ct);

        //public async Task<string> NextInventarioIdAsync(CancellationToken ct = default)
        //    => await NextAsync("INV", 7, _context.INVENTARIO.Select(x => x.INVENTARIO_ID), ct);


        public async Task<string> NextInventarioIdAsync(CancellationToken ct = default)
        {
            const string prefix = "INV";
            const int width = 7;

            // 1) último ID en BD (orden lexicográfico sirve por el zero-padding)
            var lastId = await _context.INVENTARIO.AsNoTracking()
                .Where(x => x.INVENTARIO_ID.StartsWith(prefix)
                         && x.INVENTARIO_ID.Length == prefix.Length + width)
                .OrderByDescending(x => x.INVENTARIO_ID)
                .Select(x => x.INVENTARIO_ID)
                .FirstOrDefaultAsync(ct);

            var maxDb = lastId is null ? 0 : int.Parse(lastId.Substring(prefix.Length, width));

            // 2) máximo reservado en el ChangeTracker dentro de la misma transacción
            var maxLocal = _context.ChangeTracker.Entries<INVENTARIO>()
                .Where(e => e.State == EntityState.Added
                         && e.Entity.INVENTARIO_ID != null
                         && e.Entity.INVENTARIO_ID.StartsWith(prefix)
                         && e.Entity.INVENTARIO_ID.Length == prefix.Length + width)
                .Select(e => int.Parse(e.Entity.INVENTARIO_ID.Substring(prefix.Length, width)))
                .DefaultIfEmpty(0)
                .Max();

            var next = Math.Max(maxDb, maxLocal) + 1;
            return prefix + next.ToString().PadLeft(width, '0');
        }









        // KARDEX
        public async Task<string> PeekNextKardexIdAsync(CancellationToken ct = default)
            => await PeekAsync("KAR", 7, _context.KARDEX.Select(x => x.KARDEX_ID), ct);

        public async Task<string> NextKardexIdAsync(CancellationToken ct = default)
        {
            const string prefix = "KAR";
            const int width = 7;

            var lastId = await _context.KARDEX.AsNoTracking()
                .Where(x => x.KARDEX_ID.StartsWith(prefix)
                         && x.KARDEX_ID.Length == prefix.Length + width)
                .OrderByDescending(x => x.KARDEX_ID)
                .Select(x => x.KARDEX_ID)
                .FirstOrDefaultAsync(ct);

            var maxDb = lastId is null ? 0 : int.Parse(lastId.Substring(prefix.Length, width));

            var maxLocal = _context.ChangeTracker.Entries<KARDEX>()
                .Where(e => e.State == EntityState.Added
                         && e.Entity.KARDEX_ID != null
                         && e.Entity.KARDEX_ID.StartsWith(prefix)
                         && e.Entity.KARDEX_ID.Length == prefix.Length + width)
                .Select(e => int.Parse(e.Entity.KARDEX_ID.Substring(prefix.Length, width)))
                .DefaultIfEmpty(0)
                .Max();

            var next = Math.Max(maxDb, maxLocal) + 1;
            return prefix + next.ToString().PadLeft(width, '0');
        }






        // PRECIO_HISTORICO (PK = PRECIO_ID)
        public async Task<string> PeekNextPrecioHistoricoIdAsync(CancellationToken ct = default)
            => await PeekAsync("PCH", 7, _context.PRECIO_HISTORICO.Select(x => x.PRECIO_ID), ct);
        public async Task<string> NextPrecioHistoricoIdAsync(CancellationToken ct = default)
        {
            const string prefix = "PCH";
            const int width = 7;

            var lastId = await _context.PRECIO_HISTORICO.AsNoTracking()
                .Where(x => x.PRECIO_ID.StartsWith(prefix)
                         && x.PRECIO_ID.Length == prefix.Length + width)
                .OrderByDescending(x => x.PRECIO_ID)
                .Select(x => x.PRECIO_ID)
                .FirstOrDefaultAsync(ct);

            var maxDb = lastId is null ? 0 : int.Parse(lastId.Substring(prefix.Length, width));

            var maxLocal = _context.ChangeTracker.Entries<PRECIO_HISTORICO>()
                .Where(e => e.State == EntityState.Added
                         && e.Entity.PRECIO_ID != null
                         && e.Entity.PRECIO_ID.StartsWith(prefix)
                         && e.Entity.PRECIO_ID.Length == prefix.Length + width)
                .Select(e => int.Parse(e.Entity.PRECIO_ID.Substring(prefix.Length, width)))
                .DefaultIfEmpty(0)
                .Max();

            var next = Math.Max(maxDb, maxLocal) + 1;
            return prefix + next.ToString().PadLeft(width, '0');
        }














        // ----------------- Núcleo reutilizable que ya tenías -----------------
        private static async Task<string> PeekAsync(
            string prefix, int digits, IQueryable<string> idSource, CancellationToken ct)
        {
            var last = await idSource
                .Where(id => id.StartsWith(prefix))
                .OrderByDescending(id => id)
                .FirstOrDefaultAsync(ct);

            return FormatNext(prefix, digits, last);
        }

        private static async Task<string> NextAsync(
            string prefix, int digits, IQueryable<string> idSource, CancellationToken ct)
        {
            var last = await idSource
                .Where(id => id.StartsWith(prefix))
                .OrderByDescending(id => id)
                .FirstOrDefaultAsync(ct);

            return FormatNext(prefix, digits, last);
        }

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



