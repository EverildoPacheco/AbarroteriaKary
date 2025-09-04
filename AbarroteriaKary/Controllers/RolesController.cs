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
using AbarroteriaKary.ModelsPartial.Commons;



namespace AbarroteriaKary.Controllers
{
    public class RolesController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;
        private readonly IAuditoriaService _auditoria;

        public RolesController(KaryDbContext context, ICorrelativoService correlativos, IAuditoriaService auditoria)
        {
            _context = context;
            _correlativos = correlativos;
            _auditoria = auditoria;
        }

        // GET: Roles
        // GET: Roles
        public async Task<IActionResult> Index(
            string? estado, string? q = null, string? fDesde = null, string? fHasta = null,
            int page = 1, int pageSize = 10)
        {
            // 0) Normaliza estado
            var estadoNorm = (estado ?? "ACTIVO").Trim().ToUpperInvariant();
            if (estadoNorm is not ("ACTIVO" or "INACTIVO" or "TODOS"))
                estadoNorm = "ACTIVO";

            // 1) Parseo de fechas
            DateTime? desde = ParseDate(fDesde);
            DateTime? hasta = ParseDate(fHasta);

            // 2) Base query (ignorar eliminados)
            var qry = _context.ROL
                .AsNoTracking()
                .Where(r => !r.ELIMINADO);

            // 3) Filtro por estado
            if (estadoNorm is "ACTIVO" or "INACTIVO")
                qry = qry.Where(r => r.ESTADO == estadoNorm);

            // 4) Búsqueda
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                qry = qry.Where(r =>
                    EF.Functions.Like(r.ROL_ID, term) ||
                    EF.Functions.Like(r.ROL_NOMBRE, term));
            }

            // 5) Rango de fechas (inclusivo)
            if (desde.HasValue) qry = qry.Where(r => r.FECHA_CREACION >= desde.Value.Date);
            if (hasta.HasValue) qry = qry.Where(r => r.FECHA_CREACION < hasta.Value.Date.AddDays(1));

            // 6) Orden + PROYECCIÓN a RolViewModel (ANTES de paginar)
            var proyectado = qry
                .OrderBy(r => r.ROL_ID)
                .Select(r => new AbarroteriaKary.ModelsPartial.RolViewModel
                {
                    IdRol = r.ROL_ID,
                    NombreRol = r.ROL_NOMBRE,
                    DescripcionRol = r.ROL_DESCRIPCION,

                    // ESTADO setter sincroniza el checkbox EstadoActivo
                    ESTADO = r.ESTADO,
                    FechaCreacion = r.FECHA_CREACION

                    // Auditoría (para que FechaCreacion en el VM tenga valor)

                });

            // 7) Paginación
            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 10;

            var resultado = await proyectado.ToPagedAsync(page, pageSize); // <- devuelve PaginadoViewModel<RolViewModel>

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
            var entidad = await _context.ROL
                .AsNoTracking()
                //.Include(p => p.AREA)
                .FirstOrDefaultAsync(r => r.ROL_ID == id);

            if (entidad is null) return NotFound();

            // 3) Proyección a ViewModel (lo que usaremos en la vista)
            var vm = new RolViewModel
            {
                IdRol = entidad.ROL_ID,
                NombreRol = entidad.ROL_NOMBRE,
                DescripcionRol = entidad.ROL_DESCRIPCION,
                //AREA_ID = entidad.AREA_ID,
                //AREA_NOMBRE = entidad.AREA?.AREA_NOMBRE, // ← nombre del Área para mostrar
                ESTADO = entidad.ESTADO,
                EstadoActivo = string.Equals(entidad.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),
                FechaCreacion = entidad.FECHA_CREACION
            };

            // 4) Auditoría (opcional): disponible en la vista si la quieres mostrar
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



        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new RolViewModel
            {
                // Solo “preview” del siguiente ID de rol (no definitivo)
                IdRol = await _correlativos.PeekNextRolIdAsync(),

                // El setter de ESTADO sincroniza EstadoActivo, por lo que no es necesario asignar ambos
                ESTADO = "ACTIVO",

                FechaCreacion = DateTime.Now


            };

            // Flags de modal de éxito (opcional)
            ViewBag.SavedOk = TempData["SavedOk"];
            ViewBag.SavedName = TempData["SavedName"];

            return View(vm);
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RolViewModel vm)
        {
            // Normalización básica (evita espacios/valores nulos)
            vm.NombreRol = (vm.NombreRol ?? string.Empty).Trim();
            vm.DescripcionRol = vm.DescripcionRol?.Trim();

            if (!ModelState.IsValid)
            {
                // Si se perdió el “preview” del ID, reponerlo (para Roles, no Áreas/Puestos)
                if (string.IsNullOrWhiteSpace(vm.IdRol))
                    vm.IdRol = await _correlativos.PeekNextRolIdAsync();

                return View(vm);
            }

            var ahora = DateTime.Now;
            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync(); // su servicio de auditoría

            // Validación de duplicado (ajustada a Roles)
            var existe = await _context.ROL.AnyAsync(r =>
                !r.ELIMINADO &&
                r.ROL_NOMBRE == vm.NombreRol
            );
            if (existe)
            {
                ModelState.AddModelError(nameof(vm.NombreRol), "Ya existe un rol con ese nombre.");
                if (string.IsNullOrWhiteSpace(vm.IdRol))
                    vm.IdRol = await _correlativos.PeekNextRolIdAsync();
                return View(vm);
            }

            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                // ID definitivo atómico para Rol (NO NextAreaIdAsync / NO PeekNextPuestoIdAsync)
                var nuevoId = await _correlativos.NextRolIdAsync();

                var entidad = new ROL
                {
                    ROL_ID = nuevoId,
                    ROL_NOMBRE = vm.NombreRol,
                    ROL_DESCRIPCION = vm.DescripcionRol,
                    ESTADO = vm.EstadoActivo ? "ACTIVO" : "INACTIVO",
                    ELIMINADO = false,

                    // Auditoría de alta
                    CREADO_POR = usuarioNombre,
                    FECHA_CREACION = ahora
                };

                _context.ROL.Add(entidad);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                // Para modal de éxito
                TempData["SavedOk"] = true;
                TempData["SavedName"] = entidad.ROL_NOMBRE;

                // PRG → permite altas consecutivas cómodamente
                return RedirectToAction(nameof(Create));
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError(string.Empty, $"Error BD: {ex.GetBaseException().Message}");

                if (string.IsNullOrWhiteSpace(vm.IdRol))
                    vm.IdRol = await _correlativos.PeekNextRolIdAsync();

                return View(vm);
            }
        }



        // GET: Puesto/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var entidad = await _context.ROL
                .AsNoTracking()
                //.Include(r => r.ROL_ID)
                .FirstOrDefaultAsync(r => r.ROL_ID == id);

            if (entidad == null) return NotFound();

            var vm = new RolViewModel
            {
                IdRol = entidad.ROL_ID,
                NombreRol = entidad.ROL_NOMBRE,
                DescripcionRol = entidad.ROL_DESCRIPCION,
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
        public async Task<IActionResult> Edit(string id, RolViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(id) || !string.Equals(id, vm.IdRol, StringComparison.Ordinal))
                return NotFound();

            if (!ModelState.IsValid)
            {
                //CargarAreas(vm);
                return View(vm);
            }

            var entidad = await _context.ROL.FirstOrDefaultAsync(p => p.ROL_ID == id);
            if (entidad == null) return NotFound();

            // ====== Normalización de datos nuevos desde el VM ======
            var nuevoNombre = (vm.NombreRol ?? string.Empty).Trim();
            var nuevaDesc = vm.DescripcionRol?.Trim();
            //var nuevaAreaId = vm.AREA_ID;
            var nuevoEstado = vm.EstadoActivo ? "ACTIVO" : "INACTIVO";

            // ====== ¿Hay cambios? (nombre, desc, área, estado) ======
            var sinCambios =
                string.Equals(entidad.ROL_NOMBRE ?? "", nuevoNombre, StringComparison.Ordinal) &&
                string.Equals(entidad.ROL_DESCRIPCION ?? "", nuevaDesc ?? "", StringComparison.Ordinal) &&
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
            entidad.ROL_NOMBRE = nuevoNombre;
            entidad.ROL_DESCRIPCION = nuevaDesc;
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
                TempData["UpdatedName"] = entidad.ROL_NOMBRE;
                return RedirectToAction(nameof(Edit), new { id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.ROL.AnyAsync(e => e.ROL_ID == id)) return NotFound();
                throw;
            }
        }





















        // GET: Roles/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rOL = await _context.ROL
                .FirstOrDefaultAsync(m => m.ROL_ID == id);
            if (rOL == null)
            {
                return NotFound();
            }

            return View(rOL);
        }

        // POST: Roles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var rOL = await _context.ROL.FindAsync(id);
            if (rOL != null)
            {
                _context.ROL.Remove(rOL);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ROLExists(string id)
        {
            return _context.ROL.Any(e => e.ROL_ID == id);
        }
    }
}
