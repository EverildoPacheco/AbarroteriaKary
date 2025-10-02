using AbarroteriaKary.Data;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.ModelsPartial.Paginacion;
using AbarroteriaKary.Services.Auditoria;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AbarroteriaKary.Controllers
{
    public class NotificacionesController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly IAuditoriaService _auditoria;

        public NotificacionesController(KaryDbContext context, IAuditoriaService auditoria)
        {
            _context = context;
            _auditoria = auditoria;
        }

        private async Task<string> GetUserAsync() =>
            await _auditoria.GetUsuarioNombreAsync() ?? "SYSTEM";

        // GET: /Notificaciones/Count
        //[HttpGet]
        //public async Task<IActionResult> Count(CancellationToken ct)
        //{
        //    var hoy = DateTime.Now;
        //    var count = await _context.NOTIFICACION
        //        .AsNoTracking()
        //        .Where(n => !n.ELIMINADO
        //                 && n.ESTADO == "ACTIVO"
        //                 && (n.RESUELTA == false || n.RESUELTA == null)
        //                 && (n.FECHA_PROGRAMADA == null || n.FECHA_PROGRAMADA <= hoy))
        //        .CountAsync(ct);

        //    return Json(new { count });
        //}

        [HttpGet]
        public async Task<IActionResult> Count(CancellationToken ct)
        {
            var hoy = DateTime.Now;
            var count = await _context.NOTIFICACION
                .AsNoTracking()
                .Where(n => !n.ELIMINADO
                         && n.ESTADO == "ACTIVO"
                         && (n.RESUELTA == false || n.RESUELTA == null)
                         && (n.FECHA_PROGRAMADA == null || n.FECHA_PROGRAMADA <= hoy)
                         && (n.SNOOZE_HASTA == null || n.SNOOZE_HASTA <= hoy))   // ← NUEVO
                .CountAsync(ct);

            return Json(new { count });
        }





        //[HttpGet]
        //public async Task<IActionResult> Dropdown(int top = 10, CancellationToken ct = default)
        //{
        //    var hoy = DateTime.Now;
        //    var q = (from n in _context.NOTIFICACION.AsNoTracking()
        //             join i in _context.INVENTARIO.AsNoTracking() on n.INVENTARIO_ID equals i.INVENTARIO_ID
        //             join p in _context.PRODUCTO.AsNoTracking() on n.PRODUCTO_ID equals p.PRODUCTO_ID
        //             where !n.ELIMINADO && n.ESTADO == "ACTIVO"
        //                && (n.RESUELTA == false || n.RESUELTA == null)
        //                && (n.FECHA_PROGRAMADA == null || n.FECHA_PROGRAMADA <= hoy)
        //             orderby (n.FECHA_ENVIO ?? n.FECHA_PROGRAMADA ?? n.FECHA_CREACION) descending
        //             select new NotificacionVM
        //             {
        //                 NotificacionId = n.NOTIFICACION_ID,
        //                 Tipo = n.TIPO,
        //                 Mensaje = n.MENSAJE,
        //                 ProductoId = p.PRODUCTO_ID,
        //                 CodigoProducto = p.PRODUCTO_CODIGO,
        //                 NombreProducto = p.PRODUCTO_NOMBRE,
        //                 LoteCodigo = i.LOTE_CODIGO,
        //                 Fecha = n.FECHA_CREACION,
        //                 Nivel = n.NIVEL
        //             })
        //            .Take(top);

        //    var vm = await q.ToListAsync(ct);
        //    return PartialView("~/Views/Notificaciones/_Dropdown.cshtml", vm);
        //}


        [HttpGet]
        public async Task<IActionResult> Dropdown(int top = 10, CancellationToken ct = default)
        {
            var hoy = DateTime.Now;

            var q = (from n in _context.NOTIFICACION.AsNoTracking()
                     join i in _context.INVENTARIO.AsNoTracking() on n.INVENTARIO_ID equals i.INVENTARIO_ID
                     join p in _context.PRODUCTO.AsNoTracking() on n.PRODUCTO_ID equals p.PRODUCTO_ID
                     where !n.ELIMINADO
                        && n.ESTADO == "ACTIVO"
                        && (n.RESUELTA == false || n.RESUELTA == null)
                        && (n.FECHA_PROGRAMADA == null || n.FECHA_PROGRAMADA <= hoy)
                        && (n.SNOOZE_HASTA == null || n.SNOOZE_HASTA <= hoy)                 // ← EXCLUYE SNOOZE
                     orderby (n.FECHA_ENVIO ?? n.FECHA_PROGRAMADA ?? n.FECHA_CREACION) descending
                     select new NotificacionVM
                     {
                         NotificacionId = n.NOTIFICACION_ID,
                         Tipo = n.TIPO,
                         Mensaje = n.MENSAJE,
                         ProductoId = p.PRODUCTO_ID,
                         CodigoProducto = p.PRODUCTO_CODIGO,
                         NombreProducto = p.PRODUCTO_NOMBRE,
                         LoteCodigo = i.LOTE_CODIGO,
                         Fecha = n.FECHA_CREACION,
                         Nivel = n.NIVEL,

                         // DateOnly? -> DateTime?
                         FechaVencimiento = i.FECHA_VENCIMIENTO.HasValue
                             ? new DateTime(i.FECHA_VENCIMIENTO.Value.Year,
                                            i.FECHA_VENCIMIENTO.Value.Month,
                                            i.FECHA_VENCIMIENTO.Value.Day)
                             : (DateTime?)null
                     })
                    .Take(top);

            var list = await q.ToListAsync(ct);

            // Construir URL profunda por ítem
            foreach (var n in list)
            {
                var code = string.IsNullOrWhiteSpace(n.ProductoId) ? n.ProductoId : n.ProductoId; // AQUI SE CAMBIA SI QUIERO FILTRAR POR OTRA COSA 
                // Rango de fechas (para POR_VENCER/VENCIDO)
                //var fDesde = DateTime.Today.ToString("yyyy-MM-dd");
                //var fHasta = n.FechaVencimiento?.ToString("yyyy-MM-dd");

                var esStockBajo = string.Equals(n.Tipo, "STOCK_BAJO", StringComparison.OrdinalIgnoreCase);
                var esVenc = string.Equals(n.Tipo, "POR_VENCER", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(n.Tipo, "VENCIDO", StringComparison.OrdinalIgnoreCase);



                // Valores por defecto (ajusta los nombres si tu Index usa otros)
                var route = new Dictionary<string, object?>
                {
                    ["modo"] = esStockBajo ? "CONSOLIDADO" : "DETALLADO",
                    ["q"] = code
                };

                if (string.Equals(n.Tipo, "POR_VENCER", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(n.Tipo, "VENCIDO", StringComparison.OrdinalIgnoreCase))
                {
                    // rango por fecha de vencimiento (usa los nombres que tu Index espera)
                    route["fvDesde"] = DateTime.Today.ToString("yyyy-MM-dd");
                    if (n.FechaVencimiento.HasValue)
                        route["fvHasta"] = n.FechaVencimiento.Value.ToString("yyyy-MM-dd");

                    // opcional: mostrar solo líneas con stock > 0
                    route["soloConStock"] = true;
                }

                if (string.Equals(n.Tipo, "STOCK_BAJO", StringComparison.OrdinalIgnoreCase))
                {
                    route["stock"] = "BAJO";      // activa el chip de stock bajo en tu vista
                    route["soloConStock"] = true;        // opcional
                }

                n.Url = Url.Action("Index", "Inventarios", route)!;
            }

            return PartialView("~/Views/Notificaciones/_Dropdown.cshtml", list);
        }





        // GET: /Notificaciones/Poll
        // Devuelve “debidas” (PENDIENTE/PROGRAMADA y sin snooze vigente) y las marca ENVIADA
        [HttpGet]
        public async Task<IActionResult> Poll(CancellationToken ct)
        {
            var user = await GetUserAsync();
            var ahora = DateTime.Now;

            var debidas = await _context.NOTIFICACION
                .Where(n => !n.ELIMINADO
                         && (n.USUARIO_DESTINO == null || n.USUARIO_DESTINO == user)
                         && (n.ESTADO == "PENDIENTE" || n.ESTADO == "PROGRAMADA")
                         && (n.FECHA_PROGRAMADA == null || n.FECHA_PROGRAMADA <= ahora)
                         && (n.SNOOZE_HASTA == null || n.SNOOZE_HASTA <= ahora))
                .OrderBy(n => n.FECHA_PROGRAMADA ?? n.FECHA_CREACION)
                .Take(5)
                .ToListAsync(ct);

            foreach (var n in debidas)
            {
                n.ESTADO = "ENVIADA";
                n.FECHA_ENVIO = ahora;
                n.MODIFICADO_POR = user;
                n.FECHA_MODIFICACION = ahora;
            }
            if (debidas.Count > 0)
                await _context.SaveChangesAsync(ct);

            var dto = debidas.Select(n => new NotificacionToastDTO
            {
                Id = n.NOTIFICACION_ID,
                Titulo = string.IsNullOrWhiteSpace(n.TITULO) ? n.TIPO.Replace("_", " ") : n.TITULO,
                Mensaje = n.MENSAJE,
                Tipo = n.TIPO,
                Nivel = (byte?)n.NIVEL,
                Url = n.URL
            }).ToList();

            return Json(dto);
        }

        // POST: /Notificaciones/Read/123
        //[HttpPost]
        //public async Task<IActionResult> Read(int id, CancellationToken ct)
        //{
        //    var user = await GetUserAsync();
        //    var n = await _context.NOTIFICACION.FirstOrDefaultAsync(x => x.NOTIFICACION_ID == id && !x.ELIMINADO, ct);
        //    if (n is null) return NotFound();

        //    if (n.ESTADO != "LEIDA")
        //    {
        //        n.ESTADO = "LEIDA";
        //        n.FECHA_LECTURA = DateTime.Now;
        //        n.MODIFICADO_POR = user;
        //        n.FECHA_MODIFICACION = DateTime.Now;
        //        await _context.SaveChangesAsync(ct);
        //    }
        //    return Ok();
        //}
        [HttpPost]
        public async Task<IActionResult> Read(int id, CancellationToken ct)
        {
            var user = await GetUserAsync();
            var n = await _context.NOTIFICACION.FirstOrDefaultAsync(x => x.NOTIFICACION_ID == id && !x.ELIMINADO, ct);
            if (n is null) return NotFound();

            var ahora = DateTime.Now;
            n.FECHA_LECTURA = ahora;
            n.SNOOZE_HASTA = ahora.AddHours(12);   // ← re-aparece en 12h
            n.MODIFICADO_POR = user;
            n.FECHA_MODIFICACION = ahora;
            // n.ESTADO se queda "ACTIVO"
            await _context.SaveChangesAsync(ct);
            return Ok();
        }




        // POST: /Notificaciones/ReadAll
        //[HttpPost]
        //public async Task<IActionResult> ReadAll(CancellationToken ct)
        //{
        //    var user = await GetUserAsync();
        //    var ahora = DateTime.Now;

        //    var q = await _context.NOTIFICACION
        //        .Where(n => !n.ELIMINADO
        //                 && (n.USUARIO_DESTINO == null || n.USUARIO_DESTINO == user)
        //                 && n.ESTADO != "LEIDA" && n.ESTADO != "ARCHIVADA")
        //        .ToListAsync(ct);

        //    foreach (var n in q)
        //    {
        //        n.ESTADO = "LEIDA";
        //        n.FECHA_LECTURA = ahora;
        //        n.MODIFICADO_POR = user;
        //        n.FECHA_MODIFICACION = ahora;
        //    }
        //    if (q.Count > 0) await _context.SaveChangesAsync(ct);
        //    return Ok();
        //}

        [HttpPost]
        public async Task<IActionResult> ReadAll(CancellationToken ct)
        {
            var user = await GetUserAsync();
            var ahora = DateTime.Now;

            var q = await _context.NOTIFICACION
                .Where(n => !n.ELIMINADO
                         && n.ESTADO == "ACTIVO"
                         && (n.RESUELTA == false || n.RESUELTA == null))
                .ToListAsync(ct);

            foreach (var n in q)
            {
                n.FECHA_LECTURA = ahora;
                n.SNOOZE_HASTA = ahora.AddHours(12);
                n.MODIFICADO_POR = user;
                n.FECHA_MODIFICACION = ahora;
            }
            if (q.Count > 0) await _context.SaveChangesAsync(ct);
            return Ok();
        }



        // POST: /Notificaciones/Snooze/123?mins=60
        //[HttpPost]
        //public async Task<IActionResult> Snooze(int id, int mins = 60, CancellationToken ct = default)
        //{
        //    if (mins < 1) mins = 60;
        //    var user = await GetUserAsync();
        //    var n = await _context.NOTIFICACION.FirstOrDefaultAsync(x => x.NOTIFICACION_ID == id && !x.ELIMINADO, ct);
        //    if (n is null) return NotFound();

        //    n.SNOOZE_HASTA = DateTime.Now.AddMinutes(mins);
        //    n.ESTADO = "PROGRAMADA";
        //    n.MODIFICADO_POR = user;
        //    n.FECHA_MODIFICACION = DateTime.Now;
        //    await _context.SaveChangesAsync(ct);

        //    return Ok();
        //}



        [HttpPost]
        public async Task<IActionResult> Snooze(int id, int mins = 60, CancellationToken ct = default)
        {
            if (mins < 1) mins = 60;
            var user = await GetUserAsync();
            var n = await _context.NOTIFICACION.FirstOrDefaultAsync(x => x.NOTIFICACION_ID == id && !x.ELIMINADO, ct);
            if (n is null) return NotFound();

            n.SNOOZE_HASTA = DateTime.Now.AddMinutes(mins);
            // n.ESTADO se queda ACTIVO (el Count/Dropdown lo ocultarán hasta que venza el snooze)
            n.MODIFICADO_POR = user;
            n.FECHA_MODIFICACION = DateTime.Now;
            await _context.SaveChangesAsync(ct);

            return Ok();
        }







        // POST: /Notificaciones/Archive/123
        [HttpPost]
        public async Task<IActionResult> Archive(int id, CancellationToken ct)
        {
            var user = await GetUserAsync();
            var n = await _context.NOTIFICACION.FirstOrDefaultAsync(x => x.NOTIFICACION_ID == id && !x.ELIMINADO, ct);
            if (n is null) return NotFound();

            n.ESTADO = "ARCHIVADA";
            n.MODIFICADO_POR = user;
            n.FECHA_MODIFICACION = DateTime.Now;
            await _context.SaveChangesAsync(ct);

            return Ok();
        }
    }
}
