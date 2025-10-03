using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.ModelsPartial.Commons;
using AbarroteriaKary.ModelsPartial.Paginacion;           // PaginadoViewModel<T>
using AbarroteriaKary.Services.Auditoria;
using AbarroteriaKary.Services.Correlativos;
using AbarroteriaKary.Services.Extensions;                // ToPagedAsync extension
using AbarroteriaKary.Services.Reportes;
using DocumentFormat.OpenXml.Spreadsheet;
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
    public class CajasController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;
        private readonly IAuditoriaService _auditoria;
        private readonly IReporteExportService _exportSvc;

        public CajasController(KaryDbContext context, ICorrelativoService correlativos, IAuditoriaService auditoria, IReporteExportService exportSvc)
        {
            _context = context;
            _correlativos = correlativos;
            _auditoria = auditoria;
            _exportSvc = exportSvc;
        }

        // GET: Cajas
     
      
            public async Task<IActionResult> Index(
                string? estado, string? q = null, string? fDesde = null, string? fHasta = null,
                int page = 1, int pageSize = 10)
            {
                // 0) Normaliza estado
                var estadoNorm = (estado ?? "ACTIVO").Trim().ToUpperInvariant();
                if (estadoNorm is not ("ACTIVO" or "INACTIVO" or "TODOS"))
                    estadoNorm = "ACTIVO";

                // 1) Parseo de fechas (incluye formatos es-GT y yyyy-MM-dd)
                DateTime? desde = ParseDate(fDesde);
                DateTime? hasta = ParseDate(fHasta);

                // 2) Base query (ignorar eliminados)
                var qry = _context.CAJA
                    .AsNoTracking()
                    .Where(c => !c.ELIMINADO);

                // 3) Filtro por estado
                if (estadoNorm is "ACTIVO" or "INACTIVO")
                    qry = qry.Where(c => c.ESTADO == estadoNorm);

                // 4) Búsqueda (por ID o Nombre)
                if (!string.IsNullOrWhiteSpace(q))
                {
                    var term = $"%{q.Trim()}%";
                    qry = qry.Where(c =>
                        EF.Functions.Like(c.CAJA_ID, term) ||
                        EF.Functions.Like(c.CAJA_NOMBRE, term));
                }

                // 5) Rango de fechas (inclusivo)
                if (desde.HasValue) qry = qry.Where(c => c.FECHA_CREACION >= desde.Value.Date);
                if (hasta.HasValue) qry = qry.Where(c => c.FECHA_CREACION < hasta.Value.Date.AddDays(1));

                // 6) Orden + PROYECCIÓN a CajaViewModels (ANTES de paginar)
                var proyectado = qry
                    .OrderBy(c => c.CAJA_ID)
                    .Select(c => new CajaViewModels
                    {
                        cajaID = c.CAJA_ID,
                        NombreCaja = c.CAJA_NOMBRE,

                        // El setter de ESTADO sincroniza EstadoActivo
                        ESTADO = c.ESTADO,

                        FechaCreacion = c.FECHA_CREACION
                    });

                // 7) Paginación
                var permitidos = new[] { 10, 25, 50, 100 };
                pageSize = permitidos.Contains(pageSize) ? pageSize : 10;

                var resultado = await proyectado.ToPagedAsync(page, pageSize); // <- PaginadoViewModel<CajaViewModels>

                // 8) RouteValues (para pager)
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

            // Helper local para parsear fechas como en otros controladores
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

            // ============================================================
            // DETAILS: detalle de una caja
            // Ruta: GET /Caja/Details/{id}
            // Vista: Views/Caja/Details.cshtml
            // ============================================================
            public async Task<IActionResult> Details(string id)
            {
                // 1) Validación de parámetro
                if (string.IsNullOrWhiteSpace(id)) return NotFound();

                // 2) Carga de entidad (solo lectura)
                var entidad = await _context.CAJA
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CAJA_ID == id);

                if (entidad is null) return NotFound();

                // 3) Proyección a ViewModel
                var vm = new CajaViewModels
                {
                    cajaID = entidad.CAJA_ID,
                    NombreCaja = entidad.CAJA_NOMBRE,
                    ESTADO = entidad.ESTADO,
                    EstadoActivo = string.Equals(entidad.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),
                    FechaCreacion = entidad.FECHA_CREACION
                };

                // 4) Auditoría (disponible en la vista)
                ViewBag.Auditoria = new
                {
                    CreadoPor = entidad.CREADO_POR,
                    FechaCreacion = entidad.FECHA_CREACION,
                    ModificadoPor = entidad.MODIFICADO_POR,
                    FechaModificacion = entidad.FECHA_MODIFICACION,
                    EliminadoPor = entidad.ELIMINADO_POR,
                    FechaEliminacion = entidad.FECHA_ELIMINACION
                };

                // 5) Devolver la vista tipada al ViewModel
                return View(vm);
            }

            // ============================================================
            // CREATE (GET): muestra formulario con “preview” del ID
            // Ruta: GET /Caja/Create
            // Vista: Views/Caja/Create.cshtml
            // ============================================================
            [HttpGet]
            public async Task<IActionResult> Create()
            {
                var vm = new CajaViewModels
                {
                    // Solo “preview” del siguiente ID (no definitivo)
                    cajaID = await _correlativos.PeekNextCajaIdAsync(),

                    // El setter de ESTADO sincroniza EstadoActivo, por lo que no es necesario asignar ambos
                    ESTADO = "ACTIVO",

                    FechaCreacion = DateTime.Now
                };

                // Flags de modal de éxito (opcional)
                ViewBag.SavedOk = TempData["SavedOk"];
                ViewBag.SavedName = TempData["SavedName"];

                return View(vm);
            }

            // ============================================================
            // CREATE (POST): inserta la nueva caja (ID definitivo con correlativo)
            // Ruta: POST /Caja/Create
            // ============================================================
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Create(CajaViewModels vm)
            {
                // Normalización básica
                vm.NombreCaja = (vm.NombreCaja ?? string.Empty).Trim();

                if (!ModelState.IsValid)
                {
                    // Reponer preview de ID si se perdió
                    if (string.IsNullOrWhiteSpace(vm.cajaID))
                        vm.cajaID = await _correlativos.PeekNextCajaIdAsync();

                    return View(vm);
                }

                var ahora = DateTime.Now;
                var usuarioNombre = await _auditoria.GetUsuarioNombreAsync(); // su servicio de auditoría

                // Validación de duplicado
                var existe = await _context.CAJA.AnyAsync(c =>
                    !c.ELIMINADO &&
                    c.CAJA_NOMBRE == vm.NombreCaja
                );
                if (existe)
                {
                    ModelState.AddModelError(nameof(vm.NombreCaja), "Ya existe una caja con ese nombre.");
                    if (string.IsNullOrWhiteSpace(vm.cajaID))
                        vm.cajaID = await _correlativos.PeekNextCajaIdAsync();

                    return View(vm);
                }

                await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
                try
                {
                    // ID definitivo atómico para CAJA
                    var nuevoId = await _correlativos.NextCajaIdAsync();

                    var entidad = new CAJA
                    {
                        CAJA_ID = nuevoId,
                        CAJA_NOMBRE = vm.NombreCaja,
                        ESTADO = vm.EstadoActivo ? "ACTIVO" : "INACTIVO",
                        ELIMINADO = false,

                        // Auditoría de alta
                        CREADO_POR = usuarioNombre,
                        FECHA_CREACION = ahora
                    };

                    _context.CAJA.Add(entidad);
                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    // Para modal de éxito
                    TempData["SavedOk"] = true;
                    TempData["SavedName"] = entidad.CAJA_NOMBRE;

                    // PRG → permite altas consecutivas cómodamente
                    return RedirectToAction(nameof(Create));
                }
                catch (DbUpdateException ex)
                {
                    await tx.RollbackAsync();
                    ModelState.AddModelError(string.Empty, $"Error BD: {ex.GetBaseException().Message}");

                    if (string.IsNullOrWhiteSpace(vm.cajaID))
                        vm.cajaID = await _correlativos.PeekNextCajaIdAsync();

                    return View(vm);
                }
            }

            // ============================================================
            // EDIT (GET): muestra formulario de edición
            // Ruta: GET /Caja/Edit/{id}
            // Vista: Views/Caja/Edit.cshtml
            // ============================================================
            [HttpGet]
            public async Task<IActionResult> Edit(string id)
            {
                if (string.IsNullOrWhiteSpace(id)) return NotFound();

                var entidad = await _context.CAJA
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CAJA_ID == id);

                if (entidad == null) return NotFound();

                var vm = new CajaViewModels
                {
                    cajaID = entidad.CAJA_ID,
                    NombreCaja = entidad.CAJA_NOMBRE,
                    ESTADO = entidad.ESTADO,
                    EstadoActivo = string.Equals(entidad.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),
                    FechaCreacion = entidad.FECHA_CREACION
                };

                ViewBag.UpdatedOk = TempData["UpdatedOk"];
                ViewBag.UpdatedName = TempData["UpdatedName"];
                ViewBag.NoChanges = TempData["NoChanges"];

                return View(vm);
            }

            // ============================================================
            // EDIT (POST): aplica cambios con auditoría y control de estado
            // Ruta: POST /Caja/Edit/{id}
            // ============================================================
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Edit(string id, CajaViewModels vm)
            {
                if (string.IsNullOrWhiteSpace(id) || !string.Equals(id, vm.cajaID, StringComparison.Ordinal))
                    return NotFound();

                if (!ModelState.IsValid)
                {
                    return View(vm);
                }

                var entidad = await _context.CAJA.FirstOrDefaultAsync(c => c.CAJA_ID == id);
                if (entidad == null) return NotFound();

                // ====== Normalización de datos nuevos desde el VM ======
                var nuevoNombre = (vm.NombreCaja ?? string.Empty).Trim();
                var nuevoEstado = vm.EstadoActivo ? "ACTIVO" : "INACTIVO";

                // ====== ¿Hay cambios? (nombre, estado) ======
                var sinCambios =
                    string.Equals(entidad.CAJA_NOMBRE ?? "", nuevoNombre, StringComparison.Ordinal) &&
                    string.Equals(entidad.ESTADO ?? "", nuevoEstado, StringComparison.Ordinal);

                if (sinCambios)
                {
                    TempData["NoChanges"] = true;
                    return RedirectToAction(nameof(Edit), new { id });
                }

                // ====== Usuario y fecha para auditoría ======
                var ahora = DateTime.Now;
                var usuarioNombre = await _auditoria.GetUsuarioNombreAsync();

                // ====== Validación de duplicado de nombre (si cambió) ======
                if (!string.Equals(entidad.CAJA_NOMBRE ?? "", nuevoNombre, StringComparison.Ordinal))
                {
                    var nombreDuplicado = await _context.CAJA.AnyAsync(c =>
                        !c.ELIMINADO &&
                        c.CAJA_NOMBRE == nuevoNombre &&
                        c.CAJA_ID != id
                    );
                    if (nombreDuplicado)
                    {
                        ModelState.AddModelError(nameof(vm.NombreCaja), "Ya existe una caja con ese nombre.");
                        return View(vm);
                    }
                }

                // ====== Aplicar cambios ======
                entidad.CAJA_NOMBRE = nuevoNombre;

                var estadoOriginalActivo = string.Equals(entidad.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase);
                var estadoNuevoActivo = vm.EstadoActivo;

                if (estadoOriginalActivo != estadoNuevoActivo)
                {
                    if (!estadoNuevoActivo)
                    {
                        // → DESACTIVAR (NO marcar ELIMINADO para que siga en listados)
                        entidad.ESTADO = "INACTIVO";

                        // Opcional: rastro de “desactivación” (sin eliminar lógicamente)
                        entidad.ELIMINADO_POR = usuarioNombre;
                        entidad.FECHA_ELIMINACION = ahora;
                    }
                    else
                    {
                        // → REACTIVAR
                        entidad.ESTADO = "ACTIVO";

                        // Opcional: rastro de reactivación (puede ser el mismo campo)
                        entidad.ELIMINADO_POR = usuarioNombre;
                        entidad.FECHA_ELIMINACION = ahora;
                    }
                }
                else
                {
                    // Sin cambio de estado, mantener coherencia
                    entidad.ESTADO = estadoNuevoActivo ? "ACTIVO" : "INACTIVO";
                }

                // ====== Auditoría de modificación ======
                entidad.MODIFICADO_POR = usuarioNombre;
                entidad.FECHA_MODIFICACION = ahora;

                try
                {
                    await _context.SaveChangesAsync();

                    TempData["UpdatedOk"] = true;
                    TempData["UpdatedName"] = entidad.CAJA_NOMBRE;
                    return RedirectToAction(nameof(Edit), new { id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.CAJA.AnyAsync(e => e.CAJA_ID == id)) return NotFound();
                    throw;
                }
            }






            // GET: Cajas/Delete/5
            public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cAJA = await _context.CAJA
                .FirstOrDefaultAsync(m => m.CAJA_ID == id);
            if (cAJA == null)
            {
                return NotFound();
            }

            return View(cAJA);
        }

        // POST: Cajas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var cAJA = await _context.CAJA.FindAsync(id);
            if (cAJA != null)
            {
                _context.CAJA.Remove(cAJA);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CAJAExists(string id)
        {
            return _context.CAJA.Any(e => e.CAJA_ID == id);
        }
    }
}
