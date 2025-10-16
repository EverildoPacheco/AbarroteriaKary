using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using Microsoft.Data.SqlClient;                  // IMPORTANTE
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;     // GetDbTransaction()
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AbarroteriaKary.Services.Correlativos
{
    /// <summary>
    /// Servicio de correlativos (prefijo + dígitos).
    /// Incluye modo LEGACY (leer último en tabla) y modo SEQUENCE (SQL Server).
    /// </summary>
    public class CorrelativoService : ICorrelativoService
    {
        private readonly KaryDbContext _context;


        private static readonly IReadOnlyDictionary<string, (string seq, int pad, string prefix)> _series =
            new Dictionary<string, (string seq, int pad, string prefix)>(StringComparer.OrdinalIgnoreCase)
            {
                // PERMISOS: PE00000001
                { "PE", ("SEQ_PERMISOS", 8, "PE") },

               
            };




        public CorrelativoService(KaryDbContext context) => _context = context;

        // =======================
        // Helpers (comunes)
        // =======================
        private static string BuildId(string prefix, long n, int width)
            => prefix + n.ToString().PadLeft(width, '0');

        // ---- LEGACY (lee el último ID en la tabla) ----
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

        // ---- SEQUENCE helpers (SQL Server) ----
        private async Task<string> NextFromSequenceAsync(
            string sequenceName, string prefix, int width, CancellationToken ct)
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
                cmd.CommandText = $"SELECT NEXT VALUE FOR {sequenceName};";
                cmd.CommandType = CommandType.Text;

                if (db.CurrentTransaction != null)
                    cmd.Transaction = db.CurrentTransaction.GetDbTransaction();

                var result = await cmd.ExecuteScalarAsync(ct);
                var val = Convert.ToInt64(result);
                return BuildId(prefix, val, width);
            }
            finally
            {
                if (openedHere && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }

        private async Task<IReadOnlyList<string>> NextRangeFromSequenceAsync(
            string sequenceName, string prefix, int width, int count, CancellationToken ct)
        {
            if (count <= 0) return Array.Empty<string>();

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
                cmd.CommandText = "sys.sp_sequence_get_range";
                cmd.CommandType = CommandType.StoredProcedure;

                if (db.CurrentTransaction != null)
                    cmd.Transaction = db.CurrentTransaction.GetDbTransaction();

                var pSeqName = new SqlParameter("@sequence_name", SqlDbType.NVarChar, 128) { Value = sequenceName };
                var pSize = new SqlParameter("@range_size", SqlDbType.Int) { Value = count };
                var pFirst = new SqlParameter("@range_first_value", SqlDbType.Variant) { Direction = ParameterDirection.Output };

                cmd.Parameters.Add(pSeqName);
                cmd.Parameters.Add(pSize);
                cmd.Parameters.Add(pFirst);

                await cmd.ExecuteNonQueryAsync(ct);

                var firstVal = Convert.ToInt64(pFirst.Value);
                var list = new List<string>(count);
                for (int i = 0; i < count; i++)
                    list.Add(BuildId(prefix, firstVal + i, width));

                return list;
            }
            finally
            {
                if (openedHere && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }

        private async Task<string> PeekFromSequenceAsync(
            string sequenceName, string prefix, int width, CancellationToken ct)
        {
            // NO consume: lee current_value en sys.sequences y suma 1
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
                cmd.CommandText = @"
                    SELECT CAST(current_value AS bigint)
                    FROM sys.sequences
                    WHERE name = @name;";
                cmd.CommandType = CommandType.Text;

                // Si viene como 'dbo.SEQ_X', nos quedamos con el nombre
                var onlyName = sequenceName.Contains('.')
                    ? sequenceName.Split('.')[1]
                    : sequenceName;

                var p = new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = onlyName };
                cmd.Parameters.Add(p);

                if (db.CurrentTransaction != null)
                    cmd.Transaction = db.CurrentTransaction.GetDbTransaction();

                var result = await cmd.ExecuteScalarAsync(ct);
                var curr = (result == null || result == DBNull.Value) ? 0L : Convert.ToInt64(result);

                return BuildId(prefix, curr + 1, width);
            }
            finally
            {
                if (openedHere && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }

        // =========================
        // ÁREA
        // =========================
        public Task<string> PeekNextAreaIdAsync(CancellationToken ct = default)
            => PeekAsync("AREA", 6, _context.AREA.Select(a => a.AREA_ID), ct);

        public Task<string> NextAreaIdAsync(CancellationToken ct = default)
            => NextAsync("AREA", 6, _context.AREA.Select(a => a.AREA_ID), ct);

        // =========================
        // PUESTO
        // =========================
        public Task<string> PeekNextPuestoIdAsync(CancellationToken ct = default)
            => PeekAsync("PUE", 7, _context.PUESTO.Select(p => p.PUESTO_ID), ct);

        public Task<string> NextPuestoIdAsync(CancellationToken ct = default)
            => NextAsync("PUE", 7, _context.PUESTO.Select(p => p.PUESTO_ID), ct);

        // =========================
        // PERSONA
        // =========================
        public Task<string> PeekNextPersonaIdAsync(CancellationToken ct = default)
            => PeekAsync("PER", 7, _context.PERSONA.Select(x => x.PERSONA_ID), ct);

        public Task<string> NextPersonaIdAsync(CancellationToken ct = default)
            => NextAsync("PER", 7, _context.PERSONA.Select(x => x.PERSONA_ID), ct);

        // =========================
        // ROL
        // =========================
        public Task<string> PeekNextRolIdAsync(CancellationToken ct = default)
            => PeekAsync("ROL", 7, _context.ROL.Select(x => x.ROL_ID), ct);

        public Task<string> NextRolIdAsync(CancellationToken ct = default)
            => NextAsync("ROL", 7, _context.ROL.Select(x => x.ROL_ID), ct);

        // =========================
        // USUARIO
        // =========================
        public Task<string> PeekNextUsuarioIdAsync(CancellationToken ct = default)
            => PeekAsync("USU", 7, _context.USUARIO.Select(x => x.USUARIO_ID), ct);

        public Task<string> NextUsuarioIdAsync(CancellationToken ct = default)
            => NextAsync("USU", 7, _context.USUARIO.Select(x => x.USUARIO_ID), ct);

        // =========================
        // PROVEEDOR
        // =========================
        public Task<string> PeekNextProveedorIdAsync(CancellationToken ct = default)
            => PeekAsync("PRO", 7, _context.PROVEEDOR.Select(x => x.PROVEEDOR_ID), ct);

        public Task<string> NextProveedorIdAsync(CancellationToken ct = default)
            => NextAsync("PRO", 7, _context.PROVEEDOR.Select(x => x.PROVEEDOR_ID), ct);

        // =========================
        // CLIENTE
        // =========================
        public Task<string> PeekNextClienteIdIdAsync(CancellationToken ct = default)
            => PeekAsync("CLI", 7, _context.CLIENTE.Select(x => x.CLIENTE_ID), ct);

        public Task<string> NextClienteIdIdAsync(CancellationToken ct = default)
            => NextAsync("CLI", 7, _context.CLIENTE.Select(x => x.CLIENTE_ID), ct);

        // =========================
        // CATEGORÍA / SUBCATEGORÍA
        // =========================
        public Task<string> PeekNextCategoriaIdAsync(CancellationToken ct = default)
            => PeekAsync("CAT", 7, _context.CATEGORIA.Select(x => x.CATEGORIA_ID), ct);
        public Task<string> NextCategoriaIdAsync(CancellationToken ct = default)
            => NextAsync("CAT", 7, _context.CATEGORIA.Select(x => x.CATEGORIA_ID), ct);

        public Task<string> PeekNextSubCategoriaIdAsync(CancellationToken ct = default)
            => PeekAsync("SUB", 7, _context.SUBCATEGORIA.Select(x => x.SUBCATEGORIA_ID), ct);
        public Task<string> NextSubCategoriaIdAsync(CancellationToken ct = default)
            => NextAsync("SUB", 7, _context.SUBCATEGORIA.Select(x => x.SUBCATEGORIA_ID), ct);

        // =========================
        // TIPO PRODUCTO / MATERIAL / EMPAQUE / U.M.
        // =========================
        public Task<string> PeekNextTipoProductoIdAsync(CancellationToken ct = default)
            => PeekAsync("TIP", 7, _context.TIPO_PRODUCTO.Select(x => x.TIPO_PRODUCTO_ID), ct);

        public Task<string> NextSubTipoProductoIdAsync(CancellationToken ct = default)
            => NextAsync("TIP", 7, _context.TIPO_PRODUCTO.Select(x => x.TIPO_PRODUCTO_ID), ct);

        public Task<string> PeekNextMateriaEnvaseIdAsync(CancellationToken ct = default)
            => PeekAsync("MAT", 7, _context.MATERIAL_ENVASE.Select(x => x.MATERIAL_ENVASE_ID), ct);

        public Task<string> NextMateriaEnvaseIdAsync(CancellationToken ct = default)
            => NextAsync("MAT", 7, _context.MATERIAL_ENVASE.Select(x => x.MATERIAL_ENVASE_ID), ct);

        public Task<string> PeekNextTipoEmpaqueIdAsync(CancellationToken ct = default)
            => PeekAsync("TEM", 7, _context.TIPO_EMPAQUE.Select(x => x.TIPO_EMPAQUE_ID), ct);

        public Task<string> NextTipoEmpaqueIdAsync(CancellationToken ct = default)
            => NextAsync("TEM", 7, _context.TIPO_EMPAQUE.Select(x => x.TIPO_EMPAQUE_ID), ct);

        public Task<string> PeekNextUnidadDeMedidaIdAsync(CancellationToken ct = default)
            => PeekAsync("MED", 7, _context.UNIDAD_MEDIDA.Select(x => x.UNIDAD_MEDIDA_ID), ct);

        public Task<string> NextUnidadDeMedidaIdAsync(CancellationToken ct = default)
            => NextAsync("MED", 7, _context.UNIDAD_MEDIDA.Select(x => x.UNIDAD_MEDIDA_ID), ct);

        public Task<string> PeekNextMarcaIdAsync(CancellationToken ct = default)
            => PeekAsync("MAR", 7, _context.MARCA.Select(x => x.MARCA_ID), ct);

        public Task<string> NextMarcaIdAsync(CancellationToken ct = default)
            => NextAsync("MAR", 7, _context.MARCA.Select(x => x.MARCA_ID), ct);

        // =========================
        // PRODUCTO
        // =========================
        public Task<string> PeekNextProductoIdAsync(CancellationToken ct = default)
            => PeekAsync("PRD", 7, _context.PRODUCTO.Select(x => x.PRODUCTO_ID), ct);

        public Task<string> NextProductoIdAsync(CancellationToken ct = default)
            => NextAsync("PRD", 7, _context.PRODUCTO.Select(x => x.PRODUCTO_ID), ct);

        // =========================
        // PEDIDO (tabla) + DETALLE_PEDIDO (sequence por rangos)
        // =========================
        public Task<string> PeekNextPedidosIdAsync(CancellationToken ct = default)
            => PeekAsync("PED", 7, _context.PEDIDO.Select(x => x.PEDIDO_ID), ct);

        public Task<string> NextPedidosIdAsync(CancellationToken ct = default)
            => NextAsync("PED", 7, _context.PEDIDO.Select(x => x.PEDIDO_ID), ct);

        public Task<string> PeekNextDetallePedidosIdAsync(CancellationToken ct = default)
            => PeekAsync("DET", 7, _context.DETALLE_PEDIDO.Select(x => x.DETALLE_PEDIDO_ID), ct);

        // (obsoleto pero mantenido por compatibilidad)
        public Task<string> NextDetallePedidosIdAsync(CancellationToken ct = default)
            => NextAsync("DET", 7, _context.DETALLE_PEDIDO.Select(x => x.DETALLE_PEDIDO_ID), ct);

        // Rangos con dbo.SEQ_DETALLE_PEDIDO (si lo tienes creado en BD)
        public async Task<IReadOnlyList<string>> NextDetallePedidosRangeAsync(int count, CancellationToken ct = default)
        {
            if (count <= 0) return Array.Empty<string>();

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
                cmd.CommandText = "sys.sp_sequence_get_range";
                cmd.CommandType = CommandType.StoredProcedure;

                var current = db.CurrentTransaction;
                if (current != null)
                    cmd.Transaction = current.GetDbTransaction();

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
                if (openedHere && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }

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

        // =========================
        // INVENTARIO (manejo local + BD)
        // =========================
        public Task<string> PeekNextInventarioIdAsync(CancellationToken ct = default)
            => PeekAsync("INV", 7, _context.INVENTARIO.Select(x => x.INVENTARIO_ID), ct);

        public async Task<string> NextInventarioIdAsync(CancellationToken ct = default)
        {
            const string prefix = "INV";
            const int width = 7;

            // último en BD
            var lastId = await _context.INVENTARIO.AsNoTracking()
                .Where(x => x.INVENTARIO_ID.StartsWith(prefix)
                         && x.INVENTARIO_ID.Length == prefix.Length + width)
                .OrderByDescending(x => x.INVENTARIO_ID)
                .Select(x => x.INVENTARIO_ID)
                .FirstOrDefaultAsync(ct);

            var maxDb = lastId is null ? 0 : int.Parse(lastId.Substring(prefix.Length, width));

            // máximo ya reservado en ChangeTracker (misma transacción)
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

        // =========================
        // KARDEX (similar a inventario)
        // =========================
        public Task<string> PeekNextKardexIdAsync(CancellationToken ct = default)
            => PeekAsync("KAR", 7, _context.KARDEX.Select(x => x.KARDEX_ID), ct);

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

        // =========================
        // PRECIO_HISTORICO
        // =========================
        public Task<string> PeekNextPrecioHistoricoIdAsync(CancellationToken ct = default)
            => PeekAsync("PCH", 7, _context.PRECIO_HISTORICO.Select(x => x.PRECIO_ID), ct);

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

        // =========================
        // CAJA
        // =========================
        public Task<string> PeekNextCajaIdAsync(CancellationToken ct = default)
            => PeekAsync("CAJ", 7, _context.CAJA.Select(x => x.CAJA_ID), ct);

        public Task<string> NextCajaIdAsync(CancellationToken ct = default)
            => NextAsync("CAJ", 7, _context.CAJA.Select(x => x.CAJA_ID), ct);

        // =========================
        // CAJA_SESION (legacy)
        // =========================
        public Task<string> PeekNextCajaSesionIdAsync(CancellationToken ct = default)
            => PeekAsync("SES", 7, _context.CAJA_SESION.Select(x => x.SESION_ID), ct);

        public Task<string> NextCajaSesionIdAsync(CancellationToken ct = default)
            => NextAsync("SES", 7, _context.CAJA_SESION.Select(x => x.SESION_ID), ct);

        // =========================
        // MOVIMIENTO_CAJA (SEQUENCE)
        // =========================
        public Task<string> PeekNextMovimientoCajaIdAsync(CancellationToken ct = default)
            => PeekFromSequenceAsync("dbo.SEQ_MOVIMIENTO_CAJA", "M", 9, ct);

        public Task<string> NextMovimientoCajaIdAsync(CancellationToken ct = default)
            => NextFromSequenceAsync("dbo.SEQ_MOVIMIENTO_CAJA", "M", 9, ct);

        public Task<IReadOnlyList<string>> NextMovimientoCajaRangeAsync(int count, CancellationToken ct = default)
            => NextRangeFromSequenceAsync("dbo.SEQ_MOVIMIENTO_CAJA", "M", 9, count, ct);

        // =========================
        // VENTA (SEQUENCE)
        // =========================
        public Task<string> PeekNextVentaIdAsync(CancellationToken ct = default)
            => PeekFromSequenceAsync("dbo.SEQ_VENTA", "V", 9, ct);

        public Task<string> NextVentaIdAsync(CancellationToken ct = default)
            => NextFromSequenceAsync("dbo.SEQ_VENTA", "V", 9, ct);

        public Task<IReadOnlyList<string>> NextVentaRangeAsync(int count, CancellationToken ct = default)
            => NextRangeFromSequenceAsync("dbo.SEQ_VENTA", "V", 9, count, ct);

        // =========================
        // DETALLE_VENTA (SEQUENCE)
        // =========================
        public Task<string> PeekNextDetalleVentaIdAsync(CancellationToken ct = default)
            => PeekFromSequenceAsync("dbo.SEQ_DETALLE_VENTA", "D", 9, ct);

        public Task<string> NextDetalleVentaIdAsync(CancellationToken ct = default)
            => NextFromSequenceAsync("dbo.SEQ_DETALLE_VENTA", "D", 9, ct);

        public Task<IReadOnlyList<string>> NextDetalleVentaRangeAsync(int count, CancellationToken ct = default)
            => NextRangeFromSequenceAsync("dbo.SEQ_DETALLE_VENTA", "D", 9, count, ct);

        // =========================
        // RECIBO (SEQUENCE)
        // =========================
        public Task<string> PeekNextReciboIdAsync(CancellationToken ct = default)
            => PeekFromSequenceAsync("dbo.SEQ_RECIBO", "R", 9, ct);

        public Task<string> NextReciboIdAsync(CancellationToken ct = default)
            => NextFromSequenceAsync("dbo.SEQ_RECIBO", "R", 9, ct);

        public Task<IReadOnlyList<string>> NextReciboRangeAsync(int count, CancellationToken ct = default)
            => NextRangeFromSequenceAsync("dbo.SEQ_RECIBO", "R", 9, count, ct);







        // ============================
        // API de alto nivel (por prefijo)
        // ============================

        /// Ej.: "PER" => "PE0000001"
        public async Task<string> NextAsync(string prefix, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("Prefix requerido.", nameof(prefix));

            if (!_series.TryGetValue(prefix, out var cfg))
                throw new InvalidOperationException($"No hay secuencia configurada para el prefijo '{prefix}'.");

            var n = await NextNumberAsync(cfg.seq, ct);
            return cfg.prefix + n.ToString().PadLeft(cfg.pad, '0');
        }

        /// Reserva count valores y devuelve la lista ya formateada.
        public async Task<IReadOnlyList<string>> NextRangeAsync(string prefix, int count, CancellationToken ct = default)
        {
            if (count <= 0) return Array.Empty<string>();

            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("Prefix requerido.", nameof(prefix));

            if (!_series.TryGetValue(prefix, out var cfg))
                throw new InvalidOperationException($"No hay secuencia configurada para el prefijo '{prefix}'.");

            var (first, size) = await ReserveRangeAsync(cfg.seq, count, ct);
            var list = new List<string>(size);
            for (var i = 0; i < size; i++)
                list.Add(cfg.prefix + (first + i).ToString().PadLeft(cfg.pad, '0'));

            return list;
        }

        // Conveniencias “por serie”
        public Task<string> NextPermisoAsync(CancellationToken ct = default)
            => NextAsync("PE", ct);

        public Task<IReadOnlyList<string>> NextPermisosRangeAsync(int count, CancellationToken ct = default)
            => NextRangeAsync("PE", count, ct);

        // ============================
        // Bajo nivel
        // ============================

        /// Retorna un único número crudo desde una SEQUENCE (thread-safe en SQL).
        public async Task<long> NextNumberAsync(string sequenceName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sequenceName))
                throw new ArgumentException("Nombre de secuencia requerido.", nameof(sequenceName));

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
                cmd.CommandText = $"SELECT NEXT VALUE FOR dbo.{sequenceName};";
                cmd.CommandType = CommandType.Text;

                // Compartimos transacción si EF tiene una activa
                var current = db.CurrentTransaction;
                if (current != null)
                    cmd.Transaction = current.GetDbTransaction();

                var result = await cmd.ExecuteScalarAsync(ct);
                return Convert.ToInt64(result);
            }
            finally
            {
                if (openedHere && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }

        /// Reserva un rango vía sp_sequence_get_range y retorna (first, size real).
        public async Task<(long first, int size)> ReserveRangeAsync(string sequenceName, int count, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sequenceName))
                throw new ArgumentException("Nombre de secuencia requerido.", nameof(sequenceName));
            if (count <= 0) return (0L, 0);

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
                cmd.CommandText = "sys.sp_sequence_get_range";
                cmd.CommandType = CommandType.StoredProcedure;

                // Transacción actual de EF si existe
                var current = db.CurrentTransaction;
                if (current != null)
                    cmd.Transaction = current.GetDbTransaction();

                var pSeqName = new SqlParameter("@sequence_name", SqlDbType.NVarChar, 128) { Value = $"dbo.{sequenceName}" };
                var pSize = new SqlParameter("@range_size", SqlDbType.Int) { Value = count };
                var pFirst = new SqlParameter("@range_first_value", SqlDbType.Variant) { Direction = ParameterDirection.Output };

                cmd.Parameters.Add(pSeqName);
                cmd.Parameters.Add(pSize);
                cmd.Parameters.Add(pFirst);

                await cmd.ExecuteNonQueryAsync(ct);

                var firstVal = Convert.ToInt64(pFirst.Value);
                return (firstVal, count);
            }
            finally
            {
                if (openedHere && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }


    }
}
