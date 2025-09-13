using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.ModelsPartial.Paginacion;           // PaginadoViewModel<T>
using AbarroteriaKary.Services.Auditoria;
using AbarroteriaKary.Services.Correlativos;
using AbarroteriaKary.Services.Extensions;                // ToPagedAsync extension
using AbarroteriaKary.Services.Reportes;
using AbarroteriaKary.Services.Security;
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
using System.Security.Cryptography;
using System.Threading.Tasks;


namespace AbarroteriaKary.Controllers
{
    public class UsuariosController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;
        private readonly IAuditoriaService _auditoria;
        private readonly IReporteExportService _exportSvc;

        public UsuariosController(KaryDbContext context, ICorrelativoService correlativos, IAuditoriaService auditoria, IReporteExportService exportSvc)
        {
            _context = context;
            _correlativos = correlativos;
            _auditoria = auditoria;
            _exportSvc = exportSvc;
        }

        // GET: Usuarios


        public async Task<IActionResult> Index(
           string? estado,
           string? q = null,
           string? fDesde = null,
           string? fHasta = null,
           int page = 1,
           int pageSize = 10)
        {
            // 0) Normalización de estado
            var estadoNorm = (estado ?? "ACTIVO").Trim().ToUpperInvariant();
            if (estadoNorm is not ("ACTIVO" or "INACTIVO" or "TODOS"))
                estadoNorm = "ACTIVO";

            // 1) Fechas (acepta dd/MM/yyyy o yyyy-MM-dd)
            DateTime? desde = ParseDate(fDesde);
            DateTime? hasta = ParseDate(fHasta);

            // 2) Base query (ignora eliminados) con JOINS para traer Rol y Persona (vía Empleado)
            // Nota: En su modelo EMPLEADO_ID == PERSONA_ID, por eso el join EMPLEADO->PERSONA.
            var baseQry =
                from u in _context.USUARIO.AsNoTracking()
                where !u.ELIMINADO
                join r in _context.ROL.AsNoTracking() on u.ROL_ID equals r.ROL_ID
                join e in _context.EMPLEADO.AsNoTracking() on u.EMPLEADO_ID equals e.EMPLEADO_ID
                join p in _context.PERSONA.AsNoTracking() on e.EMPLEADO_ID equals p.PERSONA_ID
                select new
                {
                    U = u,
                    R = r,
                    P = p
                };

            // 3) Filtro por estado
            if (estadoNorm is "ACTIVO" or "INACTIVO")
                baseQry = baseQry.Where(x => x.U.ESTADO == estadoNorm);

            // 4) Búsqueda por texto (ID Usuario, nombre de usuario, nombre Empleado, Rol)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                baseQry = baseQry.Where(x =>
                    EF.Functions.Like(x.U.USUARIO_ID, term) ||
                    EF.Functions.Like(x.U.USUARIO_NOMBRE, term) ||
                    // Nombre completo del empleado (Persona)
                    EF.Functions.Like(
                        (x.P.PERSONA_PRIMERNOMBRE + " " +
                         (x.P.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                         x.P.PERSONA_PRIMERAPELLIDO + " " +
                         (x.P.PERSONA_SEGUNDOAPELLIDO ?? "")).Trim(),
                        term
                    ) ||
                    // Por Rol: ID o Nombre del Rol
                    EF.Functions.Like(x.R.ROL_ID, term) ||
                    EF.Functions.Like(x.R.ROL_NOMBRE, term)
                );
            }

            // 5) Rango de fechas (inclusivo en 'desde' y 'hasta')
            if (desde.HasValue) baseQry = baseQry.Where(x => x.U.FECHA_CREACION >= desde.Value.Date);
            if (hasta.HasValue) baseQry = baseQry.Where(x => x.U.FECHA_CREACION < hasta.Value.Date.AddDays(1));

            // 6) Ordenamiento + Proyección a ViewModel (ANTES de paginar)
            var proyectado = baseQry
                .OrderBy(x => x.U.USUARIO_ID)
                .Select(x => new UsuarioListItemViewModel
                {
                    UsuarioId = x.U.USUARIO_ID,
                    NombreUsuario = x.U.USUARIO_NOMBRE,
                    EmpleadoNombre = (
                        (x.P.PERSONA_PRIMERNOMBRE + " " +
                         (x.P.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                         x.P.PERSONA_PRIMERAPELLIDO + " " +
                         (x.P.PERSONA_SEGUNDOAPELLIDO ?? "")
                        ).Trim()
                    ),
                    RolNombre = x.R.ROL_NOMBRE,
                    FechaCreacion = x.U.FECHA_CREACION,
                    ESTADO = x.U.ESTADO
                });

            // 7) Paginación (normaliza pageSize)
            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 10;

            var resultado = await proyectado.ToPagedAsync(page, pageSize); // <-- usa su extensión de paginado

            // 8) RouteValues para el pager (persistir filtros)
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

        private IQueryable<UsuarioListItemViewModel> BuildUsuariosQuery(
      string estadoNorm, string? q, DateTime? desde, DateTime? hasta)
        {
            var term = string.IsNullOrWhiteSpace(q) ? null : $"%{q.Trim()}%";

            var baseQry =
                from u in _context.USUARIO.AsNoTracking().Where(u => !u.ELIMINADO)

                    // LEFT JOIN PERSONA usando EMPLEADO_ID -> PERSONA_ID
                join p0 in _context.PERSONA on u.EMPLEADO_ID equals p0.PERSONA_ID into gp
                from p in gp.DefaultIfEmpty()

                    // LEFT JOIN ROL usando ROL_ID
                join r0 in _context.ROL on u.ROL_ID equals r0.ROL_ID into gr
                from r in gr.DefaultIfEmpty()

                select new { u, p, r };

            if (estadoNorm is "ACTIVO" or "INACTIVO")
                baseQry = baseQry.Where(x => x.u.ESTADO == estadoNorm);

            if (term != null)
            {
                baseQry = baseQry.Where(x =>
                    EF.Functions.Like(x.u.USUARIO_ID, term) ||
                    EF.Functions.Like(x.u.USUARIO_NOMBRE, term) ||
                    (x.p != null && (
                        EF.Functions.Like(x.p.PERSONA_PRIMERNOMBRE, term) ||
                        EF.Functions.Like(x.p.PERSONA_SEGUNDONOMBRE, term) ||
                        EF.Functions.Like(x.p.PERSONA_PRIMERAPELLIDO, term) ||
                        EF.Functions.Like(x.p.PERSONA_SEGUNDOAPELLIDO, term)
                    )) ||
                    (x.r != null && EF.Functions.Like(x.r.ROL_NOMBRE, term))
                );
            }

            if (desde.HasValue) baseQry = baseQry.Where(x => x.u.FECHA_CREACION >= desde.Value.Date);
            if (hasta.HasValue) baseQry = baseQry.Where(x => x.u.FECHA_CREACION < hasta.Value.Date.AddDays(1));

            return baseQry
                .OrderBy(x => x.u.USUARIO_ID)
                .Select(x => new UsuarioListItemViewModel
                {
                    UsuarioId = x.u.USUARIO_ID,
                    NombreUsuario = x.u.USUARIO_NOMBRE,

                    EmpleadoNombre =
                        (x.p == null ? "" :
                            (
                              (x.p.PERSONA_PRIMERNOMBRE ?? "") +
                              ((x.p.PERSONA_SEGUNDONOMBRE ?? "") == "" ? "" : " " + x.p.PERSONA_SEGUNDONOMBRE) + " " +
                              (x.p.PERSONA_PRIMERAPELLIDO ?? "") +
                              ((x.p.PERSONA_SEGUNDOAPELLIDO ?? "") == "" ? "" : " " + x.p.PERSONA_SEGUNDOAPELLIDO)
                            ).Trim()
                        ),

                    RolNombre = x.r != null ? x.r.ROL_NOMBRE : "",
                    ESTADO = x.u.ESTADO,
                    FechaCreacion = x.u.FECHA_CREACION
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
            var datos = await BuildUsuariosQuery(estadoNorm, qParam, desde, hasta).ToListAsync();

            // 5) ViewData tipado + modelo dentro de ViewData (evita Model=null en Rotativa)
            var pdfViewData = new ViewDataDictionary<IEnumerable<UsuarioListItemViewModel>>(
                new EmptyModelMetadataProvider(),
                new ModelStateDictionary())
            {
                Model = datos ?? new List<UsuarioListItemViewModel>()
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
                        var footerUrl = Url.Action("PdfFooter", "Roles", null, Request.Scheme);

                        var pdf = new ViewAsPdf("~/Views/Usuarios/UsuariosPdf.cshtml")
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
                        var xlsx = _exportSvc.GenerarExcelUsuarios(datos);
                        return File(
                            xlsx,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"Reporte_Areas_{stamp}.xlsx");
                    }

                case "docx":
                case "word":
                    {
                        var docx = _exportSvc.GenerarWordUsuarios(datos);
                        return File(
                            docx,
                            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                            $"Reporte_Areas_{stamp}.docx");
                    }

                default:
                    return BadRequest("Formato no soportado. Use pdf | xlsx | docx.");
            }


        }


















        // GET: Usuarios/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            // 1) Validación de parámetro
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // 2) Cargar USUARIO + ROL (solo lectura)
            var entidad = await _context.USUARIO
                .AsNoTracking()
                .Include(u => u.ROL)
                .FirstOrDefaultAsync(u => u.USUARIO_ID == id && !u.ELIMINADO);

            if (entidad is null) return NotFound();

            // 3) Obtener el nombre completo del empleado desde PERSONA (no hay ThenInclude)
            var persona = await _context.PERSONA
                .AsNoTracking()
                .Where(p => p.PERSONA_ID == entidad.EMPLEADO_ID && !p.ELIMINADO)
                .Select(p => new
                {
                    p.PERSONA_PRIMERNOMBRE,
                    p.PERSONA_SEGUNDONOMBRE,
                    p.PERSONA_PRIMERAPELLIDO,
                    p.PERSONA_SEGUNDOAPELLIDO
                })
                .FirstOrDefaultAsync();

            var empleadoNombre = (persona == null)
                ? entidad.EMPLEADO_ID
                : $"{persona.PERSONA_PRIMERNOMBRE} {(persona.PERSONA_SEGUNDONOMBRE ?? "")} " +
                  $"{persona.PERSONA_PRIMERAPELLIDO} {(persona.PERSONA_SEGUNDOAPELLIDO ?? "")}".Trim();

            // (Opcional) Último cambio de contraseña (si quieres mostrarlo)
            DateTime? ultimoCambioPwd = await _context.HISTORIAL_CONTRASENA
                .AsNoTracking()
                .Where(h => h.USUARIO_ID == entidad.USUARIO_ID && !h.ELIMINADO)
                .OrderByDescending(h => h.FECHA_CREACION)
                .Select(h => (DateTime?)h.FECHA_CREACION)
                .FirstOrDefaultAsync();

            // 4) Proyección a ViewModel de detalle
            var vm = new UsuarioDetailsViewModel
            {
                UsuarioId = entidad.USUARIO_ID,
                NombreUsuario = entidad.USUARIO_NOMBRE,
                RolId = entidad.ROL_ID,
                RolNombre = entidad.ROL?.ROL_NOMBRE,
                EmpleadoId = entidad.EMPLEADO_ID,
                EmpleadoNombre = empleadoNombre,
                ESTADO = entidad.ESTADO,
                EstadoActivo = string.Equals(entidad.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),
                CambioInicial = entidad.USUARIO_CAMBIOINICIAL,
                FechaRegistro = entidad.FECHA_CREACION,
                UltimoCambioPwd = ultimoCambioPwd  // opcional en la vista
            };

            // 5) Auditoría (disponible para la vista)
            ViewBag.Auditoria = new
            {
                CreadoPor = entidad.CREADO_POR,
                FechaCreacion = entidad.FECHA_CREACION,
                ModificadoPor = entidad.MODIFICADO_POR,
                FechaModificacion = entidad.FECHA_MODIFICACION,
                EliminadoPor = entidad.ELIMINADO_POR,
                FechaEliminacion = entidad.FECHA_ELIMINACION
            };

            // 6) Devolver la vista tipada
            return View(vm);
        }




















        // ==========================
        // GET: Usuarios/Create
        // ==========================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new UsuarioCreateViewModel
            {
                // Solo informativo; el definitivo se asigna en POST con Next...
                UsuarioId = await _correlativos.PeekNextUsuarioIdAsync(),
                ESTADO = "ACTIVO",
                EstadoActivo = true,
                CambioInicial = true,
                FechaRegistro = DateTime.Now,

                Roles = await GetRolesActivosAsync(),
                EmpleadosDisponibles = await GetEmpleadosSinUsuarioAsync()
            };

            // Flags para modal de éxito (opcional)
            ViewBag.SavedOk = TempData["SavedOk"];
            ViewBag.SavedName = TempData["SavedName"];

            return View(vm);
        }

        // ==========================
        // POST: Usuarios/Create
        // ==========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UsuarioCreateViewModel vm)
        {
            // Sincroniza checkbox -> cadena ("ACTIVO"/"INACTIVO")
            vm.SincronizarEstado();

            // Validación por DataAnnotations (regex, required, etc.)
            if (!ModelState.IsValid)
            {
                // Reponer ID preview si se perdió
                if (string.IsNullOrWhiteSpace(vm.UsuarioId))
                    vm.UsuarioId = await _correlativos.PeekNextUsuarioIdAsync();

                await CargarCombosAsync(vm);
                return View(vm);
            }

            // === Validaciones de negocio adicionales ===


            // Normalizar SIEMPRE el login a MAYÚSCULAS
            var login = (vm.NombreUsuario ?? string.Empty).Trim().ToUpperInvariant();

            var loginExiste = await _context.USUARIO
                .AnyAsync(u => !u.ELIMINADO && u.USUARIO_NOMBRE == login);


            if (loginExiste)
            {
                ModelState.AddModelError(nameof(vm.NombreUsuario), "El nombre de usuario ya existe.");
                if (string.IsNullOrWhiteSpace(vm.UsuarioId))
                    vm.UsuarioId = await _correlativos.PeekNextUsuarioIdAsync();
                await CargarCombosAsync(vm);
                return View(vm);
            }

            // 2) Empleado aún sin usuario (defensivo, por cambios concurrentes)
            var empId = (vm.EmpleadoId ?? string.Empty).Trim().ToUpperInvariant();
            var empleadoYaTiene = await _context.USUARIO
                .AnyAsync(u => u.EMPLEADO_ID == empId && !u.ELIMINADO);
            if (empleadoYaTiene)
            {
                ModelState.AddModelError(nameof(vm.EmpleadoId), "El empleado seleccionado ya tiene usuario.");
                if (string.IsNullOrWhiteSpace(vm.UsuarioId))
                    vm.UsuarioId = await _correlativos.PeekNextUsuarioIdAsync();
                await CargarCombosAsync(vm);
                return View(vm);
            }

            // ====== Auditoría ======
            var ahora = DateTime.Now;
            var creadoPor = (await (_auditoria?.GetUsuarioNombreAsync() ?? Task.FromResult(User?.Identity?.Name)))
                            ?? "Sistema";

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                // ID definitivo y único (atómico)
                var nuevoId = await _correlativos.NextUsuarioIdAsync();

                // Hash + Salt con el servicio oficial (100,000 iteraciones)
                var salt = PasswordHasher.GenerateSalt();
                var hash = PasswordHasher.Hash(vm.ContraseñaTemporal, salt);

                // Mapear VM -> Entidad EF
                var entidad = new USUARIO
                {
                    USUARIO_ID = nuevoId,
                    USUARIO_NOMBRE = login,
                    USUARIO_CONTRASENA = hash,
                    USUARIO_SALT = salt,
                    USUARIO_CAMBIOINICIAL = true, // regla de negocio
                    ROL_ID = (vm.RolId ?? string.Empty).Trim().ToUpperInvariant(),
                    EMPLEADO_ID = empId,
                    ESTADO = vm.ESTADO,  // "ACTIVO"/"INACTIVO"
                    ELIMINADO = false,

                    // Auditoría
                    CREADO_POR = creadoPor,
                    FECHA_CREACION = ahora
                };

                _context.USUARIO.Add(entidad);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["SavedOk"] = true;
                TempData["SavedName"] = entidad.USUARIO_NOMBRE;
                return RedirectToAction(nameof(Create));
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync();


                // using Microsoft.Data.SqlClient;
                // if (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
                //     ModelState.AddModelError(nameof(vm.NombreUsuario), "Ese nombre de usuario ya existe.");

                ModelState.AddModelError(string.Empty, $"Error BD: {ex.GetBaseException().Message}");

                if (string.IsNullOrWhiteSpace(vm.UsuarioId))
                    vm.UsuarioId = await _correlativos.PeekNextUsuarioIdAsync();



                await CargarCombosAsync(vm);
                return View(vm);
            }
        }

        // ==========================
        // Helpers de combos
        // ==========================
        private async Task CargarCombosAsync(UsuarioCreateViewModel vm)
        {
            vm.Roles = await GetRolesActivosAsync();
            vm.EmpleadosDisponibles = await GetEmpleadosSinUsuarioAsync();
        }

        private async Task<IEnumerable<SelectListItem>> GetRolesActivosAsync()
        {
            return await _context.ROL
                .AsNoTracking()
                .Where(r => !r.ELIMINADO && r.ESTADO == "ACTIVO")
                .OrderBy(r => r.ROL_NOMBRE)
                .Select(r => new SelectListItem
                {
                    Value = r.ROL_ID.Trim().ToUpperInvariant(),
                    Text = r.ROL_NOMBRE
                })
                .ToListAsync();
        }

        /// <summary>
        /// Empleados activos/no eliminados que NO tienen usuario (cualquier estado) asociado.
        /// </summary>
        private async Task<IEnumerable<SelectListItem>> GetEmpleadosSinUsuarioAsync()
        {
            var baseQuery =
                from e in _context.EMPLEADO.AsNoTracking()
                join p in _context.PERSONA.AsNoTracking() on e.EMPLEADO_ID equals p.PERSONA_ID
                where !e.ELIMINADO && e.ESTADO == "ACTIVO"
                   && !p.ELIMINADO && p.ESTADO == "ACTIVO"
                select new
                {
                    e.EMPLEADO_ID,
                    Nombre = (p.PERSONA_PRIMERNOMBRE + " " + (p.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                              p.PERSONA_PRIMERAPELLIDO + " " + (p.PERSONA_SEGUNDOAPELLIDO ?? "")).Trim()
                };

            // Excluir los empleados que ya tienen un usuario (no eliminado), sin importar estado
            var disponibles = await baseQuery
                .Where(x => !_context.USUARIO.Any(u => u.EMPLEADO_ID == x.EMPLEADO_ID && !u.ELIMINADO))
                .OrderBy(x => x.Nombre)
                .Select(x => new SelectListItem
                {
                    Value = x.EMPLEADO_ID.Trim().ToUpperInvariant(),
                    Text = $"{x.Nombre} ({x.EMPLEADO_ID})"
                })
                .ToListAsync();

            return disponibles;
        }



        // ==========================
        // GET: Usuarios/Edit/{id}
        // ==========================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // 1) Carga USUARIO + ROL (sin ThenInclude)
            var entidad = await _context.USUARIO
                .AsNoTracking()
                .Include(u => u.ROL)
                .FirstOrDefaultAsync(u => u.USUARIO_ID == id && !u.ELIMINADO);

            if (entidad == null) return NotFound();

            // 2) Cargar datos de PERSONA del EMPLEADO (consulta separada)
            var persona = await _context.PERSONA
                .AsNoTracking()
                .Where(p => p.PERSONA_ID == entidad.EMPLEADO_ID)
                .Select(p => new
                {
                    p.PERSONA_PRIMERNOMBRE,
                    p.PERSONA_SEGUNDONOMBRE,
                    p.PERSONA_PRIMERAPELLIDO,
                    p.PERSONA_SEGUNDOAPELLIDO
                })
                .FirstOrDefaultAsync();

            var empleadoNombre = (persona == null)
                ? entidad.EMPLEADO_ID
                : $"{persona.PERSONA_PRIMERNOMBRE} {(persona.PERSONA_SEGUNDONOMBRE ?? "")} {persona.PERSONA_PRIMERAPELLIDO} {(persona.PERSONA_SEGUNDOAPELLIDO ?? "")}".Trim();

            // 3) Armar VM
            var vm = new UsuarioEditViewModel
            {
                UsuarioId = entidad.USUARIO_ID,
                NombreUsuario = entidad.USUARIO_NOMBRE,
                RolId = entidad.ROL_ID,
                EmpleadoId = entidad.EMPLEADO_ID, // no editable
                ESTADO = entidad.ESTADO,
                EstadoActivo = string.Equals(entidad.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),
                CambioInicial = entidad.USUARIO_CAMBIOINICIAL,
                FechaRegistro = entidad.FECHA_CREACION,

                // Combos
                Roles = await GetRolesActivosMasActualAsync(entidad.ROL_ID),
                EmpleadosDisponibles = new List<SelectListItem>
        {
            new SelectListItem
            {
                Value = entidad.EMPLEADO_ID,
                Text  = $"{empleadoNombre} ({entidad.EMPLEADO_ID})"
            }
        }
            };

            ViewBag.UpdatedOk = TempData["UpdatedOk"];
            ViewBag.UpdatedName = TempData["UpdatedName"];
            ViewBag.NoChanges = TempData["NoChanges"];

            return View(vm);
        }

        // ==========================
        // POST: Usuarios/Edit/{id}
        // ==========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UsuarioEditViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(id) || !string.Equals(id, vm.UsuarioId, StringComparison.Ordinal))
                return NotFound();

            // Validaciones por DataAnnotations
            if (!ModelState.IsValid)
            {
                await CargarCombosEditAsync(vm);
                return View(vm);
            }

            var entidad = await _context.USUARIO.FirstOrDefaultAsync(u => u.USUARIO_ID == id && !u.ELIMINADO);
            if (entidad == null) return NotFound();




            // ====== Normalización y datos nuevos desde el VM ======
            var nuevoLogin = (vm.NombreUsuario ?? string.Empty).Trim().ToUpperInvariant();
            var nuevoRolId = (vm.RolId ?? string.Empty).Trim().ToUpperInvariant();
            var nuevoEstado = vm.EstadoActivo ? "ACTIVO" : "INACTIVO";

         







            // 1) Unicidad de login (case-insensitive por normalización) excluyendo el propio ID
            var existeOtroConMismoLogin = await _context.USUARIO
                .AnyAsync(u => !u.ELIMINADO && u.USUARIO_NOMBRE == nuevoLogin && u.USUARIO_ID != id);
            if (existeOtroConMismoLogin)
            {
                ModelState.AddModelError(nameof(vm.NombreUsuario), "Ya existe otro usuario con ese nombre.");
                await CargarCombosEditAsync(vm);
                return View(vm);
            }

            // 2) Bloquear cambio de Empleado desde Edit (defensivo)
            if (!string.Equals(vm.EmpleadoId, entidad.EMPLEADO_ID, StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(vm.EmpleadoId), "No se permite cambiar el empleado desde esta pantalla.");
                // Reponer VM combos y vista
                await CargarCombosEditAsync(vm);
                return View(vm);
            }

            // 3) (Opcional pero recomendado) Validar que el rol exista y esté ACTIVO,
            //    salvo que quieras permitir mantener un rol inactivo ya asignado.
            var rolOk = await _context.ROL.AnyAsync(r => r.ROL_ID == nuevoRolId && !r.ELIMINADO && r.ESTADO == "ACTIVO");
            if (!rolOk && !string.Equals(nuevoRolId, entidad.ROL_ID, StringComparison.Ordinal))
            {
                ModelState.AddModelError(nameof(vm.RolId), "El rol seleccionado no es válido.");
                await CargarCombosEditAsync(vm);
                return View(vm);
            }

            // 4) Si marcó “Restablecer contraseña”, valida que envió las contraseñas
            if (vm.RestablecerPassword)
            {
                if (string.IsNullOrWhiteSpace(vm.NuevaContraseña))
                    ModelState.AddModelError(nameof(vm.NuevaContraseña), "Ingrese la nueva contraseña.");
                if (string.IsNullOrWhiteSpace(vm.ConfirmarNuevaContraseña))
                    ModelState.AddModelError(nameof(vm.ConfirmarNuevaContraseña), "Confirme la nueva contraseña.");
                if (!ModelState.IsValid)
                {
                    await CargarCombosEditAsync(vm);
                    return View(vm);
                }
            }

            // ====== ¿Hay cambios? (login, rol, estado, y/o restablecer password) ======
            //bool sinCambios =
            //    string.Equals(entidad.USUARIO_NOMBRE ?? "", nuevoLogin, StringComparison.Ordinal) &&
            //    string.Equals(entidad.ROL_ID ?? "", nuevoRolId, StringComparison.Ordinal) &&
            //    string.Equals(entidad.ESTADO ?? "", nuevoEstado, StringComparison.Ordinal) &&
            //    !vm.RestablecerPassword;

            //if (sinCambios)
            //{
            //    TempData["NoChanges"] = true;
            //    return RedirectToAction(nameof(Edit), new { id });
            //}

            static string N(string? s) => (s ?? "").Trim().ToUpperInvariant();

            // ====== ¿Hay cambios? (login, rol, estado, y/o restablecer password) ======
            bool sinCambios =
                N(entidad.USUARIO_NOMBRE) == N(nuevoLogin) &&
                N(entidad.ROL_ID) == N(nuevoRolId) &&
                N(entidad.ESTADO) == N(nuevoEstado) &&
                !vm.RestablecerPassword;

            if (sinCambios)
            {
                TempData["NoChanges"] = true;
                return RedirectToAction(nameof(Edit), new { id });
            }


            // ====== Auditoría ======
            var ahora = DateTime.Now;
            var usuarioNombre = (await (_auditoria?.GetUsuarioNombreAsync() ?? Task.FromResult(User?.Identity?.Name))) ?? "Sistema";

            // ====== Aplicar cambios ======
            entidad.USUARIO_NOMBRE = nuevoLogin;
            entidad.ROL_ID = nuevoRolId;

            // Estado (no tocar ELIMINADO)
            var estadoOriginalActivo = string.Equals(entidad.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase);
            var estadoNuevoActivo = vm.EstadoActivo;

            if (estadoOriginalActivo != estadoNuevoActivo)
            {
                if (!estadoNuevoActivo)
                {
                    entidad.ESTADO = "INACTIVO";
                    // (opcional) rastro
                    entidad.ELIMINADO_POR = usuarioNombre;
                    entidad.FECHA_ELIMINACION = ahora;
                }
                else
                {
                    entidad.ESTADO = "ACTIVO";
                    // (opcional) rastro de reactivación
                    entidad.ELIMINADO_POR = usuarioNombre;
                    entidad.FECHA_ELIMINACION = ahora;
                }
            }
            else
            {
                entidad.ESTADO = nuevoEstado; // sincroniza por claridad
            }

            // Restablecer contraseña (si marcó el toggle)
            if (vm.RestablecerPassword)
            {
                var newSalt = PasswordHasher.GenerateSalt();
                var newHash = PasswordHasher.Hash(vm.NuevaContraseña!, newSalt);

                entidad.USUARIO_SALT = newSalt;
                entidad.USUARIO_CONTRASENA = newHash;

                // REGLA: si restablecen desde administración, forzar cambio en próximo login
                entidad.USUARIO_CAMBIOINICIAL = true;
            }

            // Auditoría de modificación
            entidad.MODIFICADO_POR = usuarioNombre;
            entidad.FECHA_MODIFICACION = ahora;

            try
            {
                await _context.SaveChangesAsync();

                TempData["UpdatedOk"] = true;
                TempData["UpdatedName"] = entidad.USUARIO_NOMBRE;
                return RedirectToAction(nameof(Edit), new { id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.USUARIO.AnyAsync(e => e.USUARIO_ID == id && !e.ELIMINADO)) return NotFound();
                throw;
            }
        }

        // ==========================
        // Helpers específicos para Edit
        // ==========================

        // Carga combos para re-render de la vista (roles activos + incluir el rol actual)
        private async Task CargarCombosEditAsync(UsuarioEditViewModel vm)
        {
            vm.Roles = await GetRolesActivosMasActualAsync(vm.RolId);
            // Para empleado: solo el actual (no editable)
            string nombre = await GetEmpleadoNombreAsync(vm.EmpleadoId);
            vm.EmpleadosDisponibles = await GetEmpleadoActualAsSelectListAsync(vm.EmpleadoId, nombre);
        }

        // Roles activos + el rol actual aunque esté inactivo (para no romper dropdown)
        private async Task<IEnumerable<SelectListItem>> GetRolesActivosMasActualAsync(string rolActualId)
        {
            rolActualId = (rolActualId ?? "").Trim().ToUpperInvariant();

            var activos = await _context.ROL
                .AsNoTracking()
                .Where(r => !r.ELIMINADO && r.ESTADO == "ACTIVO")
                .OrderBy(r => r.ROL_NOMBRE)
                .Select(r => new SelectListItem
                {
                    Value = r.ROL_ID.Trim().ToUpperInvariant(),
                    Text = r.ROL_NOMBRE
                })
                .ToListAsync();

            // ¿El actual no está en la lista (inactivo)? lo agregamos
            if (!string.IsNullOrEmpty(rolActualId) && !activos.Any(x => x.Value == rolActualId))
            {
                var actual = await _context.ROL.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ROL_ID == rolActualId);
                if (actual != null)
                {
                    activos.Insert(0, new SelectListItem
                    {
                        Value = actual.ROL_ID.Trim().ToUpperInvariant(),
                        Text = $"{actual.ROL_NOMBRE} (inactivo)"
                    });
                }
            }
            return activos;
        }

        // Obtiene el nombre del empleado (para mostrar readonly)
        private async Task<string> GetEmpleadoNombreAsync(string empleadoId)
        {
            empleadoId = (empleadoId ?? "").Trim().ToUpperInvariant();
            var q =
                from e in _context.EMPLEADO.AsNoTracking()
                join p in _context.PERSONA.AsNoTracking() on e.EMPLEADO_ID equals p.PERSONA_ID
                where e.EMPLEADO_ID == empleadoId
                select (p.PERSONA_PRIMERNOMBRE + " " + (p.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        p.PERSONA_PRIMERAPELLIDO + " " + (p.PERSONA_SEGUNDOAPELLIDO ?? "")).Trim();

            return await q.FirstOrDefaultAsync() ?? empleadoId;
        }

        // Devuelve un combo de 1 solo ítem (empleado actual) para bloquear edición en la vista
        private async Task<IEnumerable<SelectListItem>> GetEmpleadoActualAsSelectListAsync(string empleadoId, string? nombre = null)
        {
            var display = nombre ?? await GetEmpleadoNombreAsync(empleadoId);
            return new List<SelectListItem>
    {
        new SelectListItem
        {
            Value = (empleadoId ?? "").Trim().ToUpperInvariant(),
            Text  = $"{display} ({empleadoId})"
        }
    };
        }


























        // GET: Usuarios/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var uSUARIO = await _context.USUARIO
                .Include(u => u.EMPLEADO)
                .Include(u => u.ROL)
                .FirstOrDefaultAsync(m => m.USUARIO_ID == id);
            if (uSUARIO == null)
            {
                return NotFound();
            }

            return View(uSUARIO);
        }

        // POST: Usuarios/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var uSUARIO = await _context.USUARIO.FindAsync(id);
            if (uSUARIO != null)
            {
                _context.USUARIO.Remove(uSUARIO);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool USUARIOExists(string id)
        {
            return _context.USUARIO.Any(e => e.USUARIO_ID == id);
        }
    }
}
