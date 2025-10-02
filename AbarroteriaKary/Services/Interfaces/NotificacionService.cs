using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AbarroteriaKary.Data; // tu DbContext
using AbarroteriaKary.Services; // interfaz
using AbarroteriaKary.Models;   // tus entidades
using AbarroteriaKary.Services.Auditoria; // IAuditoriaService (el que ya usas)
using Microsoft.Data.SqlClient;


namespace AbarroteriaKary.Services
{
    public class NotificacionService : INotificacionService
    {
        private readonly KaryDbContext _ctx;
        private readonly IAuditoriaService _auditoria;

        public NotificacionService(KaryDbContext ctx, IAuditoriaService auditoria)
        {
            _ctx = ctx;
            _auditoria = auditoria;
        }

        

        public async Task<int> UpsertStockBajoAsync(string productoId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(productoId)) return 0;
            productoId = productoId.Trim().ToUpperInvariant();

            var ahora = DateTime.Now;
            var user = await _auditoria.GetUsuarioNombreAsync();

            var lineasQry = _ctx.INVENTARIO
                .Where(i => !i.ELIMINADO && (i.ESTADO ?? "ACTIVO") == "ACTIVO" && i.PRODUCTO_ID == productoId);

            var stockTotal = await lineasQry.SumAsync(i => (int?)i.STOCK_ACTUAL, ct) ?? 0;
            var umbral = 10; // ← fijo; si luego quieres Options, aquí se reemplaza.

            // Línea ancla
            var anchor = await lineasQry
                .OrderBy(i => i.FECHA_VENCIMIENTO == null)
                .ThenBy(i => i.FECHA_VENCIMIENTO)
                .ThenBy(i => i.INVENTARIO_ID)
                .FirstOrDefaultAsync(ct);

            // Si no hay líneas, resolvemos lo pendiente
            if (anchor == null)
            {
                var activas = await _ctx.NOTIFICACION
                    .Where(n => !n.ELIMINADO && n.ESTADO == "ACTIVO" && !n.RESUELTA
                             && n.TIPO == "STOCK_BAJO" && n.PRODUCTO_ID == productoId)
                    .ToListAsync(ct);

                foreach (var n in activas)
                {
                    n.RESUELTA = true;
                    n.RESUELTA_EN = ahora;
                    n.RESUELTA_POR = user;
                    n.MODIFICADO_POR = user;
                    n.FECHA_MODIFICACION = ahora;
                }
                return await _ctx.SaveChangesAsync(ct);
            }

            // Datos de producto para mensaje
            var prod = await _ctx.PRODUCTO.AsNoTracking()
                .Where(p => p.PRODUCTO_ID == productoId && !p.ELIMINADO)
                .Select(p => new { p.PRODUCTO_ID, p.PRODUCTO_NOMBRE })
                .FirstOrDefaultAsync(ct);

            if (stockTotal <= umbral)
            {
                //var notif = await _ctx.NOTIFICACION.FirstOrDefaultAsync(n =>
                //        !n.ELIMINADO && n.ESTADO == "ACTIVO" && !n.RESUELTA
                //        && n.TIPO == "STOCK_BAJO"
                //        && n.INVENTARIO_ID == anchor.INVENTARIO_ID
                //        && n.PRODUCTO_ID == productoId, ct);


                //var notif = await _ctx.NOTIFICACION.FirstOrDefaultAsync(n =>
                //        !n.ELIMINADO && !n.RESUELTA
                //        && n.TIPO == "STOCK_BAJO"
                //        && n.INVENTARIO_ID == anchor.INVENTARIO_ID
                //        && n.PRODUCTO_ID == productoId, ct);

                var notif = await _ctx.NOTIFICACION.FirstOrDefaultAsync(n =>
                        !n.ELIMINADO && n.RESUELTA == false
                        && n.TIPO == "STOCK_BAJO"
                        && n.INVENTARIO_ID == anchor.INVENTARIO_ID
                        && n.PRODUCTO_ID == productoId, ct);

                var msg = $"STOCK BAJO — {prod?.PRODUCTO_NOMBRE ?? ""} ({productoId}). " +
                          $"Ya solo cuenta con {stockTotal} unidades.";

                if (notif == null)
                {
                    _ctx.NOTIFICACION.Add(new NOTIFICACION
                    {
                        INVENTARIO_ID = anchor.INVENTARIO_ID,
                        PRODUCTO_ID = productoId,
                        TIPO = "STOCK_BAJO",
                        MENSAJE = msg,
                        NIVEL = 4,
                        URL = $"/Inventarios?productoId={productoId}&lote={anchor.LOTE_CODIGO}",
                        FECHA_DETECCION = ahora,
                        RESUELTA = false,
                        ESTADO = "ACTIVO",
                        ELIMINADO = false,
                        CREADO_POR = user,
                        FECHA_CREACION = ahora,
                        MODIFICADO_POR = user,
                        FECHA_MODIFICACION = ahora
                    });
                }
                else
                {
                    notif.MENSAJE = msg;
                    notif.URL = $"/Inventarios?productoId={productoId}&lote={anchor.LOTE_CODIGO}";
                    notif.NIVEL = 4;
                    notif.MODIFICADO_POR = user;
                    notif.FECHA_MODIFICACION = ahora;
                }

                // Cerrar otras STOCK_BAJO del mismo producto (si hubiera)
                //var otras = await _ctx.NOTIFICACION
                //    .Where(n => !n.ELIMINADO && n.ESTADO == "ACTIVO" && !n.RESUELTA
                //             && n.TIPO == "STOCK_BAJO" && n.PRODUCTO_ID == productoId
                //             && n.INVENTARIO_ID != anchor.INVENTARIO_ID)
                //    .ToListAsync(ct);


                var otras = await _ctx.NOTIFICACION
                    .Where(n => !n.ELIMINADO && n.RESUELTA == false
                             && n.TIPO == "STOCK_BAJO"
                             && n.PRODUCTO_ID == productoId
                             && n.INVENTARIO_ID != anchor.INVENTARIO_ID)
                    .ToListAsync(ct);





                foreach (var n in otras)
                {
                    n.RESUELTA = true; n.RESUELTA_EN = ahora; n.RESUELTA_POR = user;
                    n.MODIFICADO_POR = user; n.FECHA_MODIFICACION = ahora;
                }
            }
            else
            {
                //// Subió sobre el umbral => resolver todas
                //var activas = await _ctx.NOTIFICACION
                //    .Where(n => !n.ELIMINADO && n.ESTADO == "ACTIVO" && !n.RESUELTA
                //             && n.TIPO == "STOCK_BAJO" && n.PRODUCTO_ID == productoId)
                //    .ToListAsync(ct);

                //var activas = await _ctx.NOTIFICACION
                //  .Where(n => !n.ELIMINADO && !n.RESUELTA
                //           && n.TIPO == "STOCK_BAJO" && n.PRODUCTO_ID == productoId)
                //  .ToListAsync(ct);
                var activas = await _ctx.NOTIFICACION
                  .Where(n => !n.ELIMINADO && n.RESUELTA == false
                           && n.TIPO == "STOCK_BAJO"
                           && n.PRODUCTO_ID == productoId)
                  .ToListAsync(ct);



                foreach (var n in activas)
                {
                    n.RESUELTA = true; n.RESUELTA_EN = ahora; n.RESUELTA_POR = user;
                    n.MODIFICADO_POR = user; n.FECHA_MODIFICACION = ahora;
                }
            }

            return await _ctx.SaveChangesAsync(ct);
        }






        public async Task<int> UpsertVencimientosAsync(string productoId, int diasUmbral = 15, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(productoId)) return 0;
            productoId = productoId.Trim().ToUpperInvariant();

            var hoy = DateTime.Today;
            var ahora = DateTime.Now;
            var user = await _auditoria.GetUsuarioNombreAsync();

            // Stock total del producto (para saltar notificación si está en 0)
            var stockTotalProducto = await _ctx.INVENTARIO
                .Where(i => !i.ELIMINADO && (i.ESTADO ?? "ACTIVO") == "ACTIVO" && i.PRODUCTO_ID == productoId)
                .SumAsync(i => (int?)i.STOCK_ACTUAL, ct) ?? 0;

            // Producto (para nombre)
            var prod = await _ctx.PRODUCTO.AsNoTracking()
                .Where(p => p.PRODUCTO_ID == productoId && !p.ELIMINADO)
                .Select(p => new { p.PRODUCTO_ID, p.PRODUCTO_NOMBRE })
                .FirstOrDefaultAsync(ct);

            var lineas = await _ctx.INVENTARIO
                .Where(i => !i.ELIMINADO && (i.ESTADO ?? "ACTIVO") == "ACTIVO"
                         && i.PRODUCTO_ID == productoId
                         && i.FECHA_VENCIMIENTO != null)
                .Select(i => new {
                    i.INVENTARIO_ID,
                    i.PRODUCTO_ID,
                    i.LOTE_CODIGO,
                    i.FECHA_VENCIMIENTO,
                    i.STOCK_ACTUAL
                })
                .ToListAsync(ct);

            int cambios = 0;

            foreach (var l in lineas)
            {
                // Si el producto en general no tiene stock, o esta línea está en 0 => resolver y saltar.
                if (stockTotalProducto <= 0 || l.STOCK_ACTUAL <= 0)
                {
                    //var activas = await _ctx.NOTIFICACION
                    //    .Where(n => !n.ELIMINADO && n.ESTADO == "ACTIVO" && !n.RESUELTA
                    //             && n.PRODUCTO_ID == l.PRODUCTO_ID
                    //             && n.INVENTARIO_ID == l.INVENTARIO_ID
                    //             && (n.TIPO == "POR_VENCER" || n.TIPO == "VENCIDO"))
                    //    .ToListAsync(ct);
                    var activas = await _ctx.NOTIFICACION
                            .Where(n => !n.ELIMINADO && n.RESUELTA == false
                                     && n.PRODUCTO_ID == l.PRODUCTO_ID
                                     && n.INVENTARIO_ID == l.INVENTARIO_ID
                                     && (n.TIPO == "POR_VENCER" || n.TIPO == "VENCIDO"))
                            .ToListAsync(ct);


                    foreach (var n in activas)
                    {
                        n.RESUELTA = true; n.RESUELTA_EN = ahora; n.RESUELTA_POR = user;
                        n.MODIFICADO_POR = user; n.FECHA_MODIFICACION = ahora; cambios++;
                    }
                    continue;
                }

                // Calcular días al vencimiento
                DateTime fv = new DateTime(l.FECHA_VENCIMIENTO!.Value.Year, l.FECHA_VENCIMIENTO.Value.Month, l.FECHA_VENCIMIENTO.Value.Day);
                int dias = (fv - hoy).Days;

                string? tipo = null;
                if (dias < 0) tipo = "VENCIDO";
                else if (dias <= diasUmbral) tipo = "POR_VENCER";

                // Si ya no aplica ninguna: resolver y seguir
                if (tipo == null)
                {
                    var activas = await _ctx.NOTIFICACION
                        .Where(n => !n.ELIMINADO && n.ESTADO == "ACTIVO" && !n.RESUELTA
                                 && n.PRODUCTO_ID == l.PRODUCTO_ID
                                 && n.INVENTARIO_ID == l.INVENTARIO_ID
                                 && (n.TIPO == "POR_VENCER" || n.TIPO == "VENCIDO"))
                        .ToListAsync(ct);
                    foreach (var n in activas)
                    {
                        n.RESUELTA = true; n.RESUELTA_EN = ahora; n.RESUELTA_POR = user;
                        n.MODIFICADO_POR = user; n.FECHA_MODIFICACION = ahora; cambios++;
                    }
                    continue;
                }

                // Mensaje bonito
                var msg = tipo == "VENCIDO"
                    ? $"VENCIDO — {prod?.PRODUCTO_NOMBRE ?? ""} ({productoId}) del lote {l.LOTE_CODIGO ?? "-"} venció el {fv:dd/MM/yyyy}."
                    : $"POR VENCER — {prod?.PRODUCTO_NOMBRE ?? ""} ({productoId}) del lote {l.LOTE_CODIGO ?? "-"}, vence el {fv:dd/MM/yyyy} (en {dias} días).";

                // Upsert de esa línea y tipo
                //var notif = await _ctx.NOTIFICACION.FirstOrDefaultAsync(n =>
                //        !n.ELIMINADO && n.ESTADO == "ACTIVO" && !n.RESUELTA
                //        && n.TIPO == tipo
                //        && n.INVENTARIO_ID == l.INVENTARIO_ID
                //        && n.PRODUCTO_ID == l.PRODUCTO_ID, ct);

                var notif = await _ctx.NOTIFICACION.FirstOrDefaultAsync(n =>
                    !n.ELIMINADO && n.RESUELTA == false
                    && n.TIPO == tipo
                    && n.INVENTARIO_ID == l.INVENTARIO_ID
                    && n.PRODUCTO_ID == l.PRODUCTO_ID, ct);


                var nivel = tipo == "VENCIDO" ? (byte)5 : (byte)3;
                var url = $"/Inventarios?productoId={l.PRODUCTO_ID}&lote={l.LOTE_CODIGO}";

                if (notif == null)
                {
                    _ctx.NOTIFICACION.Add(new NOTIFICACION
                    {
                        INVENTARIO_ID = l.INVENTARIO_ID,
                        PRODUCTO_ID = l.PRODUCTO_ID,
                        TIPO = tipo,
                        MENSAJE = msg,
                        URL = url,
                        NIVEL = nivel,
                        FECHA_DETECCION = ahora,
                        RESUELTA = false,
                        ESTADO = "ACTIVO",
                        ELIMINADO = false,
                        CREADO_POR = user,
                        FECHA_CREACION = ahora,
                        MODIFICADO_POR = user,
                        FECHA_MODIFICACION = ahora
                    });
                    cambios++;
                }
                else
                {
                    notif.MENSAJE = msg;
                    notif.URL = url;
                    notif.NIVEL = nivel;
                    notif.MODIFICADO_POR = user;
                    notif.FECHA_MODIFICACION = ahora;
                    cambios++;
                }

                // Si está VENCIDO, cerrar cualquier POR_VENCER previa de esa misma línea
                if (tipo == "VENCIDO")
                {
                    //var porVencerActivas = await _ctx.NOTIFICACION
                    //    .Where(n => !n.ELIMINADO && n.ESTADO == "ACTIVO" && !n.RESUELTA
                    //             && n.TIPO == "POR_VENCER"
                    //             && n.PRODUCTO_ID == l.PRODUCTO_ID
                    //             && n.INVENTARIO_ID == l.INVENTARIO_ID)
                    //    .ToListAsync(ct);

                    var porVencerActivas = await _ctx.NOTIFICACION
                            .Where(n => !n.ELIMINADO && n.RESUELTA == false
                                     && n.TIPO == "POR_VENCER"
                                     && n.PRODUCTO_ID == l.PRODUCTO_ID
                                     && n.INVENTARIO_ID == l.INVENTARIO_ID)
                            .ToListAsync(ct);

                    foreach (var n in porVencerActivas)
                    {
                        n.RESUELTA = true; n.RESUELTA_EN = ahora; n.RESUELTA_POR = user;
                        n.MODIFICADO_POR = user; n.FECHA_MODIFICACION = ahora; cambios++;
                    }
                }
            }

            return await _ctx.SaveChangesAsync(ct);
        }



       

        public async Task<int> RebuildVencimientosGlobalAsync(int diasUmbral = 15, CancellationToken ct = default)
        {
            // Recorre todos los productos activos que tengan al menos una línea con FV
            var productos = await _ctx.INVENTARIO
                .Where(i => !i.ELIMINADO && (i.ESTADO ?? "ACTIVO") == "ACTIVO" && i.FECHA_VENCIMIENTO != null)
                .Select(i => i.PRODUCTO_ID)
                .Distinct()
                .ToListAsync(ct);

            int total = 0;
            foreach (var pid in productos)
                total += await UpsertVencimientosAsync(pid, diasUmbral, ct);

            return total;
        }


    }



}
