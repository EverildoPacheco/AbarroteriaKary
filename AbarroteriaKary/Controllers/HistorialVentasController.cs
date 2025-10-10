//using AbarroteriaKary.Data;
//using AbarroteriaKary.ModelsPartial;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using System;
//using System.Globalization;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;

//namespace AbarroteriaKary.Controllers
//{
//    public class HistorialVentasController : Controller
//    {
//        private readonly KaryDbContext _db;
//        public HistorialVentasController(KaryDbContext db) => _db = db;

//        // GET: /HistorialVentas
//        // Lista sesiones CERRADAS (historial).
//        [HttpGet]
//        public async Task<IActionResult> Index(CancellationToken ct = default)
//        {
//            var sesiones = await _db.CAJA_SESION.AsNoTracking()
//                .Where(s => !s.ELIMINADO && s.ESTADO_SESION == "CERRADA")
//                .OrderByDescending(s => s.FECHA_CIERRE ?? s.FECHA_APERTURA)
//                .Select(s => new HistorialSesionItemVM
//                {
//                    SesionId = s.SESION_ID,
//                    CajaNombre = _db.CAJA.Where(c => c.CAJA_ID == s.CAJA_ID).Select(c => c.CAJA_NOMBRE).FirstOrDefault(),
//                    FechaApertura = s.FECHA_APERTURA,
//                    FechaCierre = s.FECHA_CIERRE,
//                    UsuarioCierreNombre = _db.USUARIO
//                        .Where(u => u.USUARIO_ID == s.USUARIO_CIERRE_ID && !u.ELIMINADO)
//                        .Select(u => u.USUARIO_NOMBRE)
//                        .FirstOrDefault(),
//                    TotalVentas = _db.VENTA
//                        .Where(v => !v.ELIMINADO && v.SESION_ID == s.SESION_ID)
//                        .Sum(v => (decimal?)v.TOTAL) ?? 0m
//                })
//                .ToListAsync(ct);

//            return View("~/Views/HistorialVentas/Index.cshtml", sesiones);
//        }







//        // GET: /HistorialVentas/Sesion/SES0000001
//        // Ventas pertenecientes a la sesión indicada.
//        [HttpGet]
//        public async Task<IActionResult> Sesion(string id, CancellationToken ct = default)
//        {
//            if (string.IsNullOrWhiteSpace(id)) return NotFound();

//            // Header rápido con info de la sesión
//            var header = await _db.CAJA_SESION.AsNoTracking()
//                .Where(s => s.SESION_ID == id && !s.ELIMINADO)
//                .Select(s => new
//                {
//                    s.SESION_ID,
//                    s.FECHA_APERTURA,
//                    s.FECHA_CIERRE,
//                    CajaNombre = _db.CAJA.Where(c => c.CAJA_ID == s.CAJA_ID).Select(c => c.CAJA_NOMBRE).FirstOrDefault()
//                })
//                .FirstOrDefaultAsync(ct);

//            if (header == null) return NotFound();

//            ViewBag.SesionId = header.SESION_ID;
//            ViewBag.CajaNombre = header.CajaNombre;
//            ViewBag.FechaApertura = header.FECHA_APERTURA;
//            ViewBag.FechaCierre = header.FECHA_CIERRE;

//            var ventas = await _db.VENTA.AsNoTracking()
//                .Where(v => !v.ELIMINADO && v.SESION_ID == id)
//                .OrderBy(v => v.FECHA)
//                .Select(v => new HistorialVentaItemVM
//                {
//                    VentaId = v.VENTA_ID,
//                    Fecha = v.FECHA,
//                    ClienteId = v.CLIENTE_ID,
//                    UsuarioId = v.USUARIO_ID,
//                    Total = v.TOTAL
//                })
//                .ToListAsync(ct);

//            return View("~/Views/HistorialVentas/Sesion.cshtml", ventas);
//        }

//        // GET: /HistorialVentas/Venta/V000000123
//        // Detalle de la venta (similar al recibo, consolidado por producto).
//        [HttpGet]
//        public async Task<IActionResult> Venta(string id, CancellationToken ct = default)
//        {
//            if (string.IsNullOrWhiteSpace(id)) return NotFound();

//            var v = await _db.VENTA.AsNoTracking()
//                .FirstOrDefaultAsync(x => x.VENTA_ID == id && !x.ELIMINADO, ct);
//            if (v == null) return NotFound();

//            var lineas = await _db.DETALLE_VENTA.AsNoTracking()
//                .Where(d => !d.ELIMINADO && d.VENTA_ID == id)
//                .Join(_db.PRODUCTO.AsNoTracking(),
//                      d => d.PRODUCTO_ID,
//                      p => p.PRODUCTO_ID,
//                      (d, p) => new { d, p })
//                .GroupBy(x => new { x.p.PRODUCTO_ID, x.p.PRODUCTO_NOMBRE, x.d.PRECIO_UNITARIO })
//                .Select(g => new HistorialVentaDetalleLineaVM
//                {
//                    ProductoId = g.Key.PRODUCTO_ID,
//                    Nombre = g.Key.PRODUCTO_NOMBRE,
//                    Cantidad = g.Sum(z => z.d.CANTIDAD),
//                    PrecioUnitario = g.Key.PRECIO_UNITARIO
//                })
//                .OrderBy(l => l.Nombre)
//                .ToListAsync(ct);

//            var vm = new HistorialVentaDetalleVM
//            {
//                VentaId = v.VENTA_ID,
//                Fecha = v.FECHA,
//                ClienteId = v.CLIENTE_ID,
//                UsuarioId = v.USUARIO_ID,
//                Total = v.TOTAL,
//                Lineas = lineas
//            };

//            return View("~/Views/HistorialVentas/Venta.cshtml", vm);
//        }
//    }
//}

using AbarroteriaKary.Data;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.ModelsPartial.Paginacion; // si tu ToPagedAsync vive aquí
using AbarroteriaKary.Services.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AbarroteriaKary.Controllers
{
    public class HistorialVentasController : Controller
    {
        private readonly KaryDbContext _context;
        public HistorialVentasController(KaryDbContext context) => _context = context;

        // ===========================
        // NIVEL 1: Sesiones cerradas
        // ===========================
        [HttpGet]
        public async Task<IActionResult> Index(
            string? q = null,
            string? fDesde = null,
            string? fHasta = null,
            int page = 1, int pageSize = 25,
            CancellationToken ct = default)
        {
            // 1) Rango de fechas (cerradas → usamos FECHA_CIERRE)
            DateTime? desde = ParseDate(fDesde);
            DateTime? hasta = ParseDate(fHasta);

            var baseQry = _context.CAJA_SESION.AsNoTracking()
                .Where(s => !s.ELIMINADO && s.ESTADO_SESION == "CERRADA");

            if (desde.HasValue) baseQry = baseQry.Where(s => s.FECHA_CIERRE >= desde.Value.Date);
            if (hasta.HasValue) baseQry = baseQry.Where(s => s.FECHA_CIERRE < hasta.Value.Date.AddDays(1));

            // 2) Búsqueda por Sesión/Caja/Usuario cierre (id o nombre)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                baseQry = baseQry.Where(s =>
                    EF.Functions.Like(s.SESION_ID, term) ||
                    _context.CAJA.Any(c => c.CAJA_ID == s.CAJA_ID && !c.ELIMINADO && EF.Functions.Like(c.CAJA_NOMBRE, term)) ||
                    _context.USUARIO.Any(u => !u.ELIMINADO
                        && u.USUARIO_ID == s.USUARIO_CIERRE_ID
                        && (EF.Functions.Like(u.USUARIO_ID, term) || EF.Functions.Like(u.USUARIO_NOMBRE, term)))
                );
            }

            // 3) Orden + proyección
            var proyectado = baseQry
                .OrderByDescending(s => s.FECHA_CIERRE ?? s.FECHA_APERTURA)
                .Select(s => new HistorialSesionItemVM
                {
                    SesionId = s.SESION_ID,
                    CajaNombre = _context.CAJA.Where(c => c.CAJA_ID == s.CAJA_ID).Select(c => c.CAJA_NOMBRE).FirstOrDefault(),
                    FechaApertura = s.FECHA_APERTURA,
                    FechaCierre = s.FECHA_CIERRE,
                    UsuarioCierreNombre = _context.USUARIO
                        .Where(u => u.USUARIO_ID == s.USUARIO_CIERRE_ID && !u.ELIMINADO)
                        .Select(u => u.USUARIO_NOMBRE)
                        .FirstOrDefault(),
                    TotalVentas = _context.VENTA
                        .Where(v => !v.ELIMINADO && v.SESION_ID == s.SESION_ID)
                        .Sum(v => (decimal?)v.TOTAL) ?? 0m
                });

            // 4) Paginación
            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 25;

            var resultado = await proyectado.ToPagedAsync(page, pageSize, ct);

            // 5) RouteValues para el pager
            resultado.RouteValues["q"] = q;
            resultado.RouteValues["fDesde"] = desde?.ToString("yyyy-MM-dd");
            resultado.RouteValues["fHasta"] = hasta?.ToString("yyyy-MM-dd");

            // 6) Toolbar
            ViewBag.Q = q;
            ViewBag.FDesde = resultado.RouteValues["fDesde"];
            ViewBag.FHasta = resultado.RouteValues["fHasta"];

            return View("~/Views/HistorialVentas/Index.cshtml", resultado);
        }

        // =======================================
        // NIVEL 2: Ventas por sesión seleccionada
        // =======================================
        [HttpGet]
        public async Task<IActionResult> Sesion(
    string id,
    string? q = null,
    string? fDesde = null,
    string? fHasta = null,
    int page = 1, int pageSize = 25,
    CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // Header
            var header = await _context.CAJA_SESION.AsNoTracking()
                .Where(s => s.SESION_ID == id && !s.ELIMINADO)
                .Select(s => new
                {
                    s.SESION_ID,
                    s.FECHA_APERTURA,
                    s.FECHA_CIERRE,
                    CajaNombre = _context.CAJA.Where(c => c.CAJA_ID == s.CAJA_ID)
                                              .Select(c => c.CAJA_NOMBRE).FirstOrDefault()
                })
                .FirstOrDefaultAsync(ct);
            if (header == null) return NotFound();

            ViewBag.SesionId = header.SESION_ID;
            ViewBag.CajaNombre = header.CajaNombre;
            ViewBag.FechaApertura = header.FECHA_APERTURA;
            ViewBag.FechaCierre = header.FECHA_CIERRE;

            // Filtros
            DateTime? desde = ParseDate(fDesde);
            DateTime? hasta = ParseDate(fHasta);

            // JOIN explícito para poder mostrar y filtrar por nombres
            var baseQry =
                from v in _context.VENTA.AsNoTracking()
                join p0 in _context.PERSONA.AsNoTracking().Where(pp => !pp.ELIMINADO)
                    on v.CLIENTE_ID equals p0.PERSONA_ID into JP
                from p in JP.DefaultIfEmpty()
                join u0 in _context.USUARIO.AsNoTracking().Where(uu => !uu.ELIMINADO)
                    on v.USUARIO_ID equals u0.USUARIO_ID into JU
                from u in JU.DefaultIfEmpty()
                where !v.ELIMINADO && v.SESION_ID == id
                select new
                {
                    v,
                    ClienteNombre = (
                        (p.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (p.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (p.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (p.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (p.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                        (p.PERSONA_APELLIDOCASADA ?? "")
                    ).Trim(),
                    UsuarioNombre = u.USUARIO_NOMBRE
                };

            // Búsqueda: por venta, cliente/vendedor id o nombres
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                baseQry = baseQry.Where(x =>
                    EF.Functions.Like(x.v.VENTA_ID, term) ||
                    EF.Functions.Like(x.v.CLIENTE_ID ?? "", term) ||
                    EF.Functions.Like(x.v.USUARIO_ID ?? "", term) ||
                    EF.Functions.Like(x.ClienteNombre ?? "", term) ||
                    EF.Functions.Like(x.UsuarioNombre ?? "", term)
                );
            }

            // Rango por fecha de la venta
            if (desde.HasValue) baseQry = baseQry.Where(x => x.v.FECHA >= desde.Value.Date);
            if (hasta.HasValue) baseQry = baseQry.Where(x => x.v.FECHA < hasta.Value.Date.AddDays(1));

            var proyectado = baseQry
                .OrderBy(x => x.v.FECHA)
                .Select(x => new HistorialVentaItemVM
                {
                    VentaId = x.v.VENTA_ID,
                    Fecha = x.v.FECHA,
                    ClienteId = x.v.CLIENTE_ID,
                    ClienteNombre = x.ClienteNombre,     // 👈 nombre
                    UsuarioId = x.v.USUARIO_ID,
                    UsuarioNombre = x.UsuarioNombre,     // 👈 nombre
                    Total = x.v.TOTAL
                });

            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 25;

            var resultado = await proyectado.ToPagedAsync(page, pageSize, ct);

            resultado.RouteValues["id"] = id;
            resultado.RouteValues["q"] = q;
            resultado.RouteValues["fDesde"] = desde?.ToString("yyyy-MM-dd");
            resultado.RouteValues["fHasta"] = hasta?.ToString("yyyy-MM-dd");

            ViewBag.Q = q;
            ViewBag.FDesde = resultado.RouteValues["fDesde"];
            ViewBag.FHasta = resultado.RouteValues["fHasta"];

            return View("~/Views/HistorialVentas/Sesion.cshtml", resultado);
        }


        // =======================
        // NIVEL 3: Detalle venta
        // =======================
        [HttpGet]
        public async Task<IActionResult> Venta(string id, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // 1) Venta
            var v = await _context.VENTA.AsNoTracking()
                .FirstOrDefaultAsync(x => x.VENTA_ID == id && !x.ELIMINADO, ct);
            if (v == null) return NotFound();

            // 2) Nombre del cliente (PERSONA). Si no existe, fallback a "Consumidor Final" o al ID.
            string? clienteNombre = null;
            if (!string.IsNullOrWhiteSpace(v.CLIENTE_ID))
            {
                var cli = await _context.PERSONA.AsNoTracking()
                    .Where(p => p.PERSONA_ID == v.CLIENTE_ID && !p.ELIMINADO)
                    .Select(p => (p.PERSONA_PRIMERNOMBRE + " "
                                + (p.PERSONA_SEGUNDONOMBRE ?? "") + " "
                                + (p.PERSONA_TERCERNOMBRE ?? "") + " "
                                + p.PERSONA_PRIMERAPELLIDO + " "
                                + (p.PERSONA_SEGUNDOAPELLIDO ?? "") + " "
                                + (p.PERSONA_APELLIDOCASADA ?? "")).Trim())
                    .FirstOrDefaultAsync(ct);

                clienteNombre = string.IsNullOrWhiteSpace(cli)
                    ? (string.Equals(v.CLIENTE_ID, "CF", StringComparison.OrdinalIgnoreCase) ? "Consumidor Final" : v.CLIENTE_ID)
                    : cli;
            }

            // 3) Nombre del vendedor (USUARIO). Si no existe, fallback al ID.
            var usuarioNombre = await _context.USUARIO.AsNoTracking()
                .Where(u => u.USUARIO_ID == v.USUARIO_ID && !u.ELIMINADO)
                .Select(u => u.USUARIO_NOMBRE)
                .FirstOrDefaultAsync(ct);
            usuarioNombre ??= v.USUARIO_ID;

            // 4) Líneas (consolidadas por producto y precio)
            var lineas = await _context.DETALLE_VENTA.AsNoTracking()
                .Where(d => !d.ELIMINADO && d.VENTA_ID == id)
                .Join(_context.PRODUCTO.AsNoTracking(),
                      d => d.PRODUCTO_ID,
                      p => p.PRODUCTO_ID,
                      (d, p) => new { d, p })
                .GroupBy(x => new { x.p.PRODUCTO_ID, x.p.PRODUCTO_NOMBRE, x.d.PRECIO_UNITARIO })
                .Select(g => new HistorialVentaDetalleLineaVM
                {
                    ProductoId = g.Key.PRODUCTO_ID,
                    Nombre = g.Key.PRODUCTO_NOMBRE,
                    Cantidad = g.Sum(z => z.d.CANTIDAD),
                    PrecioUnitario = g.Key.PRECIO_UNITARIO
                })
                .OrderBy(l => l.Nombre)
                .ToListAsync(ct);

            // 5) VM
            var vm = new HistorialVentaDetalleVM
            {
                VentaId = v.VENTA_ID,
                Fecha = v.FECHA,

                ClienteId = v.CLIENTE_ID,
                ClienteNombre = clienteNombre,

                UsuarioId = v.USUARIO_ID,
                UsuarioNombre = usuarioNombre,

                // Si agregaste SESION_ID a VENTA y lo mapeaste en el VM, pásalo:
                SesionId = v.SESION_ID,   // ← quítalo si tu VM no lo tiene

                Total = v.TOTAL,
                Lineas = lineas
            };

            return View("~/Views/HistorialVentas/Venta.cshtml", vm);
        }






        // Utilidad: fechas dd/MM/yyyy o yyyy-MM-dd
        private static DateTime? ParseDate(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var formats = new[] { "dd/MM/yyyy", "yyyy-MM-dd" };
            if (DateTime.TryParseExact(input, formats,
                CultureInfo.GetCultureInfo("es-GT"),
                DateTimeStyles.None, out var d)) return d;
            if (DateTime.TryParse(input, out d)) return d;
            return null;
        }
    }
}
