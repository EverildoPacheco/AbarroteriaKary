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

        //// GET: Areas
        //public async Task<IActionResult> Index()
        //{
        //    return View(await _context.AREA.ToListAsync());

        //}

        // GET: /Areas
        //[HttpGet]
        //public async Task<IActionResult> Index(string? estado, string? q = null, string? fDesde = null, string? fHasta = null)
        //{
        //    // ---------------------------
        //    // 0) Normalización del estado
        //    // ---------------------------
        //    // Fallback robusto: si viene null, vacío o un valor inválido -> ACTIVO
        //    var estadoNorm = (estado ?? "ACTIVO").Trim().ToUpperInvariant();
        //    if (estadoNorm != "ACTIVO" && estadoNorm != "INACTIVO" && estadoNorm != "TODOS")
        //        estadoNorm = "ACTIVO";

        //    // --------------------------------------------
        //    // 1) Parseo flexible de fechas (dd/MM/yyyy o yyyy-MM-dd)
        //    // --------------------------------------------
        //    DateTime? desde = ParseDate(fDesde);
        //    DateTime? hasta = ParseDate(fHasta);

        //    // --------------------------------------------
        //    // 2) Base query: ignoramos eliminados lógicos
        //    // --------------------------------------------
        //    var qry = _context.AREA
        //        .AsNoTracking()
        //        .Where(a => !a.ELIMINADO);

        //    // --------------------------------------------
        //    // 3) Filtro por estado (ACTIVO/INACTIVO) o TODOS
        //    // --------------------------------------------
        //    if (estadoNorm == "ACTIVO" || estadoNorm == "INACTIVO")
        //        qry = qry.Where(a => a.ESTADO == estadoNorm);
        //    // Si es "TODOS", no se filtra por ESTADO.

        //    // --------------------------------------------
        //    // 4) Búsqueda por ID o Nombre (contains)
        //    // --------------------------------------------
        //    if (!string.IsNullOrWhiteSpace(q))
        //    {
        //        var term = q.Trim();
        //        qry = qry.Where(a =>
        //            a.AREA_ID.Contains(term) ||
        //            a.AREA_NOMBRE.Contains(term));
        //    }

        //    // --------------------------------------------
        //    // 5) Filtro de fechas por FECHA_CREACION (rango inclusivo)
        //    // --------------------------------------------
        //    if (desde.HasValue) qry = qry.Where(a => a.FECHA_CREACION >= desde.Value.Date);
        //    if (hasta.HasValue) qry = qry.Where(a => a.FECHA_CREACION < hasta.Value.Date.AddDays(1));

        //    // --------------------------------------------
        //    // 6) Proyección a ViewModel y orden
        //    // --------------------------------------------
        //    var lista = await qry
        //        .OrderBy(a => a.AREA_ID)
        //        .Select(a => new AreasViewModel
        //        {
        //            areaId = a.AREA_ID,
        //            areaNombre = a.AREA_NOMBRE,
        //            areaDescripcion = a.AREA_DESCRIPCION,
        //            estadoArea = a.ESTADO,
        //            FechaCreacion = a.FECHA_CREACION
        //        })
        //        .ToListAsync();

        //    // --------------------------------------------
        //    // 7) Variables para la Vista (mantener valores en inputs/estilos)
        //    // --------------------------------------------
        //    ViewBag.Estado = estadoNorm;                             // ACTIVO / INACTIVO / TODOS
        //    ViewBag.Q = q;
        //    ViewBag.FDesde = desde?.ToString("yyyy-MM-dd");          // <input type="date"> o hidden
        //    ViewBag.FHasta = hasta?.ToString("yyyy-MM-dd");

        //    return View(lista);
        //}

        //// --------------------------------------------
        //// Utilidad privada para parsear fechas del querystring
        //// --------------------------------------------
        //private static DateTime? ParseDate(string? input)
        //{
        //    if (string.IsNullOrWhiteSpace(input)) return null;

        //    var formats = new[] { "dd/MM/yyyy", "yyyy-MM-dd" };
        //    if (DateTime.TryParseExact(input, formats,
        //            CultureInfo.GetCultureInfo("es-GT"),
        //            DateTimeStyles.None, out var d))
        //        return d;

        //    // Fallback genérico
        //    if (DateTime.TryParse(input, out d)) return d;
        //    return null;
        //}
















        // GET: Areas/Details/5




        [HttpGet]
        public async Task<IActionResult> Index(
    string? estado,
    string? q = null,
    string? fDesde = null,
    string? fHasta = null,
    int page = 1,
    int pageSize = 10)
        {
            // ---------------------------
            // 0) Normalización del estado
            // ---------------------------
            var estadoNorm = (estado ?? "ACTIVO").Trim().ToUpperInvariant();
            if (estadoNorm != "ACTIVO" && estadoNorm != "INACTIVO" && estadoNorm != "TODOS")
                estadoNorm = "ACTIVO";

            // --------------------------------------------
            // 1) Parseo flexible de fechas (dd/MM/yyyy o yyyy-MM-dd)
            // --------------------------------------------
            DateTime? desde = ParseDate(fDesde);
            DateTime? hasta = ParseDate(fHasta);

            // --------------------------------------------
            // 2) Base query: ignoramos eliminados lógicos
            // --------------------------------------------
            var qry = _context.AREA
                .AsNoTracking()
                .Where(a => !a.ELIMINADO);

            // --------------------------------------------
            // 3) Filtro por estado (ACTIVO/INACTIVO) o TODOS
            // --------------------------------------------
            if (estadoNorm == "ACTIVO" || estadoNorm == "INACTIVO")
                qry = qry.Where(a => a.ESTADO == estadoNorm);
            // Si es "TODOS", no se filtra por ESTADO.

            // --------------------------------------------
            // 4) Búsqueda por ID o Nombre (contains)
            // --------------------------------------------
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                qry = qry.Where(a =>
                    a.AREA_ID.Contains(term) ||
                    a.AREA_NOMBRE.Contains(term));
            }

            // --------------------------------------------
            // 5) Filtro de fechas por FECHA_CREACION (rango inclusivo)
            // --------------------------------------------
            if (desde.HasValue) qry = qry.Where(a => a.FECHA_CREACION >= desde.Value.Date);
            if (hasta.HasValue) qry = qry.Where(a => a.FECHA_CREACION < hasta.Value.Date.AddDays(1));

            // --------------------------------------------
            // 6) Proyección a ViewModel + orden
            //    (IMPORTANTE: ordene ANTES de paginar)
            // --------------------------------------------
            var proyectado = qry
                .OrderBy(a => a.AREA_ID)
                .Select(a => new AreasViewModel
                {
                    areaId = a.AREA_ID,
                    areaNombre = a.AREA_NOMBRE,
                    areaDescripcion = a.AREA_DESCRIPCION,
                    estadoArea = a.ESTADO,
                    FechaCreacion = a.FECHA_CREACION
                });

            // --------------------------------------------
            // 7) Paginación (normalizamos pageSize permitido)
            // --------------------------------------------
            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 10;

            var resultado = await proyectado.ToPagedAsync(page, pageSize);

            // --------------------------------------------
            // 8) RouteValues para el paginador (preservar filtros)
            // --------------------------------------------
            resultado.RouteValues["estado"] = estadoNorm;
            resultado.RouteValues["q"] = q;
            resultado.RouteValues["fDesde"] = desde?.ToString("yyyy-MM-dd");
            resultado.RouteValues["fHasta"] = hasta?.ToString("yyyy-MM-dd");

            // (Opcional) Variables para su toolbar actual
            ViewBag.Estado = estadoNorm;
            ViewBag.Q = q;
            ViewBag.FDesde = resultado.RouteValues["fDesde"];
            ViewBag.FHasta = resultado.RouteValues["fHasta"];

            return View(resultado); // ← ahora la vista recibe PaginadoViewModel<AreasViewModel>
        }

        // --------------------------------------------
        // Utilidad privada para parsear fechas del querystring
        // --------------------------------------------
        private static DateTime? ParseDate(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            var formats = new[] { "dd/MM/yyyy", "yyyy-MM-dd" };
            if (DateTime.TryParseExact(input, formats,
                    CultureInfo.GetCultureInfo("es-GT"),
                    DateTimeStyles.None, out var d))
                return d;

            // Fallback genérico
            if (DateTime.TryParse(input, out d)) return d;
            return null;
        }










        public async Task<IActionResult> Details(string id)
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





        // =======================
        // GET: /Areas/Create
        // =======================
        [HttpGet] // <-- importante para evitar ambigüedad
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

        // =======================
        // POST: /Areas/Create
        // =======================
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
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var aREA = await _context.AREA.FindAsync(id);
            if (aREA == null)
            {
                return NotFound();
            }
            return View(aREA);
        }




        // POST: Areas/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("AREA_ID,AREA_NOMBRE,AREA_DESCRIPCION,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO")] AREA aREA)
        {
            if (id != aREA.AREA_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(aREA);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AREAExists(aREA.AREA_ID))
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
            return View(aREA);
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
