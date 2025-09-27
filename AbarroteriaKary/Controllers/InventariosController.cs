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
using static AbarroteriaKary.ModelsPartial.InventarioViewModel;

namespace AbarroteriaKary.Controllers
{
    public class InventariosController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;
        private readonly IAuditoriaService _auditoria;
        private readonly IReporteExportService _exportSvc;

        public InventariosController(KaryDbContext context, ICorrelativoService correlativos, IAuditoriaService auditoria, IReporteExportService exportSvc)
        {
            _context = context;
            _correlativos = correlativos;
            _auditoria = auditoria;
            _exportSvc = exportSvc;
        }

        // GET: Inventarios
        // GET: Inventarios
        [HttpGet]
        public async Task<IActionResult> Index(
            string? modo = "DETALLADO",      // DETALLADO | CONSOLIDADO
            string? stock = "TODOS",         // TODOS | BAJO | ALTO
            string? q = null,                // búsqueda por código/nombre/ID/lote
            string? fvDesde = null,          // fecha vencimiento desde (dd/MM/yyyy o yyyy-MM-dd)
            string? fvHasta = null,          // fecha vencimiento hasta (dd/MM/yyyy o yyyy-MM-dd)
            bool soloConStock = false,       // extra: solo mostrar con stock > 0
            int page = 1, int pageSize = 25, // paginación
            CancellationToken ct = default)
        {
            // 0) Normalización de parámetros
            var modoNorm = (modo ?? "DETALLADO").Trim().ToUpperInvariant();
            if (modoNorm is not ("DETALLADO" or "CONSOLIDADO")) modoNorm = "DETALLADO";

            var stockNorm = (stock ?? "TODOS").Trim().ToUpperInvariant();
            if (stockNorm is not ("TODOS" or "BAJO" or "ALTO")) stockNorm = "TODOS";

            // 1) Fechas de vencimiento (DateOnly?)
            DateTime? dtDesde = ParseDate(fvDesde);
            DateTime? dtHasta = ParseDate(fvHasta);
            DateOnly? vDesde = dtDesde.HasValue ? new DateOnly(dtDesde.Value.Year, dtDesde.Value.Month, dtDesde.Value.Day) : (DateOnly?)null;
            DateOnly? vHasta = dtHasta.HasValue ? new DateOnly(dtHasta.Value.Year, dtHasta.Value.Month, dtHasta.Value.Day) : (DateOnly?)null;

            // 2) Base query con JOIN explícito (ignorando eliminados)
            var baseQry = from i in _context.INVENTARIO.AsNoTracking()
                          join p in _context.PRODUCTO.AsNoTracking()
                            on i.PRODUCTO_ID equals p.PRODUCTO_ID
                          where !i.ELIMINADO && !p.ELIMINADO
                          select new { i, p };

            // 3) Búsqueda (PRODUCTO_ID, CÓDIGO, NOMBRE, LOTE)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                baseQry = baseQry.Where(x =>
                    EF.Functions.Like(x.p.PRODUCTO_ID, term) ||
                    (x.p.PRODUCTO_CODIGO != null && EF.Functions.Like(x.p.PRODUCTO_CODIGO, term)) ||
                    (x.p.PRODUCTO_NOMBRE != null && EF.Functions.Like(x.p.PRODUCTO_NOMBRE, term)) ||
                    (x.i.LOTE_CODIGO != null && EF.Functions.Like(x.i.LOTE_CODIGO, term))
                );
            }

            // 4) Rango por FECHA_VENCIMIENTO (DateOnly?)
            if (vDesde.HasValue) baseQry = baseQry.Where(x => x.i.FECHA_VENCIMIENTO >= vDesde.Value);
            if (vHasta.HasValue) baseQry = baseQry.Where(x => x.i.FECHA_VENCIMIENTO <= vHasta.Value);

            // 5) Solo con stock > 0 (opcional)
            if (soloConStock) baseQry = baseQry.Where(x => x.i.STOCK_ACTUAL > 0);

            // 6) Filtros por nivel de stock (umbral = STOCK_MINIMO si > 0; si no, 10)
            //    - En detallado se aplica por línea.
            //    - En consolidado se aplica por producto (sumas).
            if (modoNorm == "DETALLADO")
            {
                if (stockNorm == "BAJO")
                {
                    baseQry = baseQry.Where(x =>
                        x.i.STOCK_ACTUAL <= (x.i.STOCK_MINIMO > 0 ? x.i.STOCK_MINIMO : 10));
                }
                else if (stockNorm == "ALTO")
                {
                    baseQry = baseQry.Where(x =>
                        x.i.STOCK_ACTUAL > (x.i.STOCK_MINIMO > 0 ? x.i.STOCK_MINIMO : 10));
                }

                // 7) Orden + proyección a VM (detallado)
                var proyectado = baseQry
                    .OrderBy(x => x.p.PRODUCTO_NOMBRE)
                    .ThenBy(x => x.i.FECHA_VENCIMIENTO)
                    .ThenBy(x => x.i.LOTE_CODIGO)
                    .Select(x => new InventarioIndexItemVM
                    {
                        InventarioId = x.i.INVENTARIO_ID,
                        ProductoId = x.p.PRODUCTO_ID,
                        CodigoProducto = x.p.PRODUCTO_CODIGO,
                        NombreProducto = x.p.PRODUCTO_NOMBRE,
                        LoteCodigo = x.i.LOTE_CODIGO,
                        FechaVencimiento = x.i.FECHA_VENCIMIENTO.HasValue
                            ? new DateTime(x.i.FECHA_VENCIMIENTO.Value.Year, x.i.FECHA_VENCIMIENTO.Value.Month, x.i.FECHA_VENCIMIENTO.Value.Day)
                            : (DateTime?)null,
                        StockActual = x.i.STOCK_ACTUAL,
                        StockMinimo = x.i.STOCK_MINIMO,
                        CostoUnitario = x.i.COSTO_UNITARIO,
                        Activo = (x.i.ESTADO ?? "ACTIVO").ToUpper() == "ACTIVO"
                    });

                // 8) Paginación
                var permitidos = new[] { 10, 25, 50, 100 };
                pageSize = permitidos.Contains(pageSize) ? pageSize : 25;

                var resultado = await proyectado.ToPagedAsync(page, pageSize, ct);

                // 9) RouteValues
                resultado.RouteValues["modo"] = modoNorm;
                resultado.RouteValues["stock"] = stockNorm;
                resultado.RouteValues["q"] = q;
                resultado.RouteValues["fvDesde"] = dtDesde?.ToString("yyyy-MM-dd");
                resultado.RouteValues["fvHasta"] = dtHasta?.ToString("yyyy-MM-dd");
                resultado.RouteValues["soloConStock"] = soloConStock;

                // Toolbar (persistencia de filtros)
                ViewBag.Modo = modoNorm;
                ViewBag.Stock = stockNorm;
                ViewBag.Q = q;
                ViewBag.FVDesde = resultado.RouteValues["fvDesde"];
                ViewBag.FVHasta = resultado.RouteValues["fvHasta"];
                ViewBag.SoloConStock = soloConStock;

                return View("IndexDetallado", resultado); // usa la vista que prefieras (o "Index")
            }
            else
            {
                // === CONSOLIDADO: agrupar por producto y sumar stock ===
                var grp = baseQry
                    .GroupBy(x => new
                    {
                        x.p.PRODUCTO_ID,
                        x.p.PRODUCTO_CODIGO,
                        x.p.PRODUCTO_NOMBRE
                    })
                    .Select(g => new
                    {
                        g.Key.PRODUCTO_ID,
                        g.Key.PRODUCTO_CODIGO,
                        g.Key.PRODUCTO_NOMBRE,
                        StockTotal = g.Sum(z => (int?)z.i.STOCK_ACTUAL) ?? 0,
                        // tomamos el máximo STOCK_MINIMO > 0 como umbral, si todos 0 -> 10
                        MaxStockMinimo = g.Max(z => (int?)z.i.STOCK_MINIMO) ?? 0
                    });

                // Aplicar filtro BAJO/ALTO sobre el sumado
                if (stockNorm == "BAJO")
                {
                    grp = grp.Where(x => x.StockTotal <= (x.MaxStockMinimo > 0 ? x.MaxStockMinimo : 10));
                }
                else if (stockNorm == "ALTO")
                {
                    grp = grp.Where(x => x.StockTotal > (x.MaxStockMinimo > 0 ? x.MaxStockMinimo : 10));
                }

                var proyectado = grp
                    .OrderBy(x => x.PRODUCTO_NOMBRE)
                    .Select(x => new InventarioIndexConsolidadoVM
                    {
                        ProductoId = x.PRODUCTO_ID,
                        CodigoProducto = x.PRODUCTO_CODIGO,
                        NombreProducto = x.PRODUCTO_NOMBRE,
                        StockTotal = x.StockTotal,
                        Umbral = (x.MaxStockMinimo > 0 ? x.MaxStockMinimo : 10),
                        EsBajo = x.StockTotal <= (x.MaxStockMinimo > 0 ? x.MaxStockMinimo : 10)
                    });

                var permitidos = new[] { 10, 25, 50, 100 };
                pageSize = permitidos.Contains(pageSize) ? pageSize : 25;

                var resultado = await proyectado.ToPagedAsync(page, pageSize, ct);

                resultado.RouteValues["modo"] = modoNorm;
                resultado.RouteValues["stock"] = stockNorm;
                resultado.RouteValues["q"] = q;
                resultado.RouteValues["fvDesde"] = dtDesde?.ToString("yyyy-MM-dd");
                resultado.RouteValues["fvHasta"] = dtHasta?.ToString("yyyy-MM-dd");
                resultado.RouteValues["soloConStock"] = soloConStock;

                ViewBag.Modo = modoNorm;
                ViewBag.Stock = stockNorm;
                ViewBag.Q = q;
                ViewBag.FVDesde = resultado.RouteValues["fvDesde"];
                ViewBag.FVHasta = resultado.RouteValues["fvHasta"];
                ViewBag.SoloConStock = soloConStock;

                return View("IndexConsolidado", resultado); // o la vista única "Index"
            }
        }

        // === Utilidad local para parsear fechas (dd/MM/yyyy o yyyy-MM-dd) ===
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










        // GET: Inventarios/Edit/INV0000123
        [HttpGet]
        public async Task<IActionResult> Edit(string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta id.");
            id = id.Trim().ToUpperInvariant();

            var row = await (
                from i in _context.INVENTARIO.AsNoTracking()
                join p in _context.PRODUCTO.AsNoTracking() on i.PRODUCTO_ID equals p.PRODUCTO_ID
                where !i.ELIMINADO && i.INVENTARIO_ID == id && !p.ELIMINADO
                select new { i, p }
            ).FirstOrDefaultAsync(ct);

            if (row is null) return NotFound("Inventario no encontrado.");

            var vm = new InventarioEditVM
            {
                InventarioId = row.i.INVENTARIO_ID,
                ProductoId = row.i.PRODUCTO_ID,
                CodigoProducto = row.p.PRODUCTO_CODIGO,
                NombreProducto = row.p.PRODUCTO_NOMBRE,
                LoteCodigo = row.i.LOTE_CODIGO,
                FechaVencimiento = row.i.FECHA_VENCIMIENTO.HasValue
                    ? new DateTime(row.i.FECHA_VENCIMIENTO.Value.Year, row.i.FECHA_VENCIMIENTO.Value.Month, row.i.FECHA_VENCIMIENTO.Value.Day)
                    : (DateTime?)null,
                StockActual = row.i.STOCK_ACTUAL,
                NuevoStock = row.i.STOCK_ACTUAL, // por defecto igual
                Motivo = ""
            };

            return View(vm);
        }

        // POST: Inventarios/Edit/INV0000123
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, InventarioEditVM vm, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta id.");
            id = id.Trim().ToUpperInvariant();
            if (id != (vm.InventarioId ?? "").Trim().ToUpperInvariant())
                return BadRequest("Inconsistencia de id.");

            // Cargar entidades base
            var inv = await _context.INVENTARIO.FirstOrDefaultAsync(x => !x.ELIMINADO && x.INVENTARIO_ID == id, ct);
            if (inv is null) return NotFound("Inventario no encontrado.");

            var prod = await _context.PRODUCTO.AsNoTracking()
                .FirstOrDefaultAsync(x => x.PRODUCTO_ID == inv.PRODUCTO_ID && !x.ELIMINADO, ct);
            if (prod is null)
            {
                ModelState.AddModelError(string.Empty, "El producto del inventario no existe o está eliminado.");
                HydrateVmFromEntities(inv, prod, vm);
                return View(vm);
            }

            // Validaciones VM
            vm.Motivo = (vm.Motivo ?? "").Trim();
            if (vm.NuevoStock < 0)
                ModelState.AddModelError(nameof(vm.NuevoStock), "El nuevo stock no puede ser negativo.");
            if (vm.Motivo.Length == 0)
                ModelState.AddModelError(nameof(vm.Motivo), "Ingrese el motivo del ajuste.");

            if (!ModelState.IsValid)
            {
                HydrateVmFromEntities(inv, prod, vm);
                return View(vm);
            }

            // Delta
            var nuevo = Math.Max(0, vm.NuevoStock);
            int delta = nuevo - inv.STOCK_ACTUAL;

            if (delta == 0)
            {
                TempData["NoChanges"] = true;
                return RedirectToAction(nameof(Edit), new { id });
            }

            var ahora = DateTime.Now;
            var usuario = await _auditoria.GetUsuarioNombreAsync();

            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            try
            {
                // 1) INVENTARIO
                inv.STOCK_ACTUAL = nuevo;
                inv.MODIFICADO_POR = usuario;
                inv.FECHA_MODIFICACION = ahora;

                // 2) KARDEX (AJUSTE)
                //var kdxId = await _correlativos.NextKardexIdAsync(ct);
                //_context.KARDEX.Add(new KARDEX
                //{
                //    KARDEX_ID = kdxId,
                //    PRODUCTO_ID = inv.PRODUCTO_ID,
                //    FECHA = ahora,
                //    TIPO_MOVIMIENTO = "AJUSTE",
                //    CANTIDAD = Math.Abs(delta),
                //    COSTO_UNITARIO = inv.COSTO_UNITARIO,
                //    REFERENCIA = delta > 0 ? "AJUSTE (+)" : "AJUSTE (-)",
                //    LOTE_CODIGO = inv.LOTE_CODIGO,
                //    MOTIVO = vm.Motivo,
                //    ESTADO = "ACTIVO",
                //    ELIMINADO = false,
                //    CREADO_POR = usuario,
                //    FECHA_CREACION = ahora,
                //    MODIFICADO_POR = usuario,
                //    FECHA_MODIFICACION = ahora
                //});

                var kdxId = await _correlativos.NextKardexIdAsync(ct);
                _context.KARDEX.Add(new KARDEX
                {
                    KARDEX_ID = kdxId,
                    PRODUCTO_ID = inv.PRODUCTO_ID,
                    FECHA = ahora,
                    TIPO_MOVIMIENTO = "AJUSTE",
                    CANTIDAD = delta,        // mantenemos magnitud positiva
                    COSTO_UNITARIO = inv.COSTO_UNITARIO,
                    // REFERENCIA antes: delta > 0 ? "AJUSTE (+)" : "AJUSTE (-)"
                    REFERENCIA = inv.INVENTARIO_ID,      // ← ahora guardamos el ID de la línea de inventario
                    LOTE_CODIGO = inv.LOTE_CODIGO,
                    MOTIVO = vm.Motivo,
                    ESTADO = "ACTIVO",
                    ELIMINADO = false,
                    CREADO_POR = usuario,
                    FECHA_CREACION = ahora,
                    MODIFICADO_POR = usuario,
                    FECHA_MODIFICACION = ahora
                });

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["UpdatedOk"] = true;
                TempData["UpdatedName"] = inv.INVENTARIO_ID;
                return RedirectToAction(nameof(Edit), new { id });
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError(string.Empty, $"Error BD: {ex.GetBaseException().Message}");
                HydrateVmFromEntities(inv, prod, vm);
                return View(vm);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError(string.Empty, ex.GetBaseException().Message);
                HydrateVmFromEntities(inv, prod, vm);
                return View(vm);
            }
        }

        // ---- Helper privado: asegura que la vista siempre tenga datos completos ----
        private static void HydrateVmFromEntities(INVENTARIO inv, PRODUCTO prod, InventarioEditVM vm)
        {
            if (inv == null || prod == null) return;
            vm.InventarioId = inv.INVENTARIO_ID;
            vm.ProductoId = inv.PRODUCTO_ID;
            vm.CodigoProducto = prod.PRODUCTO_CODIGO;
            vm.NombreProducto = prod.PRODUCTO_NOMBRE;
            vm.LoteCodigo = inv.LOTE_CODIGO;
            vm.FechaVencimiento = inv.FECHA_VENCIMIENTO.HasValue
                ? new DateTime(inv.FECHA_VENCIMIENTO.Value.Year, inv.FECHA_VENCIMIENTO.Value.Month, inv.FECHA_VENCIMIENTO.Value.Day)
                : (DateTime?)null;
            vm.StockActual = inv.STOCK_ACTUAL;
        }











































        // GET: Inventarios/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var iNVENTARIO = await _context.INVENTARIO
                .Include(i => i.PRODUCTO)
                .FirstOrDefaultAsync(m => m.INVENTARIO_ID == id);
            if (iNVENTARIO == null)
            {
                return NotFound();
            }

            return View(iNVENTARIO);
        }

        // GET: Inventarios/Create
        public IActionResult Create()
        {
            ViewData["PRODUCTO_ID"] = new SelectList(_context.PRODUCTO, "PRODUCTO_ID", "PRODUCTO_ID");
            return View();
        }

        // POST: Inventarios/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("INVENTARIO_ID,PRODUCTO_ID,STOCK_ACTUAL,STOCK_MINIMO,COSTO_UNITARIO,FECHA_VENCIMIENTO,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO,LOTE_CODIGO,MOTIVO")] INVENTARIO iNVENTARIO)
        {
            if (ModelState.IsValid)
            {
                _context.Add(iNVENTARIO);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["PRODUCTO_ID"] = new SelectList(_context.PRODUCTO, "PRODUCTO_ID", "PRODUCTO_ID", iNVENTARIO.PRODUCTO_ID);
            return View(iNVENTARIO);
        }


















        // GET: Inventarios/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var iNVENTARIO = await _context.INVENTARIO
                .Include(i => i.PRODUCTO)
                .FirstOrDefaultAsync(m => m.INVENTARIO_ID == id);
            if (iNVENTARIO == null)
            {
                return NotFound();
            }

            return View(iNVENTARIO);
        }

        // POST: Inventarios/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var iNVENTARIO = await _context.INVENTARIO.FindAsync(id);
            if (iNVENTARIO != null)
            {
                _context.INVENTARIO.Remove(iNVENTARIO);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool INVENTARIOExists(string id)
        {
            return _context.INVENTARIO.Any(e => e.INVENTARIO_ID == id);
        }
    }
}
