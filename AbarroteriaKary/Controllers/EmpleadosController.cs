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
    public class EmpleadosController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;
        private readonly IAuditoriaService _auditoria;
        private readonly IReporteExportService _exportSvc;



        public EmpleadosController(KaryDbContext context, ICorrelativoService correlativos, IAuditoriaService auditoria, IReporteExportService exportSvc)
        {
            _context = context;
            _correlativos = correlativos;
            _auditoria = auditoria;
            _exportSvc = exportSvc;
        }

        // GET: Empleados
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

            // 2) Base query con JOIN (evitamos depender de nombres de navegación)
            //    - Ignoramos eliminados lógicos en EMPLEADO y también (opcional) en PERSONA/PUESTO
            var qry = from e in _context.EMPLEADO.AsNoTracking()
                      join per in _context.PERSONA.AsNoTracking() on e.EMPLEADO_ID equals per.PERSONA_ID
                      join pu in _context.PUESTO.AsNoTracking() on e.PUESTO_ID equals pu.PUESTO_ID
                      where !e.ELIMINADO && !per.ELIMINADO && !pu.ELIMINADO
                      select new { e, per, pu };

            // 3) Filtro por estado (preferimos el de EMPLEADO; el de PERSONA debe ir sincronizado)
            if (estadoNorm is "ACTIVO" or "INACTIVO")
                qry = qry.Where(x => x.e.ESTADO == estadoNorm);

            // 4) Búsqueda por texto (ID EMPLEADO, NOMBRE COMPLETO, CUI, NIT, PUESTO)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";

                // Construimos "Nombre completo" server-side para EF.Functions.Like
                var nombreCompleto = qry.Select(x =>
                    new
                    {
                        x,
                        FullName =
                            (x.per.PERSONA_PRIMERNOMBRE ?? "") + " " +
                            (x.per.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                            (x.per.PERSONA_TERCERNOMBRE ?? "") + " " +
                            (x.per.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                            (x.per.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                            (x.per.PERSONA_APELLIDOCASADA ?? "")
                    });

                qry = nombreCompleto
                    .Where(y =>
                        EF.Functions.Like(y.x.e.EMPLEADO_ID, term) ||
                        EF.Functions.Like(y.FullName, term) ||
                        (y.x.per.PERSONA_CUI != null && EF.Functions.Like(y.x.per.PERSONA_CUI, term)) ||
                        (y.x.per.PERSONA_NIT != null && EF.Functions.Like(y.x.per.PERSONA_NIT, term)) ||
                        (y.x.pu.PUESTO_NOMBRE != null && EF.Functions.Like(y.x.pu.PUESTO_NOMBRE, term)) ||
                        EF.Functions.Like(y.x.pu.PUESTO_ID, term)
                    )
                    .Select(y => y.x); // regresar a la forma { e, per, pu }
            }

            // 5) Rango de fechas (inclusivo) por FECHA_CREACION de EMPLEADO
            if (desde.HasValue) qry = qry.Where(x => x.e.FECHA_CREACION >= desde.Value.Date);
            if (hasta.HasValue) qry = qry.Where(x => x.e.FECHA_CREACION < hasta.Value.Date.AddDays(1));

            // 6) Ordenamiento y proyección a ViewModel (ANTES de paginar)
            //    Nota sobre fechas: en DB son DateOnly? para EMPLEADO; para el VM pasamos DateTime? "normalizado".
            var proyectado = qry
                .OrderBy(x => x.e.EMPLEADO_ID)
                .Select(x => new EmpleadoViewModel
                {
                    // Identificador
                    Id = x.e.EMPLEADO_ID,

                    // Persona (para que NombreCompleto del VM funcione)
                    PrimerNombre = x.per.PERSONA_PRIMERNOMBRE,
                    SegundoNombre = x.per.PERSONA_SEGUNDONOMBRE,
                    TercerNombre = x.per.PERSONA_TERCERNOMBRE,
                    PrimerApellido = x.per.PERSONA_PRIMERAPELLIDO,
                    SegundoApellido = x.per.PERSONA_SEGUNDOAPELLIDO,
                    ApellidoCasada = x.per.PERSONA_APELLIDOCASADA,

                    // Identificación/contacto
                    NIT = x.per.PERSONA_NIT,
                    CUI = x.per.PERSONA_CUI,
                    TelefonoMovil = x.per.PERSONA_TELEFONOMOVIL,
                    Correo = x.per.PERSONA_CORREO,
                    Direccion = x.per.PERSONA_DIRECCION,

                    // Empleado
                    Genero = x.e.EMPLEADO_GENERO,
                    PuestoId = x.e.PUESTO_ID,

                    PuestoNombre = x.pu.PUESTO_NOMBRE,


                    // Fechas (DB DateOnly? -> VM DateTime? para mostrar)
                    FechaNacimiento = x.e.EMPLEADO_FECHANACIMIENTO.HasValue
                        ? new DateTime(x.e.EMPLEADO_FECHANACIMIENTO.Value.Year,
                                       x.e.EMPLEADO_FECHANACIMIENTO.Value.Month,
                                       x.e.EMPLEADO_FECHANACIMIENTO.Value.Day)
                        : (DateTime?)null,

                    FechaIngreso = new DateTime(
                        x.e.EMPLEADO_FECHAINGRESO.Year,
                        x.e.EMPLEADO_FECHAINGRESO.Month,
                        x.e.EMPLEADO_FECHAINGRESO.Day
                    ),

                    // Estado (usamos el checkbox del VM y el string persistente)
                    ESTADO = x.e.ESTADO,
                    EstadoActivo = x.e.ESTADO == "ACTIVO",

                    // (Opcional: si luego agrega PuestoNombre al VM, puede proyectarlo aquí)
                    // PuestoNombre = x.pu.PUESTO_NOMBRE
                });

            // 7) Paginación (normaliza pageSize)
            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 10;

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





        //----------------------------Reporete------------------------------------

        [AllowAnonymous]
        [HttpGet]
        public IActionResult PdfFooter()
        {
            // Vista compartida del pie
            return View("~/Views/Shared/Reportes/_PdfFooter.cshtml");
        }

        private IQueryable<EmpleadoListItemViewModel> BuildEmpleadosQuery(
     string estadoNorm, string? q, DateTime? desde, DateTime? hasta)
        {
            var term = string.IsNullOrWhiteSpace(q) ? null : $"%{q.Trim()}%";

            var baseQry =
                from e in _context.EMPLEADO.AsNoTracking().Where(e => !e.ELIMINADO)
                    // LEFT JOIN PERSONA (EMPLEADO_ID ↔ PERSONA_ID)
                join p0 in _context.PERSONA on e.EMPLEADO_ID equals p0.PERSONA_ID into gp
                from p in gp.DefaultIfEmpty()
                    // LEFT JOIN PUESTO (PUESTO_ID)
                join pu0 in _context.PUESTO on e.PUESTO_ID equals pu0.PUESTO_ID into gpu
                from pu in gpu.DefaultIfEmpty()
                select new { e, p, pu };

            if (estadoNorm is "ACTIVO" or "INACTIVO")
                baseQry = baseQry.Where(x => x.e.ESTADO == estadoNorm);

            if (term != null)
            {
                baseQry = baseQry.Where(x =>
                    EF.Functions.Like(x.e.EMPLEADO_ID, term) ||
                    (x.p != null && (
                        EF.Functions.Like(x.p.PERSONA_PRIMERNOMBRE, term) ||
                        EF.Functions.Like(x.p.PERSONA_SEGUNDONOMBRE, term) ||
                        EF.Functions.Like(x.p.PERSONA_PRIMERAPELLIDO, term) ||
                        EF.Functions.Like(x.p.PERSONA_SEGUNDOAPELLIDO, term) ||
                        EF.Functions.Like(x.p.PERSONA_CUI, term)
                    )) ||
                    (x.pu != null && EF.Functions.Like(x.pu.PUESTO_NOMBRE, term))
                );
            }

            // Rango (igual que en otros módulos, si quieres puedes usar FECHA_CREACION del empleado)
            if (desde.HasValue) baseQry = baseQry.Where(x => x.e.FECHA_CREACION >= desde.Value.Date);
            if (hasta.HasValue) baseQry = baseQry.Where(x => x.e.FECHA_CREACION < hasta.Value.Date.AddDays(1));

            return baseQry
                .OrderBy(x => x.e.EMPLEADO_ID)
                .Select(x => new EmpleadoListItemViewModel
                {
                    EmpleadoId = x.e.EMPLEADO_ID,
                    EmpleadoNombre =
                        (x.p == null)
                            ? ""
                            : (
                                (x.p.PERSONA_PRIMERNOMBRE ?? "")
                                + (string.IsNullOrWhiteSpace(x.p.PERSONA_SEGUNDONOMBRE) ? "" : " " + x.p.PERSONA_SEGUNDONOMBRE)
                                + (string.IsNullOrWhiteSpace(x.p.PERSONA_TERCERNOMBRE) ? "" : " " + x.p.PERSONA_TERCERNOMBRE)
                                + " " + (x.p.PERSONA_PRIMERAPELLIDO ?? "")
                                + (string.IsNullOrWhiteSpace(x.p.PERSONA_SEGUNDOAPELLIDO) ? "" : " " + x.p.PERSONA_SEGUNDOAPELLIDO)
                                + (string.IsNullOrWhiteSpace(x.p.PERSONA_APELLIDOCASADA) ? "" : " " + x.p.PERSONA_APELLIDOCASADA)
                              ).Trim(),
                    PuestoNombre = x.pu != null ? x.pu.PUESTO_NOMBRE : "",
                    CUI = (x.p == null) ? "" : (x.p.PERSONA_CUI ?? ""),
                    Telefono = (x.p == null)
                    ? ""
                    : ((x.p.PERSONA_TELEFONOMOVIL ?? x.p.PERSONA_TELEFONOCASA) ?? ""),
                    Genero = x.e.EMPLEADO_GENERO ?? "",
                    FechaIngreso = x.e.EMPLEADO_FECHAINGRESO,   // ← de la tabla EMPLEADO
                    ESTADO = x.e.ESTADO
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
            // 1) Querystring primero
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
            var datos = await BuildEmpleadosQuery(estadoNorm, qParam, desde, hasta).ToListAsync();

            // 5) ViewData tipado
            var pdfViewData = new ViewDataDictionary<IEnumerable<EmpleadoListItemViewModel>>(
                new EmptyModelMetadataProvider(),
                new ModelStateDictionary())
            {
                Model = datos ?? new List<EmpleadoListItemViewModel>()
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
                        var footerUrl = Url.Action("PdfFooter", "Empleados", null, Request.Scheme);

                        var pdf = new ViewAsPdf("~/Views/Empleados/EmpleadosPdf.cshtml")
                        {
                            ViewData = pdfViewData,
                            PageSize = Size.Letter,
                            PageOrientation = Orientation.Landscape, // Portrait-> Vertical / Landscape--> Horizontal
                            PageMargins = new Margins(10, 10, 20, 12),
                            CustomSwitches =
                                "--disable-smart-shrinking --print-media-type " +
                                $"--footer-html \"{footerUrl}\" --footer-spacing 4 " +
                                "--load-error-handling ignore --load-media-error-handling ignore"
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
                        // Si aún no tienes estos métodos, avísame y los agregamos.
                        var xlsx = _exportSvc.GenerarExcelEmpleado(datos);
                        return File(xlsx,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"Reporte_Empleados_{stamp}.xlsx");
                    }

                case "docx":
                case "word":
                    {
                        var docx = _exportSvc.GenerarWordEmpleado(datos);
                        return File(docx,
                            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                            $"Reporte_Empleados_{stamp}.docx");
                    }

                default:
                    return BadRequest("Formato no soportado. Use pdf | xlsx | docx.");
            }
        }





















        // GET: Empleados/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(string id, CancellationToken ct)
        {
            // 1) Validación de parámetro
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // 2) Carga EMPLEADO + PERSONA + PUESTO (solo lectura)
            var data = await (from e in _context.EMPLEADO.AsNoTracking()
                              join per in _context.PERSONA.AsNoTracking() on e.EMPLEADO_ID equals per.PERSONA_ID
                              join pu in _context.PUESTO.AsNoTracking() on e.PUESTO_ID equals pu.PUESTO_ID
                              where e.EMPLEADO_ID == id
                              select new { e, per, pu })
                             .FirstOrDefaultAsync(ct);

            if (data is null) return NotFound();

            // 3) Proyección a ViewModel (lo que usaremos en la vista)
            var vm = new EmpleadoViewModel
            {
                // Identificador compartido
                Id = data.e.EMPLEADO_ID,

                // PERSONA
                PrimerNombre = data.per.PERSONA_PRIMERNOMBRE,
                SegundoNombre = data.per.PERSONA_SEGUNDONOMBRE,
                TercerNombre = data.per.PERSONA_TERCERNOMBRE,
                PrimerApellido = data.per.PERSONA_PRIMERAPELLIDO,
                SegundoApellido = data.per.PERSONA_SEGUNDOAPELLIDO,
                ApellidoCasada = data.per.PERSONA_APELLIDOCASADA,
                NIT = data.per.PERSONA_NIT,
                CUI = data.per.PERSONA_CUI,
                Direccion = data.per.PERSONA_DIRECCION,
                TelefonoMovil = data.per.PERSONA_TELEFONOMOVIL,
                Correo = data.per.PERSONA_CORREO,

                // EMPLEADO
                Genero = data.e.EMPLEADO_GENERO,
                PuestoId = data.e.PUESTO_ID,
                PuestoNombre = data.pu.PUESTO_NOMBRE, // apoyo UI

                // Fechas (DB: DateOnly?/DateOnly -> VM: DateTime?/DateTime)
                FechaNacimiento = data.e.EMPLEADO_FECHANACIMIENTO.HasValue
                                       ? new DateTime(data.e.EMPLEADO_FECHANACIMIENTO.Value.Year,
                                                      data.e.EMPLEADO_FECHANACIMIENTO.Value.Month,
                                                      data.e.EMPLEADO_FECHANACIMIENTO.Value.Day)
                                       : (DateTime?)null,
                FechaIngreso = new DateTime(data.e.EMPLEADO_FECHAINGRESO.Year,
                                                data.e.EMPLEADO_FECHAINGRESO.Month,
                                                data.e.EMPLEADO_FECHAINGRESO.Day),

                // Estado (checkbox + string)
                ESTADO = data.e.ESTADO,
                EstadoActivo = string.Equals(data.e.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase)
            };

            // 4) Auditoría (opcional): disponible en la vista si la quieres mostrar
            ViewBag.Auditoria = new
            {
                CreadoPor = data.e.CREADO_POR,
                FechaCreacion = data.e.FECHA_CREACION,
                ModificadoPor = data.e.MODIFICADO_POR,
                FechaModificacion = data.e.FECHA_MODIFICACION,
                EliminadoPor = data.e.ELIMINADO_POR,
                FechaEliminacion = data.e.FECHA_ELIMINACION
            };

            // 5) Devolver la vista tipada al ViewModel
            return View(vm);
        }





        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            var vm = new EmpleadoViewModel
            {
                // ID “preview” (no reserva todavía)
                Id = await _correlativos.PeekNextPersonaIdAsync(ct),
                FechaIngreso = DateTime.Today,
                ESTADO = "ACTIVO",
            };

            CargarPuestos(vm);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmpleadoViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(vm.Id))
                    vm.Id = await _correlativos.PeekNextPersonaIdAsync
                        (ct);

                CargarPuestos(vm);
                return View(vm);
            }

            var ahora = DateTime.Now;
            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync();

            // Validación CUI único (si viene)
            if (!string.IsNullOrWhiteSpace(vm.CUI))
            {
                bool existeCui = await _context.PERSONA
                    .AsNoTracking()
                    .AnyAsync(p => !p.ELIMINADO
                                   && p.PERSONA_CUI != null
                                   && p.PERSONA_CUI.Trim() == vm.CUI.Trim(), ct);
                if (existeCui)
                    ModelState.AddModelError(nameof(vm.CUI), "Ya existe una persona con ese CUI.");
            }

            // Validación NIT único (si viene)
            if (!string.IsNullOrWhiteSpace(vm.NIT))
            {
                bool existeNit = await _context.PERSONA
                    .AsNoTracking()
                    .AnyAsync(p => !p.ELIMINADO
                                   && p.PERSONA_NIT != null
                                   && p.PERSONA_NIT.Trim().ToUpper() == vm.NIT.Trim().ToUpper(), ct);
                if (existeNit)
                    ModelState.AddModelError(nameof(vm.NIT), "Ya existe una persona con ese NIT.");
            }

            if (!ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(vm.Id))
                    vm.Id = await _correlativos.PeekNextPersonaIdAsync(ct);

                CargarPuestos(vm);
                return View(vm);
            }

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                // ⚠️ Aquí debe ser Next* (reserva definitiva del ID)
                var nuevoId = await _correlativos.NextPersonaIdAsync(ct);
                // Derivar ESTADO del checkbox antes de guardar
                vm.ESTADO = vm.EstadoActivo ? "ACTIVO" : "INACTIVO";

                // PERSONA
                var persona = new PERSONA
                {
                    PERSONA_ID = nuevoId,
                    PERSONA_PRIMERNOMBRE = (vm.PrimerNombre ?? string.Empty).Trim(),
                    PERSONA_SEGUNDONOMBRE = vm.SegundoNombre?.Trim(),
                    PERSONA_TERCERNOMBRE = vm.TercerNombre?.Trim(),
                    PERSONA_PRIMERAPELLIDO = (vm.PrimerApellido ?? string.Empty).Trim(),
                    PERSONA_SEGUNDOAPELLIDO = vm.SegundoApellido?.Trim(),
                    PERSONA_APELLIDOCASADA = vm.ApellidoCasada?.Trim(),
                    PERSONA_NIT = vm.NIT?.Trim().ToUpper(),
                    PERSONA_CUI = vm.CUI?.Trim(),
                    PERSONA_DIRECCION = vm.Direccion?.Trim(),
                    PERSONA_TELEFONOMOVIL = vm.TelefonoMovil?.Trim(),
                    PERSONA_CORREO = vm.Correo?.Trim(),
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false,
                    CREADO_POR = usuarioNombre,
                    FECHA_CREACION = ahora
                };

                // EMPLEADO (conversión DateTime? -> DateOnly?)
                var empleado = new EMPLEADO
                {
                    EMPLEADO_ID = nuevoId, // = PERSONA_ID
                    EMPLEADO_FECHANACIMIENTO = ToDateOnly(vm.FechaNacimiento),           // DateOnly?
                    EMPLEADO_FECHAINGRESO = DateOnly.FromDateTime(vm.FechaIngreso),   // DateOnly (o DateOnly?)
                    EMPLEADO_GENERO = vm.Genero?.Trim(),
                    PUESTO_ID = vm.PuestoId,
                    //ESTADO = vm.Estado?.Trim().ToUpper() == "INACTIVO" ? "INACTIVO" : "ACTIVO",
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false,
                    CREADO_POR = usuarioNombre,
                    FECHA_CREACION = ahora
                };

                _context.Add(persona);
                _context.Add(empleado);
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
                    vm.Id = await _correlativos.PeekNextPersonaIdAsync(ct);

                CargarPuestos(vm);
                return View(vm);
            }
        }

        // ---------- Helpers ----------
        private static DateOnly? ToDateOnly(DateTime? dt)
            => dt.HasValue ? DateOnly.FromDateTime(dt.Value) : (DateOnly?)null;

        private void CargarPuestos(EmpleadoViewModel vm)
        {
            vm.ComboPuestos = _context.PUESTO
                .AsNoTracking()
                .Where(p => !p.ELIMINADO && p.ESTADO == "ACTIVO")
                .OrderBy(p => p.PUESTO_NOMBRE)
                .Select(p => new SelectListItem
                {
                    Value = p.PUESTO_ID,
                    Text = p.PUESTO_NOMBRE
                })
                .ToList();
        }



        [HttpGet]
        public async Task<IActionResult> Edit(string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // Traer EMPLEADO + PERSONA + PUESTO (sin tracking)
            var data = await (from e in _context.EMPLEADO.AsNoTracking()
                              join per in _context.PERSONA.AsNoTracking() on e.EMPLEADO_ID equals per.PERSONA_ID
                              join pu in _context.PUESTO.AsNoTracking() on e.PUESTO_ID equals pu.PUESTO_ID
                              where e.EMPLEADO_ID == id
                              select new { e, per, pu })
                             .FirstOrDefaultAsync(ct);

            if (data is null) return NotFound();

            // Proyección a VM (ojo: DateOnly? -> DateTime?)
            var vm = new EmpleadoViewModel
            {
                // Identificador compartido
                Id = data.e.EMPLEADO_ID,

                // PERSONA
                PrimerNombre = data.per.PERSONA_PRIMERNOMBRE,
                SegundoNombre = data.per.PERSONA_SEGUNDONOMBRE,
                TercerNombre = data.per.PERSONA_TERCERNOMBRE,
                PrimerApellido = data.per.PERSONA_PRIMERAPELLIDO,
                SegundoApellido = data.per.PERSONA_SEGUNDOAPELLIDO,
                ApellidoCasada = data.per.PERSONA_APELLIDOCASADA,
                NIT = data.per.PERSONA_NIT,
                CUI = data.per.PERSONA_CUI,
                Direccion = data.per.PERSONA_DIRECCION,
                TelefonoMovil = data.per.PERSONA_TELEFONOMOVIL,
                Correo = data.per.PERSONA_CORREO,

                // EMPLEADO
                Genero = data.e.EMPLEADO_GENERO,
                PuestoId = data.e.PUESTO_ID,
                PuestoNombre = data.pu.PUESTO_NOMBRE, // apoyo UI

                FechaNacimiento = data.e.EMPLEADO_FECHANACIMIENTO.HasValue
                    ? new DateTime(data.e.EMPLEADO_FECHANACIMIENTO.Value.Year,
                                   data.e.EMPLEADO_FECHANACIMIENTO.Value.Month,
                                   data.e.EMPLEADO_FECHANACIMIENTO.Value.Day)
                    : (DateTime?)null,

                FechaIngreso = new DateTime(
                    data.e.EMPLEADO_FECHAINGRESO.Year,
                    data.e.EMPLEADO_FECHAINGRESO.Month,
                    data.e.EMPLEADO_FECHAINGRESO.Day
                ),

                // Estado (checkbox + string)
                ESTADO = data.e.ESTADO,
                EstadoActivo = string.Equals(data.e.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),

                // Auditoría (opcional para mostrar en card)
                Auditoria = new AuditoriaViewModel
                {
                    CreadoPor = data.e.CREADO_POR,
                    FechaCreacion = data.e.FECHA_CREACION,
                    ModificadoPor = data.e.MODIFICADO_POR,
                    FechaModificacion = data.e.FECHA_MODIFICACION,
                    Eliminado = data.e.ELIMINADO,
                    EliminadoPor = data.e.ELIMINADO_POR,
                    FechaEliminacion = data.e.FECHA_ELIMINACION
                }
            };

            // Combos
            CargarPuestos(vm);

            // Flags de resultado de actualización (PRG)
            ViewBag.UpdatedOk = TempData["UpdatedOk"];
            ViewBag.UpdatedName = TempData["UpdatedName"];
            ViewBag.NoChanges = TempData["NoChanges"];

            return View(vm);
        }









        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, EmpleadoViewModel vm, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id) || !string.Equals(id, vm.Id, StringComparison.Ordinal))
                return NotFound();

            if (!ModelState.IsValid)
            {
                CargarPuestos(vm);
                return View(vm);
            }

            // Entidades con tracking
            var empleado = await _context.EMPLEADO.FirstOrDefaultAsync(e => e.EMPLEADO_ID == id, ct);
            var persona = await _context.PERSONA.FirstOrDefaultAsync(p => p.PERSONA_ID == id, ct);
            if (empleado is null || persona is null) return NotFound();

            // ===== Normalización de datos nuevos desde el VM =====
            string nPrimerNombre = (vm.PrimerNombre ?? "").Trim();
            string nSegundoNombre = vm.SegundoNombre?.Trim();
            string nTercerNombre = vm.TercerNombre?.Trim();
            string nPrimerApellido = (vm.PrimerApellido ?? "").Trim();
            string nSegundoApellido = vm.SegundoApellido?.Trim();
            string nApellidoCasada = vm.ApellidoCasada?.Trim();
            string nNit = vm.NIT?.Trim().ToUpperInvariant();
            string nCui = vm.CUI?.Trim();
            string nDireccion = vm.Direccion?.Trim();
            string nTelefono = vm.TelefonoMovil?.Trim();
            string nCorreo = vm.Correo?.Trim();

            string nGenero = vm.Genero?.Trim();
            string nPuestoId = vm.PuestoId; // requerido en VM
            var nFechaNac = vm.FechaNacimiento.HasValue ? DateOnly.FromDateTime(vm.FechaNacimiento.Value) : (DateOnly?)null;
            var nFechaIngreso = DateOnly.FromDateTime(vm.FechaIngreso);

            // Estado derivado del checkbox
            var nEstado = vm.EstadoActivo ? "ACTIVO" : "INACTIVO";

            // ===== ¿Hay cambios? (comparación estricta/ordinal) =====
            bool sinCambios =
                string.Equals(persona.PERSONA_PRIMERNOMBRE ?? "", nPrimerNombre, StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_SEGUNDONOMBRE ?? "", nSegundoNombre ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_TERCERNOMBRE ?? "", nTercerNombre ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_PRIMERAPELLIDO ?? "", nPrimerApellido, StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_SEGUNDOAPELLIDO ?? "", nSegundoApellido ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_APELLIDOCASADA ?? "", nApellidoCasada ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_NIT ?? "", nNit ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_CUI ?? "", nCui ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_DIRECCION ?? "", nDireccion ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_TELEFONOMOVIL ?? "", nTelefono ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_CORREO ?? "", nCorreo ?? "", StringComparison.Ordinal) &&

                string.Equals(empleado.EMPLEADO_GENERO ?? "", nGenero ?? "", StringComparison.Ordinal) &&
                string.Equals(empleado.PUESTO_ID ?? "", nPuestoId ?? "", StringComparison.Ordinal) &&
                Nullable.Equals(empleado.EMPLEADO_FECHANACIMIENTO, nFechaNac) &&
                empleado.EMPLEADO_FECHAINGRESO.Equals(nFechaIngreso) &&
                string.Equals(empleado.ESTADO ?? "", nEstado, StringComparison.Ordinal);

            if (sinCambios)
            {
                TempData["NoChanges"] = true;
                return RedirectToAction(nameof(Edit), new { id });
            }

            // ===== Auditoría =====
            var ahora = DateTime.Now;
            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync();

            // ===== Aplicar cambios a PERSONA =====
            persona.PERSONA_PRIMERNOMBRE = nPrimerNombre;
            persona.PERSONA_SEGUNDONOMBRE = nSegundoNombre;
            persona.PERSONA_TERCERNOMBRE = nTercerNombre;
            persona.PERSONA_PRIMERAPELLIDO = nPrimerApellido;
            persona.PERSONA_SEGUNDOAPELLIDO = nSegundoApellido;
            persona.PERSONA_APELLIDOCASADA = nApellidoCasada;
            persona.PERSONA_NIT = nNit;
            persona.PERSONA_CUI = nCui;
            persona.PERSONA_DIRECCION = nDireccion;
            persona.PERSONA_TELEFONOMOVIL = nTelefono;
            persona.PERSONA_CORREO = nCorreo;
            persona.MODIFICADO_POR = usuarioNombre;
            persona.FECHA_MODIFICACION = ahora;

            // Mantener ESTADO de PERSONA sincronizado con EMPLEADO
            // (si PERSONA se usa para otros módulos y quiere independencia, puede omitir esta línea)
            persona.ESTADO = nEstado;

            // ===== Aplicar cambios a EMPLEADO =====
            empleado.EMPLEADO_GENERO = nGenero;
            empleado.PUESTO_ID = nPuestoId;
            empleado.EMPLEADO_FECHANACIMIENTO = nFechaNac;
            empleado.EMPLEADO_FECHAINGRESO = nFechaIngreso;

            var estadoOriginalActivo = string.Equals(empleado.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase);
            var estadoNuevoActivo = vm.EstadoActivo;

            if (estadoOriginalActivo != estadoNuevoActivo)
            {
                if (!estadoNuevoActivo)
                {
                    // → DESACTIVAR (NO tocar ELIMINADO: debe seguir listando)
                    empleado.ESTADO = "INACTIVO";
                    // Opcional: use estos campos como “rastro” de desactivación
                    empleado.ELIMINADO_POR = usuarioNombre;
                    empleado.FECHA_ELIMINACION = ahora;
                    empleado.ELIMINADO = false;
                }
                else
                {
                    // → REACTIVAR
                    empleado.ESTADO = "ACTIVO";
                    // Opcional: limpiar rastro o conservarlo (aquí lo actualizamos también)
                    empleado.ELIMINADO_POR = usuarioNombre;
                    empleado.FECHA_ELIMINACION = ahora;
                    empleado.ELIMINADO = false;
                }
            }
            else
            {
                // Sin cambio de estado, sincronizamos explícitamente
                empleado.ESTADO = nEstado;
            }

            empleado.MODIFICADO_POR = usuarioNombre;
            empleado.FECHA_MODIFICACION = ahora;

            try
            {
                await _context.SaveChangesAsync(ct);

                TempData["UpdatedOk"] = true;
                TempData["UpdatedName"] = $"{persona.PERSONA_PRIMERNOMBRE} {persona.PERSONA_PRIMERAPELLIDO}".Trim();
                return RedirectToAction(nameof(Edit), new { id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.EMPLEADO.AnyAsync(e => e.EMPLEADO_ID == id, ct))
                    return NotFound();
                throw;
            }
        }








        // GET: Empleados/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var eMPLEADO = await _context.EMPLEADO
                .Include(e => e.EMPLEADONavigation)
                .Include(e => e.PUESTO)
                .FirstOrDefaultAsync(m => m.EMPLEADO_ID == id);
            if (eMPLEADO == null)
            {
                return NotFound();
            }

            return View(eMPLEADO);
        }

        // POST: Empleados/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var eMPLEADO = await _context.EMPLEADO.FindAsync(id);
            if (eMPLEADO != null)
            {
                _context.EMPLEADO.Remove(eMPLEADO);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool EMPLEADOExists(string id)
        {
            return _context.EMPLEADO.Any(e => e.EMPLEADO_ID == id);
        }
    }
}
