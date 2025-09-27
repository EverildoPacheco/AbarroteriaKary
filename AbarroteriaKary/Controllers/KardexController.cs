using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.ModelsPartial.Paginacion;           // PaginadoViewModel<T>
using AbarroteriaKary.Services.Auditoria;
using AbarroteriaKary.Services.Correlativos;
using AbarroteriaKary.Services.Extensions;                // ToPagedAsync extension
using AbarroteriaKary.Services.Reportes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;
using System;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AbarroteriaKary.Controllers
{
    public class KardexController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;
        private readonly IAuditoriaService _auditoria;
        private readonly IReporteExportService _exportSvc;

        public KardexController(KaryDbContext context, ICorrelativoService correlativos, IAuditoriaService auditoria, IReporteExportService exportSvc)
        {
               _context = context;
            _correlativos = correlativos;
            _auditoria = auditoria;
            _exportSvc = exportSvc;
        }



        // GET: Kardex
        [HttpGet]
        public async Task<IActionResult> Index(
         string? tipo = "TODOS",           // TODOS | ENTRADA | SALIDA | AJUSTE
         string? estado = "ACTIVO",        // ACTIVO | INACTIVO | TODOS
         string? productoId = null,        // filtra por producto puntual
         string? q = null,                 // búsqueda libre
         string? fDesde = null,            // dd/MM/yyyy o yyyy-MM-dd
         string? fHasta = null,            // dd/MM/yyyy o yyyy-MM-dd
         int page = 1, int pageSize = 25,
         CancellationToken ct = default)
        {
            // 0) Normalización de filtros
            var tipoNorm = (tipo ?? "TODOS").Trim().ToUpperInvariant();
            if (tipoNorm is not ("TODOS" or "ENTRADA" or "SALIDA" or "AJUSTE"))
                tipoNorm = "TODOS";

            var estadoNorm = (estado ?? "ACTIVO").Trim().ToUpperInvariant();
            if (estadoNorm is not ("ACTIVO" or "INACTIVO" or "TODOS"))
                estadoNorm = "ACTIVO";

            // 1) Rango de fechas (por FECHA del kardex)
            DateTime? desde = ParseDate(fDesde);
            DateTime? hasta = ParseDate(fHasta);

            // 2) Base query con JOIN a PRODUCTO, ignorando eliminados
            var qry = from k in _context.KARDEX.AsNoTracking()
                      join p in _context.PRODUCTO.AsNoTracking()
                          on k.PRODUCTO_ID equals p.PRODUCTO_ID
                      where !k.ELIMINADO && !p.ELIMINADO
                      select new { k, p };

            // 3) Filtros por tipo y estado
            if (tipoNorm != "TODOS")
                qry = qry.Where(x => x.k.TIPO_MOVIMIENTO == tipoNorm);

            if (estadoNorm is "ACTIVO" or "INACTIVO")
                qry = qry.Where(x => (x.k.ESTADO ?? "ACTIVO").ToUpper() == estadoNorm);

            // 4) Filtro por producto puntual (si viene)
            if (!string.IsNullOrWhiteSpace(productoId))
            {
                var pid = productoId.Trim().ToUpperInvariant();
                qry = qry.Where(x => x.p.PRODUCTO_ID == pid);
            }

            // 5) Rango de fechas (incluyente)
            if (desde.HasValue) qry = qry.Where(x => x.k.FECHA >= desde.Value.Date);
            if (hasta.HasValue) qry = qry.Where(x => x.k.FECHA < hasta.Value.Date.AddDays(1));

            // 6) Búsqueda libre: por id kardex, producto (id/código/nombre), lote, referencia y motivo
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                qry = qry.Where(x =>
                    EF.Functions.Like(x.k.KARDEX_ID, term) ||
                    EF.Functions.Like(x.p.PRODUCTO_ID, term) ||
                    (x.p.PRODUCTO_CODIGO != null && EF.Functions.Like(x.p.PRODUCTO_CODIGO, term)) ||
                    (x.p.PRODUCTO_NOMBRE != null && EF.Functions.Like(x.p.PRODUCTO_NOMBRE, term)) ||
                    (x.k.LOTE_CODIGO != null && EF.Functions.Like(x.k.LOTE_CODIGO, term)) ||
                    (x.k.REFERENCIA != null && EF.Functions.Like(x.k.REFERENCIA, term)) ||
                    (x.k.MOTIVO != null && EF.Functions.Like(x.k.MOTIVO, term))
                );
            }

            // 7) Orden y proyección a VM (ANTES de paginar)
            var proyectado = qry
                .OrderByDescending(x => x.k.FECHA)
                .ThenByDescending(x => x.k.KARDEX_ID)
                .Select(x => new KardexViewModel
                {
                    KardexId = x.k.KARDEX_ID,
                    Fecha = x.k.FECHA,
                    ProductoId = x.p.PRODUCTO_ID,
                    CodigoProducto = x.p.PRODUCTO_CODIGO,
                    NombreProducto = x.p.PRODUCTO_NOMBRE,
                    TipoMovimiento = x.k.TIPO_MOVIMIENTO,
                    Cantidad = x.k.CANTIDAD,

                    CostoUnitario = x.k.COSTO_UNITARIO ?? 0m,

                    LoteCodigo = x.k.LOTE_CODIGO,
                    Referencia = x.k.REFERENCIA,
                    Motivo = x.k.MOTIVO,
                    Estado = x.k.ESTADO
                });

            // 8) Paginación
            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 25;

            var resultado = await proyectado.ToPagedAsync(page, pageSize, ct);

            // 9) RouteValues para el pager
            resultado.RouteValues["tipo"] = tipoNorm;
            resultado.RouteValues["estado"] = estadoNorm;
            resultado.RouteValues["productoId"] = productoId;
            resultado.RouteValues["q"] = q;
            resultado.RouteValues["fDesde"] = desde?.ToString("yyyy-MM-dd");
            resultado.RouteValues["fHasta"] = hasta?.ToString("yyyy-MM-dd");

            // 10) Toolbar (persistencia de filtros en la vista)
            ViewBag.Tipo = tipoNorm;
            ViewBag.Estado = estadoNorm;
            ViewBag.ProductoId = productoId;
            ViewBag.Q = q;
            ViewBag.FDesde = resultado.RouteValues["fDesde"];
            ViewBag.FHasta = resultado.RouteValues["fHasta"];

            return View(resultado);
        }

        // === Utilidad local para parsear fechas ===
        private static DateTime? ParseDate(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            var formats = new[] { "dd/MM/yyyy", "yyyy-MM-dd" };
            if (DateTime.TryParseExact(input, formats,
                CultureInfo.GetCultureInfo("es-GT"),
                DateTimeStyles.None, out var d))
                return d;

            if (DateTime.TryParse(input, out d)) return d;
            return null;
        }





































        // GET: Kardex/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var kARDEX = await _context.KARDEX
                .Include(k => k.PRODUCTO)
                .FirstOrDefaultAsync(m => m.KARDEX_ID == id);
            if (kARDEX == null)
            {
                return NotFound();
            }

            return View(kARDEX);
        }

        // GET: Kardex/Create
        public IActionResult Create()
        {
            ViewData["PRODUCTO_ID"] = new SelectList(_context.PRODUCTO, "PRODUCTO_ID", "PRODUCTO_ID");
            return View();
        }

        // POST: Kardex/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("KARDEX_ID,PRODUCTO_ID,FECHA,TIPO_MOVIMIENTO,CANTIDAD,COSTO_UNITARIO,REFERENCIA,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO,LOTE_CODIGO,MOTIVO")] KARDEX kARDEX)
        {
            if (ModelState.IsValid)
            {
                _context.Add(kARDEX);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["PRODUCTO_ID"] = new SelectList(_context.PRODUCTO, "PRODUCTO_ID", "PRODUCTO_ID", kARDEX.PRODUCTO_ID);
            return View(kARDEX);
        }

        // GET: Kardex/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var kARDEX = await _context.KARDEX.FindAsync(id);
            if (kARDEX == null)
            {
                return NotFound();
            }
            ViewData["PRODUCTO_ID"] = new SelectList(_context.PRODUCTO, "PRODUCTO_ID", "PRODUCTO_ID", kARDEX.PRODUCTO_ID);
            return View(kARDEX);
        }

        // POST: Kardex/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("KARDEX_ID,PRODUCTO_ID,FECHA,TIPO_MOVIMIENTO,CANTIDAD,COSTO_UNITARIO,REFERENCIA,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO,LOTE_CODIGO,MOTIVO")] KARDEX kARDEX)
        {
            if (id != kARDEX.KARDEX_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(kARDEX);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!KARDEXExists(kARDEX.KARDEX_ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["PRODUCTO_ID"] = new SelectList(_context.PRODUCTO, "PRODUCTO_ID", "PRODUCTO_ID", kARDEX.PRODUCTO_ID);
            return View(kARDEX);
        }

        // GET: Kardex/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var kARDEX = await _context.KARDEX
                .Include(k => k.PRODUCTO)
                .FirstOrDefaultAsync(m => m.KARDEX_ID == id);
            if (kARDEX == null)
            {
                return NotFound();
            }

            return View(kARDEX);
        }

        // POST: Kardex/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var kARDEX = await _context.KARDEX.FindAsync(id);
            if (kARDEX != null)
            {
                _context.KARDEX.Remove(kARDEX);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool KARDEXExists(string id)
        {
            return _context.KARDEX.Any(e => e.KARDEX_ID == id);
        }
    }
}
