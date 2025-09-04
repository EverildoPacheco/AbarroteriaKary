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
    public class ProveedoresController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;
        private readonly IAuditoriaService _auditoria;
        public ProveedoresController(KaryDbContext context, ICorrelativoService correlativos, IAuditoriaService auditoria)
        {
            _context = context;
            _correlativos = correlativos;
            _auditoria = auditoria;
        }

        // GET: Proveedores
        [HttpGet]
        public async Task<IActionResult> Index(
            string? estado, string? q = null,
            string? fDesde = null, string? fHasta = null,
            int page = 1, int pageSize = 10,
            CancellationToken ct = default)
        {
            // 0) Normalización de estado
            var estadoNorm = (estado ?? "ACTIVO").Trim().ToUpperInvariant();
            if (estadoNorm is not ("ACTIVO" or "INACTIVO" or "TODOS"))
                estadoNorm = "ACTIVO";

            // 1) Fechas (acepta dd/MM/yyyy o yyyy-MM-dd)
            DateTime? desde = ParseDate(fDesde);
            DateTime? hasta = ParseDate(fHasta);

            // 2) Base query con JOIN (evitamos depender de navegación)
            //    - Ignoramos eliminados lógicos en PROVEEDOR y PERSONA
            var qry = from pr in _context.PROVEEDOR.AsNoTracking()
                      join per in _context.PERSONA.AsNoTracking()
                        on pr.PROVEEDOR_ID equals per.PERSONA_ID
                      where !pr.ELIMINADO && !per.ELIMINADO
                      select new { pr, per };

            // 3) Filtro por estado (preferimos el de PROVEEDOR)
            if (estadoNorm is "ACTIVO" or "INACTIVO")
                qry = qry.Where(x => x.pr.ESTADO == estadoNorm);

            // 4) Búsqueda por texto:
            //    - Código (PROVEEDOR_ID)
            //    - Nombre completo
            //    - Empresa (mientras tanto usamos PROVEEDOR_OBSERVACION como “Empresa”)
            //    - CUI, NIT, Correo, Teléfono
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";

                // Construimos "Nombre completo" server-side para usar EF.Functions.Like
                var conNombre = qry.Select(x => new
                {
                    x,
                    FullName =
                        (x.per.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (x.per.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (x.per.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (x.per.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (x.per.PERSONA_SEGUNDOAPELLIDO ?? "")
                });

                qry = conNombre
                    .Where(y =>
                        EF.Functions.Like(y.x.pr.PROVEEDOR_ID, term) ||
                        EF.Functions.Like(y.FullName, term) ||
                        (y.x.per.PERSONA_CUI != null && EF.Functions.Like(y.x.per.PERSONA_CUI, term)) ||
                        (y.x.per.PERSONA_NIT != null && EF.Functions.Like(y.x.per.PERSONA_NIT, term)) ||
                        (y.x.per.PERSONA_CORREO != null && EF.Functions.Like(y.x.per.PERSONA_CORREO, term)) ||
                        (y.x.per.PERSONA_TELEFONOMOVIL != null && EF.Functions.Like(y.x.per.PERSONA_TELEFONOMOVIL, term)) ||
                        (y.x.pr.PROVEEDOR_OBSERVACION != null && EF.Functions.Like(y.x.pr.PROVEEDOR_OBSERVACION, term))
                    // Si agrega columna PROVEEDOR_EMPRESA, reemplace la línea anterior por:
                    // (y.x.pr.PROVEEDOR_EMPRESA != null && EF.Functions.Like(y.x.pr.PROVEEDOR_EMPRESA, term))
                    )
                    .Select(y => y.x); // regresar a { pr, per }
            }

            // 5) Rango de fechas (inclusivo) por FECHA_CREACION de PROVEEDOR
            if (desde.HasValue) qry = qry.Where(x => x.pr.FECHA_CREACION >= desde.Value.Date);
            if (hasta.HasValue) qry = qry.Where(x => x.pr.FECHA_CREACION < hasta.Value.Date.AddDays(1));

            // 6) Ordenamiento y proyección a ViewModel (ANTES de paginar)
            var proyectado = qry
                .OrderBy(x => x.pr.PROVEEDOR_ID)
                .Select(x => new ProveedorListItemViewModel
                {
                    ProveedorId = x.pr.PROVEEDOR_ID,
                    NombreCompleto = (
                        (x.per.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (x.per.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (x.per.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (x.per.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (x.per.PERSONA_SEGUNDOAPELLIDO ?? "")
                    ).Replace("  ", " ").Trim(),
                    // Mientras no exista PROVEEDOR_EMPRESA, usamos Observación como “Empresa”
                    Empresa = x.pr.EMPRESA,
                    TelefonoMovil = x.per.PERSONA_TELEFONOMOVIL,
                    TelefonoCasa = x.per.PERSONA_TELEFONOCASA,

                    Correo = x.per.PERSONA_CORREO,
                    ESTADO = x.pr.ESTADO,
                    FechaCreacion = x.pr.FECHA_CREACION
                });

            // 7) Paginación (normaliza pageSize)
            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 10;

            // ToPagedAsync debe devolver su PaginadoViewModel<ProveedorListItemViewModel>
            var resultado = await proyectado.ToPagedAsync(page, pageSize, ct);

            // 8) RouteValues para el pager
            resultado.RouteValues["estado"] = estadoNorm;
            resultado.RouteValues["q"] = q;
            resultado.RouteValues["fDesde"] = desde?.ToString("yyyy-MM-dd");
            resultado.RouteValues["fHasta"] = hasta?.ToString("yyyy-MM-dd");

            // Toolbar (persistencia de filtros en la vista)
            ViewBag.Estado = estadoNorm;
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






        public async Task<IActionResult> Details(string id, CancellationToken ct)
        {
            // 1) Validación de parámetro
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // 2) Carga PROVEEDOR + PERSONA (solo lectura, sin tracking)
            var data = await (from pr in _context.PROVEEDOR.AsNoTracking()
                              join per in _context.PERSONA.AsNoTracking()
                                   on pr.PROVEEDOR_ID equals per.PERSONA_ID
                              where pr.PROVEEDOR_ID == id
                              select new { pr, per })
                             .FirstOrDefaultAsync(ct);

            if (data is null) return NotFound();

            // 3) Proyección a ViewModel (sin CUI/NIT)
            var vm = new ProveedorFormViewModel
            {
                // Identificador compartido (PERSONA_ID = PROVEEDOR_ID)
                Id = data.pr.PROVEEDOR_ID,

                // PERSONA
                PrimerNombre = data.per.PERSONA_PRIMERNOMBRE,
                SegundoNombre = data.per.PERSONA_SEGUNDONOMBRE,
                TercerNombre = data.per.PERSONA_TERCERNOMBRE,
                PrimerApellido = data.per.PERSONA_PRIMERAPELLIDO,
                SegundoApellido = data.per.PERSONA_SEGUNDOAPELLIDO,
                Direccion = data.per.PERSONA_DIRECCION,
                TelefonoMovil = data.per.PERSONA_TELEFONOMOVIL,
                TelefonoCasa = data.per.PERSONA_TELEFONOCASA, // en tu UI lo usas como “Teléfono Empresa”
                Correo = data.per.PERSONA_CORREO,

                // PROVEEDOR
                Empresa = data.pr.EMPRESA,       // ✅ columna real
                ProveedorObservacion = data.pr.PROVEEDOR_OBSERVACION,

                // Estado (checkbox + string)
                ESTADO = data.pr.ESTADO,
                EstadoActivo = string.Equals(data.pr.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),

                // Auditoría mínima para mostrar si se desea
                CREADO_POR = data.pr.CREADO_POR,
                FECHA_CREACION = data.pr.FECHA_CREACION
            };

            // 4) Auditoría ampliada (opcional en la vista)
            ViewBag.Auditoria = new
            {
                CreadoPor = data.pr.CREADO_POR,
                FechaCreacion = data.pr.FECHA_CREACION,
                ModificadoPor = data.pr.MODIFICADO_POR,
                FechaModificacion = data.pr.FECHA_MODIFICACION,
                Eliminado = data.pr.ELIMINADO,
                EliminadoPor = data.pr.ELIMINADO_POR,
                FechaEliminacion = data.pr.FECHA_ELIMINACION
            };

            // 5) Devolver la vista tipada al ViewModel
            return View(vm);
        }




        // ===========================
        // GET: Proveedor/Create
        // ===========================
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            var vm = new ProveedorFormViewModel
            {
                Id = await _correlativos.PeekNextProveedorIdAsync(ct),
                ESTADO = "ACTIVO",
                EstadoActivo = true,
                FECHA_CREACION = DateTime.Now,
                CREADO_POR = await _auditoria.GetUsuarioNombreAsync() // o User?.Identity?.Name ?? "SYSTEM"
            };
            return View(vm);
        }

        // ===========================
        // POST: Proveedor/Create
        // ===========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProveedorFormViewModel vm, CancellationToken ct)
        {
            vm.SincronizarEstado();

            if (!ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(vm.Id))
                    vm.Id = await _correlativos.PeekNextProveedorIdAsync(ct);
                return View(vm);
            }

            var ahora = DateTime.Now;
            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync();

            // (Opcional) Unicidad de CORREO si quiere conservarla
            if (!string.IsNullOrWhiteSpace(vm.Correo))
            {
                bool existeCorreo = await _context.PERSONA.AsNoTracking()
                    .AnyAsync(p => !p.ELIMINADO &&
                                   p.PERSONA_CORREO != null &&
                                   p.PERSONA_CORREO.Trim().ToUpper() == vm.Correo.Trim().ToUpper(), ct);
                if (existeCorreo)
                    ModelState.AddModelError(nameof(vm.Correo), "Ya existe una persona con ese correo.");
            }

            if (!ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(vm.Id))
                    vm.Id = await _correlativos.PeekNextProveedorIdAsync(ct);
                return View(vm);
            }

            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            try
            {
                var nuevoId = await _correlativos.NextProveedorIdAsync(ct);

                // PERSONA (sin NIT/CUI)
                var persona = new PERSONA
                {
                    PERSONA_ID = nuevoId,
                    PERSONA_PRIMERNOMBRE = (vm.PrimerNombre ?? "").Trim(),
                    PERSONA_SEGUNDONOMBRE = vm.SegundoNombre?.Trim(),
                    PERSONA_TERCERNOMBRE = vm.TercerNombre?.Trim(),
                    PERSONA_PRIMERAPELLIDO = (vm.PrimerApellido ?? "").Trim(),
                    PERSONA_SEGUNDOAPELLIDO = vm.SegundoApellido?.Trim(),
                    PERSONA_DIRECCION = vm.Direccion?.Trim(),
                    PERSONA_TELEFONOMOVIL = vm.TelefonoMovil?.Trim(),
                    PERSONA_TELEFONOCASA = vm.TelefonoCasa?.Trim(),
                    PERSONA_CORREO = vm.Correo?.Trim(),
                    PERSONA_NIT = null, // ← forzado a null
                    PERSONA_CUI = null, // ← forzado a null
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false,
                    CREADO_POR = usuarioNombre,
                    FECHA_CREACION = ahora
                };

                // PROVEEDOR (guarde EMPRESA en su columna real)
                var proveedor = new PROVEEDOR
                {
                    PROVEEDOR_ID = nuevoId,
                    EMPRESA = string.IsNullOrWhiteSpace(vm.Empresa) ? null : vm.Empresa.Trim(),
                    PROVEEDOR_OBSERVACION = string.IsNullOrWhiteSpace(vm.ProveedorObservacion) ? null : vm.ProveedorObservacion.Trim(),
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false,
                    CREADO_POR = usuarioNombre,
                    FECHA_CREACION = ahora
                };

                _context.Add(persona);
                _context.Add(proveedor);
                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["SavedOk"] = true;
                TempData["SavedName"] = $"{persona.PERSONA_PRIMERNOMBRE} {persona.PERSONA_PRIMERAPELLIDO}".Trim();
                return RedirectToAction(nameof(Create));
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError(string.Empty, $"Error BD: {ex.GetBaseException().Message}");

                if (string.IsNullOrWhiteSpace(vm.Id))
                    vm.Id = await _correlativos.PeekNextProveedorIdAsync(ct);
                return View(vm);
            }
        }




        // ===========================
        // GET: Proveedor/Edit/{id}
        // ===========================
        [HttpGet]
        public async Task<IActionResult> Edit(string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // Traer PROVEEDOR + PERSONA (sin tracking)
            var data = await (from pr in _context.PROVEEDOR.AsNoTracking()
                              join per in _context.PERSONA.AsNoTracking() on pr.PROVEEDOR_ID equals per.PERSONA_ID
                              where pr.PROVEEDOR_ID == id
                              select new { pr, per })
                             .FirstOrDefaultAsync(ct);

            if (data is null) return NotFound();

            // Proyección a VM (sin CUI/NIT)
            var vm = new ProveedorFormViewModel
            {
                // Identificador compartido
                Id = data.pr.PROVEEDOR_ID,

                // PERSONA
                PrimerNombre = data.per.PERSONA_PRIMERNOMBRE,
                SegundoNombre = data.per.PERSONA_SEGUNDONOMBRE,
                TercerNombre = data.per.PERSONA_TERCERNOMBRE,
                PrimerApellido = data.per.PERSONA_PRIMERAPELLIDO,
                SegundoApellido = data.per.PERSONA_SEGUNDOAPELLIDO,
                Direccion = data.per.PERSONA_DIRECCION,
                TelefonoMovil = data.per.PERSONA_TELEFONOMOVIL,
                TelefonoCasa = data.per.PERSONA_TELEFONOCASA,
                Correo = data.per.PERSONA_CORREO,

                // PROVEEDOR
                Empresa = data.pr.EMPRESA,       // ✅ campo real de BD
                ProveedorObservacion = data.pr.PROVEEDOR_OBSERVACION,

                // Estado (checkbox + string)
                ESTADO = data.pr.ESTADO,
                EstadoActivo = string.Equals(data.pr.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),

                // Auditoría mínima (si desea mostrarla en la vista Details/Edit)
                CREADO_POR = data.pr.CREADO_POR,
                FECHA_CREACION = data.pr.FECHA_CREACION
            };

            // Flags PRG (resultado de actualización)
            ViewBag.UpdatedOk = TempData["UpdatedOk"];
            ViewBag.UpdatedName = TempData["UpdatedName"];
            ViewBag.NoChanges = TempData["NoChanges"];

            return View(vm);
        }

        // ===========================
        // POST: Proveedor/Edit/{id}
        // ===========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, ProveedorFormViewModel vm, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id) || !string.Equals(id, vm.Id, StringComparison.Ordinal))
                return NotFound();

            // Normalizar estado desde checkbox
            vm.SincronizarEstado();

            if (!ModelState.IsValid)
                return View(vm);

            // Entidades con tracking
            var proveedor = await _context.PROVEEDOR.FirstOrDefaultAsync(p => p.PROVEEDOR_ID == id, ct);
            var persona = await _context.PERSONA.FirstOrDefaultAsync(p => p.PERSONA_ID == id, ct);
            if (proveedor is null || persona is null) return NotFound();

            // ===== Normalización de datos nuevos desde el VM (sin CUI/NIT) =====
            string nPrimerNombre = (vm.PrimerNombre ?? "").Trim();
            string nSegundoNombre = vm.SegundoNombre?.Trim();
            string nTercerNombre = vm.TercerNombre?.Trim();
            string nPrimerApellido = (vm.PrimerApellido ?? "").Trim();
            string nSegundoApellido = vm.SegundoApellido?.Trim();
            string nDireccion = vm.Direccion?.Trim();
            string nTelefonoMovil = vm.TelefonoMovil?.Trim();
            string nTelefonoCasa = vm.TelefonoCasa?.Trim();
            string nCorreo = vm.Correo?.Trim();

            string nEmpresa = vm.Empresa?.Trim();
            string nObservacion = vm.ProveedorObservacion?.Trim();

            // Estado derivado del checkbox
            var nEstado = vm.EstadoActivo ? "ACTIVO" : "INACTIVO";

            // ===== ¿Hay cambios? (comparación estricta/ordinal) =====
            bool sinCambios =
                string.Equals(persona.PERSONA_PRIMERNOMBRE ?? "", nPrimerNombre, StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_SEGUNDONOMBRE ?? "", nSegundoNombre ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_TERCERNOMBRE ?? "", nTercerNombre ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_PRIMERAPELLIDO ?? "", nPrimerApellido, StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_SEGUNDOAPELLIDO ?? "", nSegundoApellido ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_DIRECCION ?? "", nDireccion ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_TELEFONOMOVIL ?? "", nTelefonoMovil ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_TELEFONOCASA ?? "", nTelefonoCasa ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_CORREO ?? "", nCorreo ?? "", StringComparison.Ordinal) &&

                string.Equals(proveedor.EMPRESA ?? "", nEmpresa ?? "", StringComparison.Ordinal) &&
                string.Equals(proveedor.PROVEEDOR_OBSERVACION ?? "", nObservacion ?? "", StringComparison.Ordinal) &&
                string.Equals(proveedor.ESTADO ?? "", nEstado, StringComparison.Ordinal);

            if (sinCambios)
            {
                TempData["NoChanges"] = true;
                return RedirectToAction(nameof(Edit), new { id });
            }

            // ===== Auditoría =====
            var ahora = DateTime.Now;
            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync(); // o User?.Identity?.Name ?? "SYSTEM"

            // ===== Aplicar cambios a PERSONA =====
            persona.PERSONA_PRIMERNOMBRE = nPrimerNombre;
            persona.PERSONA_SEGUNDONOMBRE = nSegundoNombre;
            persona.PERSONA_TERCERNOMBRE = nTercerNombre;
            persona.PERSONA_PRIMERAPELLIDO = nPrimerApellido;
            persona.PERSONA_SEGUNDOAPELLIDO = nSegundoApellido;
            persona.PERSONA_DIRECCION = nDireccion;
            persona.PERSONA_TELEFONOMOVIL = nTelefonoMovil;
            persona.PERSONA_TELEFONOCASA = nTelefonoCasa;
            persona.PERSONA_CORREO = nCorreo;

            // Mantener PERSONA.ESTADO sincronizado con PROVEEDOR (si así lo define su modelo)
            persona.ESTADO = nEstado;
            persona.MODIFICADO_POR = usuarioNombre;
            persona.FECHA_MODIFICACION = ahora;

            // ===== Aplicar cambios a PROVEEDOR =====
            proveedor.EMPRESA = nEmpresa;       // ✅ guardar en columna real
            proveedor.PROVEEDOR_OBSERVACION = nObservacion;
            proveedor.ESTADO = nEstado;
            proveedor.MODIFICADO_POR = usuarioNombre;
            proveedor.FECHA_MODIFICACION = ahora;

            try
            {
                await _context.SaveChangesAsync(ct);

                TempData["UpdatedOk"] = true;
                TempData["UpdatedName"] = $"{persona.PERSONA_PRIMERNOMBRE} {persona.PERSONA_PRIMERAPELLIDO}".Trim();
                return RedirectToAction(nameof(Edit), new { id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.PROVEEDOR.AnyAsync(p => p.PROVEEDOR_ID == id, ct))
                    return NotFound();
                throw;
            }
        }

















        










        // GET: Proveedores/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pROVEEDOR = await _context.PROVEEDOR
                .Include(p => p.PROVEEDORNavigation)
                .FirstOrDefaultAsync(m => m.PROVEEDOR_ID == id);
            if (pROVEEDOR == null)
            {
                return NotFound();
            }

            return View(pROVEEDOR);
        }

        // POST: Proveedores/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var pROVEEDOR = await _context.PROVEEDOR.FindAsync(id);
            if (pROVEEDOR != null)
            {
                _context.PROVEEDOR.Remove(pROVEEDOR);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PROVEEDORExists(string id)
        {
            return _context.PROVEEDOR.Any(e => e.PROVEEDOR_ID == id);
        }
    }
}
