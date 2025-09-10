using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.ModelsPartial.Paginacion;           // PaginadoViewModel<T>
using AbarroteriaKary.Services.Auditoria;
using AbarroteriaKary.Services.Correlativos;
using AbarroteriaKary.Services.Extensions;                // ToPagedAsync extension
using AbarroteriaKary.Services.Reportes;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ModelBinding;



namespace AbarroteriaKary.Controllers
{
    public class AreasController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;
        private readonly IReporteExportService _exportSvc;
        private readonly IAuditoriaService _auditoria;


        public AreasController(KaryDbContext context, ICorrelativoService correlativos, IReporteExportService exportSvc, IAuditoriaService auditoria)
        {
            _context = context;
            _correlativos = correlativos;
            _exportSvc = exportSvc;
            _auditoria = auditoria;


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
                    ESTADO = a.ESTADO,
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
            ViewBag.Usuario = await _auditoria.GetUsuarioNombreAsync(); // o tu método
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



        //----------------------------Reporete



            [AllowAnonymous] // si tu sitio requiere login, esto evita bloqueos del motor al pedir el footer
            [HttpGet]
            public IActionResult PdfFooter()
            {
                // Vista compartida del pie
                return View("~/Views/Shared/_PdfFooter.cshtml");
            }

    private IQueryable<AreasViewModel> BuildAreasQuery(string estadoNorm, string? q, DateTime? desde, DateTime? hasta)
        {
            var qry = _context.AREA
                .AsNoTracking()
                .Where(a => !a.ELIMINADO);

            if (estadoNorm is "ACTIVO" or "INACTIVO")
                qry = qry.Where(a => a.ESTADO == estadoNorm);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                qry = qry.Where(a =>
                    EF.Functions.Like(a.AREA_ID, term) ||
                    EF.Functions.Like(a.AREA_NOMBRE, term));
            }

            if (desde.HasValue) qry = qry.Where(a => a.FECHA_CREACION >= desde.Value.Date);
            if (hasta.HasValue) qry = qry.Where(a => a.FECHA_CREACION < hasta.Value.Date.AddDays(1));

            return qry
                .OrderBy(a => a.AREA_ID)
                .Select(a => new AreasViewModel
                {
                    areaId = a.AREA_ID,
                    areaNombre = a.AREA_NOMBRE,
                    areaDescripcion = a.AREA_DESCRIPCION,
                    ESTADO = a.ESTADO,
                    FechaCreacion = a.FECHA_CREACION
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
            var datos = await BuildAreasQuery(estadoNorm, qParam, desde, hasta).ToListAsync();

            // 5) ViewData tipado + modelo dentro de ViewData (evita Model=null en Rotativa)
            var pdfViewData = new ViewDataDictionary<IEnumerable<AreasViewModel>>(
                new EmptyModelMetadataProvider(),
                new ModelStateDictionary())
            {
                Model = datos ?? new List<AreasViewModel>()
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
                        var footerUrl = Url.Action("PdfFooter", "Areas", null, Request.Scheme);

                        var pdf = new ViewAsPdf("~/Views/Areas/AreasPdf.cshtml")
                        {
                            ViewData = pdfViewData,
                            PageSize = Size.Letter,
                            PageOrientation = Orientation.Portrait,
                            PageMargins = new Margins(10, 10, 20, 12),
                            CustomSwitches = "--disable-smart-shrinking --print-media-type " +
                                             $"--footer-html \"{footerUrl}\" --footer-spacing 4"
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
                        var xlsx = _exportSvc.GenerarExcelAreas(datos);
                        return File(
                            xlsx,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"Reporte_Areas_{stamp}.xlsx");
                    }

                case "docx":
                case "word":
                    {
                        var docx = _exportSvc.GenerarWordAreas(datos);
                        return File(
                            docx,
                            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                            $"Reporte_Areas_{stamp}.docx");
                    }

                default:
                    return BadRequest("Formato no soportado. Use pdf | xlsx | docx.");
            }
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
                ESTADO = a.ESTADO,                            // para badges en listado
                EstadoActivo = string.Equals(a.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),
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
                ESTADO = "ACTIVO",
                EstadoActivo = true,
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

            var ahora = DateTime.Now;
            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync(); // ★ AQUÍ LA INTEGRACIÓN

            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var nuevoId = await _correlativos.NextAreaIdAsync();

                var entidad = new AREA
                {
                    AREA_ID = nuevoId,
                    AREA_NOMBRE = (vm.areaNombre ?? string.Empty).Trim(),
                    AREA_DESCRIPCION = vm.areaDescripcion?.Trim(),
                    ESTADO = vm.EstadoActivo ? "ACTIVO" : "INACTIVO",
                    ELIMINADO = false,
                    CREADO_POR = usuarioNombre,
                    FECHA_CREACION = ahora
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
                ESTADO = entidad.ESTADO,
                EstadoActivo = string.Equals(entidad.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),
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
            var nuevoEstado = vm.EstadoActivo ? "ACTIVO" : "INACTIVO";

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

            var ahora = DateTime.Now;
            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync(); // ★ AQUÍ LA INTEGRACIÓN


            // aplicar cambios + auditoría
            entidad.AREA_NOMBRE = nuevoNombre;
            entidad.AREA_DESCRIPCION = nuevaDesc;
            entidad.ESTADO = nuevoEstado;
            entidad.MODIFICADO_POR = usuarioNombre;
            entidad.FECHA_MODIFICACION = ahora;

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
