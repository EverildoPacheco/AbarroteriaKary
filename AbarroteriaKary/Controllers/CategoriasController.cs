using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.ModelsPartial.Paginacion;           // PaginadoViewModel<T>
using AbarroteriaKary.Services.Auditoria;
using AbarroteriaKary.Services.Correlativos;
using AbarroteriaKary.Services.Extensions;                // ToPagedAsync extension
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;


namespace AbarroteriaKary.Controllers
{
    public class CategoriasController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;
        private readonly IAuditoriaService _auditoria;

        public CategoriasController(KaryDbContext context, ICorrelativoService correlativos, IAuditoriaService auditoria)
        {
            _context = context;
            _correlativos = correlativos;
            _auditoria = auditoria;
        }

        // GET: Categorias
        // GET: /Categoria
        // Listado con filtros: estado, texto, fechas y paginación
        public async Task<IActionResult> Index(string? estado, string? q = null, string? fDesde = null, string? fHasta = null, int page = 1, int pageSize = 10)
        {
            // 0) Normalización de estado
            var estadoNorm = (estado ?? "ACTIVO").Trim().ToUpperInvariant();
            if (estadoNorm is not ("ACTIVO" or "INACTIVO" or "TODOS"))
                estadoNorm = "ACTIVO";

            // 1) Fechas (acepta dd/MM/yyyy o yyyy-MM-dd)
            DateTime? desde = ParseDate(fDesde);
            DateTime? hasta = ParseDate(fHasta);

            // 2) Base query (ignora eliminados)
            var qry = _context.CATEGORIA
                .AsNoTracking()
                .Where(c => !c.ELIMINADO);

            // 3) Filtro por estado
            if (estadoNorm is "ACTIVO" or "INACTIVO")
                qry = qry.Where(c => c.ESTADO == estadoNorm);

            // 4) Búsqueda por texto (ID, Nombre, Descripción)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                qry = qry.Where(c =>
                    EF.Functions.Like(c.CATEGORIA_ID, term) ||
                    EF.Functions.Like(c.CATEGORIA_NOMBRE, term) ||
                    (c.CATEGORIA_DESCRIPCION != null && EF.Functions.Like(c.CATEGORIA_DESCRIPCION, term))
                );
            }

            // 5) Rango de fechas (inclusivo)
            if (desde.HasValue) qry = qry.Where(c => c.FECHA_CREACION >= desde.Value.Date);
            if (hasta.HasValue) qry = qry.Where(c => c.FECHA_CREACION < hasta.Value.Date.AddDays(1));

            // 6) Ordenamiento + proyección a ViewModel (ANTES de paginar)
            var proyectado = qry
                .OrderBy(c => c.CATEGORIA_NOMBRE)
                .Select(c => new CategoriaViewModel
                {
                    CategoriaID = c.CATEGORIA_ID,
                    CategoriaNombre = c.CATEGORIA_NOMBRE,
                    CategoriaDescripcion = c.CATEGORIA_DESCRIPCION,
                    ESTADO = c.ESTADO,
                    EstadoActivo = c.ESTADO == "ACTIVO",
                    FechaCreacion = c.FECHA_CREACION
                });

            // 7) Paginación (normaliza pageSize)
            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 10;

            var resultado = await proyectado.ToPagedAsync(page, pageSize);

            // 8) RouteValues para el pager (conservar filtros)
            resultado.RouteValues["estado"] = estadoNorm;
            resultado.RouteValues["q"] = q;
            resultado.RouteValues["fDesde"] = desde?.ToString("yyyy-MM-dd");
            resultado.RouteValues["fHasta"] = hasta?.ToString("yyyy-MM-dd");

            // Toolbar (persistencia de filtros en la vista)
            ViewBag.Estado = estadoNorm;
            ViewBag.Q = q;
            ViewBag.FDesde = resultado.RouteValues["fDesde"];
            ViewBag.FHasta = resultado.RouteValues["fHasta"];
            ViewBag.PageSize = pageSize;

            return View(resultado); // Views/Categoria/Index.cshtml
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






        public async Task<IActionResult> Details(string id)
        {
            // 1) Validación de parámetro
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // 2) Carga de entidad con el Área (solo lectura)
            var entidad = await _context.CATEGORIA
                .AsNoTracking()
                //.Include(p => p.AREA)
                .FirstOrDefaultAsync(r => r.CATEGORIA_ID == id);

            if (entidad is null) return NotFound();

            // 3) Proyección a ViewModel (lo que usaremos en la vista)
            var vm = new CategoriaViewModel
            {
                CategoriaID = entidad.CATEGORIA_ID,
                CategoriaNombre = entidad.CATEGORIA_NOMBRE,
                CategoriaDescripcion = entidad.CATEGORIA_DESCRIPCION,
                //AREA_ID = entidad.AREA_ID,
                //AREA_NOMBRE = entidad.AREA?.AREA_NOMBRE, // ← nombre del Área para mostrar
                ESTADO = entidad.ESTADO,
                EstadoActivo = string.Equals(entidad.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),
                FechaCreacion = entidad.FECHA_CREACION
            };

            // 4) Auditoría (): disponible en la vista 
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





        // ==========================================
        // GET: /Categoria/Create
        // - Muestra el formulario con un "preview" del siguiente ID (no definitivo).
        // - Estado por defecto: ACTIVO.
        // - Envía flags para modal de éxito (opcional).
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            var vm = new CategoriaViewModel
            {
                // Solo vista previa; el definitivo se genera en el POST con NextCategoriaIdAsync()
                CategoriaID = await _correlativos.PeekNextCategoriaIdAsync(ct),
                ESTADO = "ACTIVO",
                EstadoActivo = true,
                FechaCreacion = DateTime.Now
            };

            // Flags de modal de éxito (opcional, por si muestra un toast/modal)
            ViewBag.SavedOk = TempData["SavedOk"];
            ViewBag.SavedName = TempData["SavedName"];

            return View(vm); // Views/Categoria/Create.cshtml
        }

        // ==========================================
        // POST: /Categoria/Create
        // - Normaliza entrada.
        // - Valida duplicado por NOMBRE (ignora eliminados).
        // - Genera ID definitivo (NEXT) dentro de transacción.
        // - PRG → RedirectToAction(Create) para permitir altas consecutivas.
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoriaViewModel vm, CancellationToken ct)
        {
            // 1) Sincroniza checkbox -> cadena ESTADO
            vm.RefrescarEstadoDesdeBool();

            // 2) Normalización básica (evita espacios/valores nulos)
            vm.CategoriaNombre = (vm.CategoriaNombre ?? string.Empty).Trim();
            vm.CategoriaDescripcion = vm.CategoriaDescripcion?.Trim();

            // 3) Validación servidor
            if (!ModelState.IsValid)
            {
                // Reponer "preview" si se perdió
                if (string.IsNullOrWhiteSpace(vm.CategoriaID))
                    vm.CategoriaID = await _correlativos.PeekNextCategoriaIdAsync(ct);

                return View(vm);
            }

            // 4) Regla de dominio: nombre único entre no eliminados
            var duplicado = await _context.CATEGORIA.AnyAsync(c =>
                !c.ELIMINADO && c.CATEGORIA_NOMBRE == vm.CategoriaNombre, ct);

            if (duplicado)
            {
                ModelState.AddModelError(nameof(vm.CategoriaNombre), "Ya existe una categoría con ese nombre.");
                if (string.IsNullOrWhiteSpace(vm.CategoriaID))
                    vm.CategoriaID = await _correlativos.PeekNextCategoriaIdAsync(ct);
                return View(vm);
            }

            // 5) Alta dentro de transacción para asegurar correlativo único
            var ahora = DateTime.Now;
            var usuario = User?.Identity?.Name ?? "SYSTEM";

            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            try
            {
                // ID definitivo (atómico)
                var nuevoId = await _correlativos.NextCategoriaIdAsync(ct);

                var entidad = new CATEGORIA
                {
                    CATEGORIA_ID = nuevoId,
                    CATEGORIA_NOMBRE = vm.CategoriaNombre,
                    CATEGORIA_DESCRIPCION = vm.CategoriaDescripcion,
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false,

                    // Auditoría de alta
                    CREADO_POR = usuario,
                    FECHA_CREACION = ahora,
                    MODIFICADO_POR = null,
                    FECHA_MODIFICACION = null,
                    ELIMINADO_POR = null,
                    FECHA_ELIMINACION = null
                };

                _context.CATEGORIA.Add(entidad);
                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                // Flags para modal/toast de éxito
                TempData["SavedOk"] = true;
                TempData["SavedName"] = entidad.CATEGORIA_NOMBRE;

                // PRG: permite registrar varias categorías seguidas
                return RedirectToAction(nameof(Create));
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync(ct);

                // Mensaje legible para el usuario (sin stack técnico)
                ModelState.AddModelError(string.Empty, $"Error al guardar en base de datos: {ex.GetBaseException().Message}");

                // Reponer "preview" si procede
                if (string.IsNullOrWhiteSpace(vm.CategoriaID))
                    vm.CategoriaID = await _correlativos.PeekNextCategoriaIdAsync(ct);

                return View(vm);
            }
        }







        // GET: Puesto/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var entidad = await _context.CATEGORIA
                .AsNoTracking()
                //.Include(r => r.ROL_ID)
                .FirstOrDefaultAsync(r => r.CATEGORIA_ID == id);

            if (entidad == null) return NotFound();

            var vm = new CategoriaViewModel
            {
                CategoriaID = entidad.CATEGORIA_ID,
                CategoriaNombre = entidad.CATEGORIA_NOMBRE,
                CategoriaDescripcion = entidad.CATEGORIA_DESCRIPCION,
                //AREA_ID = entidad.AREA_ID,
                //AREA_NOMBRE = entidad.AREA?.AREA_NOMBRE,
                ESTADO = entidad.ESTADO,
                EstadoActivo = string.Equals(entidad.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),
                FechaCreacion = entidad.FECHA_CREACION
            };

            //CargarAreas(vm);
            ViewBag.UpdatedOk = TempData["UpdatedOk"];
            ViewBag.UpdatedName = TempData["UpdatedName"];
            ViewBag.NoChanges = TempData["NoChanges"];

            return View(vm);
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, CategoriaViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(id) || !string.Equals(id, vm.CategoriaID, StringComparison.Ordinal))
                return NotFound();

            if (!ModelState.IsValid)
            {
                //CargarAreas(vm);
                return View(vm);
            }

            var entidad = await _context.CATEGORIA.FirstOrDefaultAsync(p => p.CATEGORIA_ID == id);
            if (entidad == null) return NotFound();

            // ====== Normalización de datos nuevos desde el VM ======
            var nuevoNombre = (vm.CategoriaNombre ?? string.Empty).Trim();
            var nuevaDesc = vm.CategoriaDescripcion?.Trim();
            //var nuevaAreaId = vm.AREA_ID;
            var nuevoEstado = vm.EstadoActivo ? "ACTIVO" : "INACTIVO";

            // ====== ¿Hay cambios? (nombre, desc, área, estado) ======
            var sinCambios =
                string.Equals(entidad.CATEGORIA_NOMBRE ?? "", nuevoNombre, StringComparison.Ordinal) &&
                string.Equals(entidad.CATEGORIA_DESCRIPCION ?? "", nuevaDesc ?? "", StringComparison.Ordinal) &&
                //string.Equals(entidad.AREA_ID ?? "", nuevaAreaId ?? "", StringComparison.Ordinal) &&
                string.Equals(entidad.ESTADO ?? "", nuevoEstado, StringComparison.Ordinal);

            if (sinCambios)
            {
                TempData["NoChanges"] = true;
                return RedirectToAction(nameof(Edit), new { id });
            }

            // ====== Usuario y fecha para auditoría (★ servicio) ======
            var ahora = DateTime.Now;
            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync(); // ★ AQUÍ LA INTEGRACIÓN

            // ====== Aplicar cambios base ======
            entidad.CATEGORIA_NOMBRE = nuevoNombre;
            entidad.CATEGORIA_DESCRIPCION = nuevaDesc;
            //entidad.AREA_ID = nuevaAreaId;

            var estadoOriginalActivo = string.Equals(entidad.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase);
            var estadoNuevoActivo = vm.EstadoActivo;

            if (estadoOriginalActivo != estadoNuevoActivo)
            {
                if (!estadoNuevoActivo)
                {
                    // → DESACTIVAR (NO tocar ELIMINADO: debe seguir en listados)
                    entidad.ESTADO = "INACTIVO";

                    // Opcional: registrar quién lo desactivó y cuándo (sin marcar ELIMINADO)
                    entidad.ELIMINADO_POR = usuarioNombre;   // rastro de “desactivación”
                    entidad.FECHA_ELIMINACION = ahora;
                    // entidad.ELIMINADO = false; // ← asegúrate que permanezca en false
                }
                else
                {
                    // → REACTIVAR
                    entidad.ESTADO = "ACTIVO";

                    //Opcional: limpiar rastro, o conservarlo si prefieres histórico
                    entidad.ELIMINADO_POR = usuarioNombre;
                    entidad.FECHA_ELIMINACION = ahora;

                    // entidad.ELIMINADO = false; // siempre false para que aparezca en listados
                }
            }
            else
            {
                // Sin cambio de estado, solo sincroniza por claridad
                entidad.ESTADO = estadoNuevoActivo ? "ACTIVO" : "INACTIVO";
                // No tocar ELIMINADO aquí
            }

            // ====== Auditoría de modificación ======
            entidad.MODIFICADO_POR = usuarioNombre;           // ★ nombre real del usuario
            entidad.FECHA_MODIFICACION = ahora;

            try
            {
                await _context.SaveChangesAsync();

                TempData["UpdatedOk"] = true;
                TempData["UpdatedName"] = entidad.CATEGORIA_NOMBRE;
                return RedirectToAction(nameof(Edit), new { id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.CATEGORIA.AnyAsync(e => e.CATEGORIA_ID == id)) return NotFound();
                throw;
            }
        }



























        // GET: Categorias/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cATEGORIA = await _context.CATEGORIA
                .FirstOrDefaultAsync(m => m.CATEGORIA_ID == id);
            if (cATEGORIA == null)
            {
                return NotFound();
            }

            return View(cATEGORIA);
        }

        // POST: Categorias/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var cATEGORIA = await _context.CATEGORIA.FindAsync(id);
            if (cATEGORIA != null)
            {
                _context.CATEGORIA.Remove(cATEGORIA);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CATEGORIAExists(string id)
        {
            return _context.CATEGORIA.Any(e => e.CATEGORIA_ID == id);
        }
    }
}
