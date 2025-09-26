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
    public class PuestoController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;
        private readonly IAuditoriaService _auditoria;
        private readonly IReporteExportService _exportSvc;


        public PuestoController(KaryDbContext context, ICorrelativoService correlativos, IAuditoriaService auditoria, IReporteExportService exportSvc)
        {
            _context = context;
            _correlativos = correlativos;
            _auditoria = auditoria;
            _exportSvc = exportSvc;



        }

        [HttpGet]
        public async Task<IActionResult> Index(  string? estado, string? q = null, string? fDesde = null, string? fHasta = null, int page = 1, int pageSize = 10)
        {
            // 0) Normalización de estado
            var estadoNorm = (estado ?? "ACTIVO").Trim().ToUpperInvariant();
            if (estadoNorm is not ("ACTIVO" or "INACTIVO" or "TODOS"))
                estadoNorm = "ACTIVO";

            // 1) Fechas (acepta dd/MM/yyyy o yyyy-MM-dd)
            DateTime? desde = ParseDate(fDesde);
            DateTime? hasta = ParseDate(fHasta);

            // 2) Base query (ignora eliminados)
            var qry = _context.PUESTO
                .AsNoTracking()
                .Where(p => !p.ELIMINADO);

            // 3) Filtro por estado
            if (estadoNorm is "ACTIVO" or "INACTIVO")
                qry = qry.Where(p => p.ESTADO == estadoNorm);

            // 4) Búsqueda por texto (ID, Nombre, Descripción, Área)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                qry = qry.Where(p =>
                    EF.Functions.Like(p.PUESTO_ID, term) ||
                    EF.Functions.Like(p.PUESTO_NOMBRE, term) ||
                    (p.PUESTO_DESCRIPCION != null && EF.Functions.Like(p.PUESTO_DESCRIPCION, term)) ||
                    EF.Functions.Like(p.AREA_ID, term) ||
                    EF.Functions.Like(p.AREA.AREA_NOMBRE, term) // navegación en proyección
                );
            }

            // 5) Rango de fechas (inclusivo)
            if (desde.HasValue) qry = qry.Where(p => p.FECHA_CREACION >= desde.Value.Date);
            if (hasta.HasValue) qry = qry.Where(p => p.FECHA_CREACION < hasta.Value.Date.AddDays(1));

            // 6) Ordenamiento + proyección a ViewModel (ANTES de paginar)
            var proyectado = qry
                .OrderBy(p => p.PUESTO_ID)
                .Select(p => new PuestoViewModel
                {
                    PUESTO_ID = p.PUESTO_ID,
                    PUESTO_NOMBRE = p.PUESTO_NOMBRE,
                    PUESTO_DESCRIPCION = p.PUESTO_DESCRIPCION,
                    AREA_ID = p.AREA_ID,
                    AREA_NOMBRE = p.AREA.AREA_NOMBRE, // nombre del Área
                    ESTADO = p.ESTADO,
                    FECHA_CREACION = p.FECHA_CREACION
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





        [AllowAnonymous]
        [HttpGet]
        public IActionResult PdfFooter()
        {
            // Vista compartida del pie
            return View("~/Views/Shared/Reportes/_PdfFooter.cshtml");
        }

        private IQueryable<PuestoViewModel> BuildPuestoQuery(string estadoNorm, string? q, DateTime? desde, DateTime? hasta)
        {
            var qry = _context.PUESTO
                .AsNoTracking()
                .Where(a => !a.ELIMINADO);

            if (estadoNorm is "ACTIVO" or "INACTIVO")
                qry = qry.Where(a => a.ESTADO == estadoNorm);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                qry = qry.Where(a =>
                    EF.Functions.Like(a.PUESTO_ID, term) ||
                    EF.Functions.Like(a.PUESTO_NOMBRE, term));
            }

            if (desde.HasValue) qry = qry.Where(a => a.FECHA_CREACION >= desde.Value.Date);
            if (hasta.HasValue) qry = qry.Where(a => a.FECHA_CREACION < hasta.Value.Date.AddDays(1));

            return qry
                .OrderBy(a => a.PUESTO_ID)
                .Select(a => new PuestoViewModel
                {
                    PUESTO_ID = a.PUESTO_ID,
                    PUESTO_NOMBRE = a.PUESTO_NOMBRE,
                    PUESTO_DESCRIPCION = a.PUESTO_DESCRIPCION,
                    AREA_NOMBRE = a.AREA.AREA_NOMBRE,
                    ESTADO = a.ESTADO,
                    FECHA_CREACION = a.FECHA_CREACION
                });
        }

        // GET: /Areas/Exportar?formato=pdf|xlsx|docx&estado=ACTIVO&q=&fDesde=&fHasta=

        private string GetUsuarioActual()
        {
            // 1) Claims (cookie de autenticación)
            if (User?.Identity?.IsAuthenticated == true)
            {
                if (!string.IsNullOrWhiteSpace(User.Identity!.Name))
                    return User.Identity.Name!;
                string[] keys = {
            ClaimTypes.Name, ClaimTypes.GivenName, ClaimTypes.Email,
            "name","usuario","user","UserName","UsuarioNombre"
        };
                foreach (var k in keys)
                {
                    var c = User.FindFirst(k);
                    if (c != null && !string.IsNullOrWhiteSpace(c.Value))
                        return c.Value;
                }
            }

            // 2) Session (si la llenas en tu login)
            var ses = HttpContext?.Session?.GetString("UsuarioNombre")
                   ?? HttpContext?.Session?.GetString("UserName");
            if (!string.IsNullOrWhiteSpace(ses)) return ses!;

            // 3) Fallback final
            return "Admin";
        }


        [HttpGet]
        public async Task<IActionResult> Exportar(
      string formato = "pdf",
      string? estado = "ACTIVO",
      string? q = null,
      string? fDesde = null,
      string? fHasta = null,
      string? by = null)
        {
            // 1) Lee PRIMERO del querystring (lo que manda el dropdown)
            var qs = Request?.Query;
            string estadoParam = !string.IsNullOrWhiteSpace(qs?["estado"]) ? qs!["estado"].ToString() : estado;
            string qParam = !string.IsNullOrWhiteSpace(qs?["q"]) ? qs!["q"].ToString() : q;
            string fDesdeParam = !string.IsNullOrWhiteSpace(qs?["fDesde"]) ? qs!["fDesde"].ToString() : fDesde;
            string fHastaParam = !string.IsNullOrWhiteSpace(qs?["fHasta"]) ? qs!["fHasta"].ToString() : fHasta;
            string byParam = !string.IsNullOrWhiteSpace(qs?["by"]) ? qs!["by"].ToString() : by;

            // 2) Normaliza estado
            var estadoNorm = (estadoParam ?? "ACTIVO").Trim().ToUpperInvariant();
            if (estadoNorm is not ("ACTIVO" or "INACTIVO" or "TODOS")) estadoNorm = "ACTIVO";

            // 3) Fechas
            DateTime? desde = ParseDate(fDesdeParam);
            DateTime? hasta = ParseDate(fHastaParam);

            // 4) Data
            var datos = await BuildPuestoQuery(estadoNorm, qParam, desde, hasta).ToListAsync();

            // 5) ViewData tipado + modelo dentro de ViewData (evita Model=null en Rotativa)
            var pdfViewData = new ViewDataDictionary<IEnumerable<PuestoViewModel>>(
                new EmptyModelMetadataProvider(),
                new ModelStateDictionary())
            {
                Model = datos ?? new List<PuestoViewModel>()
            };
            pdfViewData["Filtro_Estado"] = estadoNorm;
            pdfViewData["Filtro_Q"] = qParam;
            pdfViewData["Filtro_Desde"] = desde?.ToString("dd/MM/yyyy");
            pdfViewData["Filtro_Hasta"] = hasta?.ToString("dd/MM/yyyy");

            var usuario = GetUsuarioActual();
            if ((string.IsNullOrWhiteSpace(usuario) || usuario == "Admin") && !string.IsNullOrWhiteSpace(byParam))
                usuario = byParam;
            pdfViewData["Usuario"] = usuario;

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");

            switch ((formato ?? "").ToLowerInvariant())
            {
                case "pdf":
                    {
                        var footerUrl = Url.Action("PdfFooter", "Puesto", null, Request.Scheme);

                        var pdf = new ViewAsPdf("~/Views/Puesto/PuestosPdf.cshtml")
                        {
                            ViewData = pdfViewData,
                            PageSize = Size.Letter,
                            PageOrientation = Orientation.Portrait,
                            PageMargins = new Margins(10, 10, 20, 12),
                            CustomSwitches =
                                "--disable-smart-shrinking --print-media-type " +
                                $"--footer-html \"{footerUrl}\" --footer-spacing 4 " +
                                "--load-error-handling ignore --load-media-error-handling ignore"
                                                // ↑ estos dos ayudan a que ignore assets que fallen al cargar
                        };

                        var bytes = await pdf.BuildFile(ControllerContext);
                        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                        Response.Headers["Pragma"] = "no-cache";
                        Response.Headers["Expires"] = "0";
                        return File(bytes, "application/pdf");
                    }

                case "xlsx":
                case "excel":
                    {
                        var xlsx = _exportSvc.GenerarExcelPuestos(datos);
                        return File(
                            xlsx,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"Reporte_Areas_{stamp}.xlsx");
                    }

                case "docx":
                case "word":
                    {
                        var docx = _exportSvc.GenerarWordPuestos(datos);
                        return File(
                            docx,
                            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                            $"Reporte_Areas_{stamp}.docx");
                    }

                default:
                    return BadRequest("Formato no soportado. Use pdf | xlsx | docx.");
            }


        }




        public async Task<IActionResult> Details(string id)
        {
            // 1) Validación de parámetro
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // 2) Carga de entidad con el Área (solo lectura)
            var entidad = await _context.PUESTO
                .AsNoTracking()
                .Include(p => p.AREA)
                .FirstOrDefaultAsync(p => p.PUESTO_ID == id);

            if (entidad is null) return NotFound();

            // 3) Proyección a ViewModel (lo que usaremos en la vista)
            var vm = new PuestoViewModel
            {
                PUESTO_ID = entidad.PUESTO_ID,
                PUESTO_NOMBRE = entidad.PUESTO_NOMBRE,
                PUESTO_DESCRIPCION = entidad.PUESTO_DESCRIPCION,
                AREA_ID = entidad.AREA_ID,
                AREA_NOMBRE = entidad.AREA?.AREA_NOMBRE, // ← nombre del Área para mostrar
                ESTADO = entidad.ESTADO,
                EstadoActivo = string.Equals(entidad.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),
                FECHA_CREACION = entidad.FECHA_CREACION
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






        // GET: Create

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new PuestoViewModel
            {
                PUESTO_ID = await _correlativos.PeekNextPuestoIdAsync(), // solo para mostrar
                ESTADO = "ACTIVO",
                EstadoActivo = true,
                FECHA_CREACION = DateTime.Now
            };

            CargarAreas(vm); // combo de áreas
                             // Flags para modal de éxito (opcional, mismo patrón que Áreas)
            ViewBag.SavedOk = TempData["SavedOk"];
            ViewBag.SavedName = TempData["SavedName"];

            return View(vm);
        }

        // POST: /Puesto/Create
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PuestoViewModel vm)
        {
            // Validación de modelo
            if (!ModelState.IsValid)
            {
                // Si hubo error y se perdió el ID “preview”, lo reponemos
                if (string.IsNullOrWhiteSpace(vm.PUESTO_ID))
                    vm.PUESTO_ID = await _correlativos.PeekNextPuestoIdAsync();

                CargarAreas(vm);
                return View(vm);
            }
            // ====== Usuario y fecha para auditoría (★ servicio) ======
            var ahora = DateTime.Now;
            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync(); // ★ AQUÍ LA INTEGRACIÓN

            // (Opcional) Validación de duplicado por Nombre+Área (solo si lo desea ya)
            bool existe = await _context.PUESTO
                .AnyAsync(p => !p.ELIMINADO
                    && p.AREA_ID == vm.AREA_ID
                    && p.PUESTO_NOMBRE.Trim() == (vm.PUESTO_NOMBRE ?? "").Trim());
            if (existe)
            {
                ModelState.AddModelError(nameof(vm.PUESTO_NOMBRE),
                    "Ya existe un puesto con ese nombre en el área seleccionada.");
                CargarAreas(vm);
                if (string.IsNullOrWhiteSpace(vm.PUESTO_ID))
                    vm.PUESTO_ID = await _correlativos.PeekNextPuestoIdAsync();
                return View(vm);
            }

            // Auditoría: tome usuario autenticado o “Sistema”
            //var userName = User?.Identity?.Name ?? "Sistema";

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // ID definitivo y único (atómico)
                var nuevoId = await _correlativos.NextPuestoIdAsync();

                // Mapear VM -> Entidad EF
                var entidad = new PUESTO
                {
                    PUESTO_ID = nuevoId,
                    PUESTO_NOMBRE = (vm.PUESTO_NOMBRE ?? string.Empty).Trim(),
                    PUESTO_DESCRIPCION = vm.PUESTO_DESCRIPCION?.Trim(),
                    AREA_ID = vm.AREA_ID,
                    ESTADO = vm.EstadoActivo ? "ACTIVO" : "INACTIVO",
                    ELIMINADO = false,

                    // Auditoría (alta)
                    CREADO_POR = usuarioNombre,
                    FECHA_CREACION = ahora
                };

                _context.Add(entidad);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                // Modal de éxito (mismo patrón que Áreas)
                TempData["SavedOk"] = true;
                TempData["SavedName"] = entidad.PUESTO_NOMBRE;

                // PRG: vuelve a GET Create (para permitir alta consecutiva)
                return RedirectToAction(nameof(Create));
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError(string.Empty, $"Error BD: {ex.GetBaseException().Message}");

                // Reponer ID de “preview” para no dejar el campo en blanco
                if (string.IsNullOrWhiteSpace(vm.PUESTO_ID))
                    vm.PUESTO_ID = await _correlativos.PeekNextPuestoIdAsync();

                CargarAreas(vm);
                return View(vm);
            }
        }

        // Helper: carga combo de Áreas activas y no eliminadas
        private void CargarAreas(PuestoViewModel vm)
        {
            vm.AreasOpciones = _context.AREA
                .AsNoTracking()
                .Where(a => !a.ELIMINADO && a.ESTADO == "ACTIVO")
                .OrderBy(a => a.AREA_NOMBRE)
                .Select(a => new SelectListItem
                {
                    Value = a.AREA_ID,
                    Text = a.AREA_NOMBRE
                })
                .ToList();
        }



        // GET: Puesto/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var entidad = await _context.PUESTO
                .AsNoTracking()
                .Include(p => p.AREA)
                .FirstOrDefaultAsync(p => p.PUESTO_ID == id);

            if (entidad == null) return NotFound();

            var vm = new PuestoViewModel
            {
                PUESTO_ID = entidad.PUESTO_ID,
                PUESTO_NOMBRE = entidad.PUESTO_NOMBRE,
                PUESTO_DESCRIPCION = entidad.PUESTO_DESCRIPCION,
                AREA_ID = entidad.AREA_ID,
                AREA_NOMBRE = entidad.AREA?.AREA_NOMBRE,
                ESTADO = entidad.ESTADO,
                EstadoActivo = string.Equals(entidad.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),
                FECHA_CREACION = entidad.FECHA_CREACION
            };

            CargarAreas(vm);
            ViewBag.UpdatedOk = TempData["UpdatedOk"];
            ViewBag.UpdatedName = TempData["UpdatedName"];
            ViewBag.NoChanges = TempData["NoChanges"];

            return View(vm);
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, PuestoViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(id) || !string.Equals(id, vm.PUESTO_ID, StringComparison.Ordinal))
                return NotFound();

            if (!ModelState.IsValid)
            {
                CargarAreas(vm);
                return View(vm);
            }

            var entidad = await _context.PUESTO.FirstOrDefaultAsync(p => p.PUESTO_ID == id);
            if (entidad == null) return NotFound();

            // ====== Normalización de datos nuevos desde el VM ======
            var nuevoNombre = (vm.PUESTO_NOMBRE ?? string.Empty).Trim();
            var nuevaDesc = vm.PUESTO_DESCRIPCION?.Trim();
            var nuevaAreaId = vm.AREA_ID;
            var nuevoEstado = vm.EstadoActivo ? "ACTIVO" : "INACTIVO";

            // ====== ¿Hay cambios? (nombre, desc, área, estado) ======
            var sinCambios =
                string.Equals(entidad.PUESTO_NOMBRE ?? "", nuevoNombre, StringComparison.Ordinal) &&
                string.Equals(entidad.PUESTO_DESCRIPCION ?? "", nuevaDesc ?? "", StringComparison.Ordinal) &&
                string.Equals(entidad.AREA_ID ?? "", nuevaAreaId ?? "", StringComparison.Ordinal) &&
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
            entidad.PUESTO_NOMBRE = nuevoNombre;
            entidad.PUESTO_DESCRIPCION = nuevaDesc;
            entidad.AREA_ID = nuevaAreaId;

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
                TempData["UpdatedName"] = entidad.PUESTO_NOMBRE;
                return RedirectToAction(nameof(Edit), new { id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.PUESTO.AnyAsync(e => e.PUESTO_ID == id)) return NotFound();
                throw;
            }
        }





        // GET: Puesto/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pUESTO = await _context.PUESTO
                .Include(p => p.AREA)
                .FirstOrDefaultAsync(m => m.PUESTO_ID == id);
            if (pUESTO == null)
            {
                return NotFound();
            }

            return View(pUESTO);
        }

        // POST: Puesto/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var pUESTO = await _context.PUESTO.FindAsync(id);
            if (pUESTO != null)
            {
                _context.PUESTO.Remove(pUESTO);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PUESTOExists(string id)
        {
            return _context.PUESTO.Any(e => e.PUESTO_ID == id);
        }
    }
}
