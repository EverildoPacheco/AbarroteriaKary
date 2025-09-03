using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.Services.Correlativos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AbarroteriaKary.ModelsPartial.Paginacion;           // PaginadoViewModel<T>
using AbarroteriaKary.Services.Extensions;                // ToPagedAsync extension


namespace AbarroteriaKary.Controllers
{
    public class AreasController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;


        public AreasController(KaryDbContext context, ICorrelativoService correlativos)
        {
            _context = context;
            _correlativos = correlativos;

        }

        [HttpGet]
        public async Task<IActionResult> Index(
    string? estado, string? q = null,
    string? fDesde = null, string? fHasta = null,
    int page = 1, int pageSize = 10)
        {
            // 0) Estado
            var estadoNorm = (estado ?? "ACTIVO").Trim().ToUpperInvariant();
            if (estadoNorm is not ("ACTIVO" or "INACTIVO" or "TODOS"))
                estadoNorm = "ACTIVO";

            // 1) Fechas (dd/MM/yyyy o yyyy-MM-dd)
            DateTime? desde = ParseDate(fDesde);
            DateTime? hasta = ParseDate(fHasta);

            // 2) Base (ignora eliminados)
            var qry = _context.AREA
                .AsNoTracking()
                .Where(a => !a.ELIMINADO);

            // 3) Estado
            if (estadoNorm is "ACTIVO" or "INACTIVO")
                qry = qry.Where(a => a.ESTADO == estadoNorm);

            // 4) Búsqueda (LIKE, case-insensitive según collation)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                qry = qry.Where(a =>
                    EF.Functions.Like(a.AREA_ID, term) ||
                    EF.Functions.Like(a.AREA_NOMBRE, term));
            }

            // 5) Rango de fechas (inclusivo)
            if (desde.HasValue) qry = qry.Where(a => a.FECHA_CREACION >= desde.Value.Date);
            if (hasta.HasValue) qry = qry.Where(a => a.FECHA_CREACION < hasta.Value.Date.AddDays(1));

            // 6) Orden + proyección (ANTES de paginar)
            var proyectado = qry
                .OrderBy(a => a.AREA_ID) // "AREA000001" se ordena bien como texto
                .Select(a => new AreasViewModel
                {
                    areaId = a.AREA_ID,
                    areaNombre = a.AREA_NOMBRE,
                    areaDescripcion = a.AREA_DESCRIPCION,
                    estadoArea = a.ESTADO,
                    FechaCreacion = a.FECHA_CREACION
                });

            // 7) Paginación (normaliza pageSize)
            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 10;

            var resultado = await proyectado.ToPagedAsync(page, pageSize);

            // 8) RouteValues para el pager
            resultado.RouteValues["estado"] = estadoNorm;
            resultado.RouteValues["q"] = q;
            resultado.RouteValues["fDesde"] = desde?.ToString("yyyy-MM-dd");
            resultado.RouteValues["fHasta"] = hasta?.ToString("yyyy-MM-dd");

            // Toolbar
            ViewBag.Estado = estadoNorm;
            ViewBag.Q = q;
            ViewBag.FDesde = resultado.RouteValues["fDesde"];
            ViewBag.FHasta = resultado.RouteValues["fHasta"];

            return View(resultado);
        }

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




        // GET: Areas/Details/AREA000123
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var a = await _context.AREA
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.AREA_ID == id);

            if (a is null) return NotFound();

            var vm = new AreasViewModel
            {
                areaId = a.AREA_ID,
                areaNombre = a.AREA_NOMBRE,
                areaDescripcion = a.AREA_DESCRIPCION,
                estadoArea = a.ESTADO,                            // para badges en listado
                estadoActivo = string.Equals(a.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),
                FechaCreacion = a.FECHA_CREACION
            };

            return View(vm); // <-- usamos el mismo VM
        }



        // GET: /Areas/Create
        [HttpGet] 
        public async Task<IActionResult> Create()
        {
            var vm = new AreasViewModel
            {
                areaId = await _correlativos.PeekNextAreaIdAsync(), // solo mostrar
                estadoArea = "ACTIVO",
                FechaCreacion = DateTime.Now
            };

            ViewBag.Creado = TempData["Creado"];
            return View(vm); // La vista Create está tipada a AreasViewModel
        }


        // POST: /Areas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AreasViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(vm.areaId))
                    vm.areaId = await _correlativos.PeekNextAreaIdAsync();
                return View(vm);
            }

            var userName = User?.Identity?.Name ?? "Sistema";

            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var nuevoId = await _correlativos.NextAreaIdAsync();

                var entidad = new AREA
                {
                    AREA_ID = nuevoId,
                    AREA_NOMBRE = (vm.areaNombre ?? string.Empty).Trim(),
                    AREA_DESCRIPCION = vm.areaDescripcion?.Trim(),
                    ESTADO = vm.estadoActivo ? "ACTIVO" : "INACTIVO",
                    ELIMINADO = false,
                    CREADO_POR = userName,
                    FECHA_CREACION = DateTime.Now
                };

                _context.Add(entidad);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                // para el modal de éxito
                TempData["SavedOk"] = true;
                TempData["SavedName"] = entidad.AREA_NOMBRE;

                // Volvemos a GET Create (PRG) => allí lanzamos el modal
                return RedirectToAction(nameof(Create));
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError(string.Empty, $"Error BD: {ex.GetBaseException().Message}");
                vm.areaId = await _correlativos.PeekNextAreaIdAsync();
                return View(vm);
            }
        }


        // GET: Areas/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var entidad = await _context.AREA
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AREA_ID == id);

            if (entidad == null) return NotFound();

            var vm = new AreasViewModel
            {
                areaId = entidad.AREA_ID,
                areaNombre = entidad.AREA_NOMBRE,
                areaDescripcion = entidad.AREA_DESCRIPCION,
                estadoArea = entidad.ESTADO,
                estadoActivo = string.Equals(entidad.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),
                FechaCreacion = entidad.FECHA_CREACION
            };

            return View(vm);
        }

        // POST: Areas/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, AreasViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(id) || !string.Equals(id, vm.areaId, StringComparison.Ordinal))
                return NotFound();

            if (!ModelState.IsValid)
                return View(vm);

            var entidad = await _context.AREA.FirstOrDefaultAsync(a => a.AREA_ID == id);
            if (entidad == null) return NotFound();

            // normalización
            var nuevoNombre = (vm.areaNombre ?? string.Empty).Trim();
            var nuevaDesc = vm.areaDescripcion?.Trim();
            var nuevoEstado = vm.estadoActivo ? "ACTIVO" : "INACTIVO";

            // ¿hay cambios?
            var sinCambios =
                string.Equals(entidad.AREA_NOMBRE ?? "", nuevoNombre, StringComparison.Ordinal) &&
                string.Equals(entidad.AREA_DESCRIPCION ?? "", nuevaDesc ?? "", StringComparison.Ordinal) &&
                string.Equals(entidad.ESTADO ?? "", nuevoEstado, StringComparison.Ordinal);

            if (sinCambios)
            {
                TempData["NoChanges"] = true;
                return RedirectToAction(nameof(Edit), new { id });
            }

            // aplicar cambios + auditoría
            entidad.AREA_NOMBRE = nuevoNombre;
            entidad.AREA_DESCRIPCION = nuevaDesc;
            entidad.ESTADO = nuevoEstado;
            entidad.MODIFICADO_POR = User?.Identity?.Name ?? "Sistema";
            entidad.FECHA_MODIFICACION = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync();

                TempData["UpdatedOk"] = true;
                TempData["UpdatedName"] = entidad.AREA_NOMBRE;

                // PRG -> regresamos a GET Edit para lanzar el modal
                return RedirectToAction(nameof(Edit), new { id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.AREA.Any(e => e.AREA_ID == id)) return NotFound();
                throw;
            }
        }












        // GET: Areas/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var aREA = await _context.AREA
                .FirstOrDefaultAsync(m => m.AREA_ID == id);
            if (aREA == null)
            {
                return NotFound();
            }

            return View(aREA);
        }

        // POST: Areas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var aREA = await _context.AREA.FindAsync(id);
            if (aREA != null)
            {
                _context.AREA.Remove(aREA);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AREAExists(string id)
        {
            return _context.AREA.Any(e => e.AREA_ID == id);
        }
    }
}
