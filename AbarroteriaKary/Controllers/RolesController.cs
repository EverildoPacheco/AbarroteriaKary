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







        // GET: Roles/Details/5
        public async Task<IActionResult> Details(string id)
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










        // POST: Roles/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("ROL_ID,ROL_NOMBRE,ROL_DESCRIPCION,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO")] ROL rOL)
        {
            if (id != rOL.ROL_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(rOL);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ROLExists(rOL.ROL_ID))
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
            return View(rOL);
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
