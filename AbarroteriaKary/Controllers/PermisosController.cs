using AbarroteriaKary.Data;
using AbarroteriaKary.Filters;
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.Services.Correlativos;   // ★ Servicio de correlativos (SEQUENCE)
using AbarroteriaKary.Services.Extensions; // ToPagedAsync
using AbarroteriaKary.Services.Reportes;
using AbarroteriaKary.Services.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using Rotativa.AspNetCore.Options;
using System.Data;



namespace AbarroteriaKary.Controllers.Seguridad
{
    // Autorización a nivel de módulo Permisos
    //[KaryAuthorize("VER")]
    public class PermisosController : Controller
    {
        private readonly KaryDbContext _db;
        private readonly ICorrelativoService _corr;   //  inyección
        private readonly IKaryPermissionService _perms;
        //private readonly IKaryPermissionService? _permSvc;            //  opcional, invalidación de cache
        private readonly IReporteExportService _exportSvc;





        public PermisosController(KaryDbContext db, ICorrelativoService corr, IKaryPermissionService perms, IReporteExportService exportSvc)
        {
            _db = db;
            _corr = corr;
            _perms = perms;
            _exportSvc = exportSvc;

        }




        [HttpGet]
        public async Task<IActionResult> Index(
            string? estado,
            string? q = null,
            string? fDesde = null,
            string? fHasta = null,
            int page = 1,
            int pageSize = 10,
            CancellationToken ct = default)
        {
            // 0) Normalización de estado
            var estadoNorm = (estado ?? "ACTIVO").Trim().ToUpperInvariant();
            if (estadoNorm is not ("ACTIVO" or "INACTIVO" or "TODOS"))
                estadoNorm = "ACTIVO";

            // 1) Fechas (dd/MM/yyyy o yyyy-MM-dd)
            DateTime? desde = ParseDate(fDesde);
            DateTime? hasta = ParseDate(fHasta);

            // 2) Base: PERMISOS + ROL (solo para mostrar nombre de rol)
            var baseQry =
                from p in _db.PERMISOS.AsNoTracking()
                join r in _db.ROL.AsNoTracking() on p.ROL_ID equals r.ROL_ID
                where !p.ELIMINADO
                select new { p, r };

         

            // 4) Búsqueda por texto (permisoId, rolId, rolNombre)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                baseQry = baseQry.Where(x =>
                    EF.Functions.Like(x.p.PERMISOS_ID, term) ||
                    EF.Functions.Like(x.p.ROL_ID, term) ||
                    EF.Functions.Like(x.r.ROL_NOMBRE, term));
            }

            // 5) Rango de fechas por FECHA_CREACION del permiso
            if (desde.HasValue) baseQry = baseQry.Where(x => x.p.FECHA_CREACION >= desde.Value.Date);
            if (hasta.HasValue) baseQry = baseQry.Where(x => x.p.FECHA_CREACION < hasta.Value.Date.AddDays(1));

            // 6) Agrupado por Rol: una fila por rol
            var agrupado =
                baseQry
                .GroupBy(x => new { x.p.ROL_ID, x.r.ROL_NOMBRE })
                .Select(g => new PermisoListVM
                {
                    RolId = g.Key.ROL_ID,
                    RolNombre = g.Key.ROL_NOMBRE,
                    // Tomamos la fecha de creación más antigua de cualquiera de sus permisos
                    FechaCreacion = g.Min(y => y.p.FECHA_CREACION),
                    // Y un permiso representativo (con IDs tipo "PER0000001" el MIN lexicográfico sirve)
                    PermisosId = g.Min(y => y.p.PERMISOS_ID)
                });

            // 7) Orden + paginación
            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 10;

            var listado = await agrupado
                .OrderBy(x => x.RolNombre)
                .ToPagedAsync(page, pageSize, ct);

            // 8) RouteValues para el pager + toolbar
            listado.RouteValues["estado"] = estadoNorm;
            listado.RouteValues["q"] = q;
            listado.RouteValues["fDesde"] = desde?.ToString("yyyy-MM-dd");
            listado.RouteValues["fHasta"] = hasta?.ToString("yyyy-MM-dd");

            ViewBag.Estado = estadoNorm;
            ViewBag.Q = q;
            ViewBag.FDesde = listado.RouteValues["fDesde"];
            ViewBag.FHasta = listado.RouteValues["fHasta"];

            return View(listado);
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

        //-------------------------------REPORTE----------------------------------------------



        // ====== Footer para PDF (compartido) ======
        [AllowAnonymous]
        [HttpGet]
        public IActionResult PdfFooter()
        {
            return View("~/Views/Shared/Reportes/_PdfFooter.cshtml");
        }

        // ====== Helper fechas (dd/MM/yyyy | yyyy-MM-dd) ======
        //private static DateTime? ParseDate(string? input)
        //{
        //    if (string.IsNullOrWhiteSpace(input)) return null;
        //    var formats = new[] { "dd/MM/yyyy", "yyyy-MM-dd" };
        //    if (DateTime.TryParseExact(input, formats,
        //        CultureInfo.GetCultureInfo("es-GT"),
        //        DateTimeStyles.None, out var d)) return d;
        //    if (DateTime.TryParse(input, out d)) return d;
        //    return null;
        //}

        // ====== Query base: solo lo ASIGNADO, con filtros ======
        private IQueryable<PermisoReporteRow> BuildPermisosDetalleQuery(string? q, DateTime? desde, DateTime? hasta)
        {
            // PERMISOS + ROL + MODULO + SUBMODULO (left join)
            //var qry =
            //    from p in _db.PERMISOS.AsNoTracking()
            //    join r in _db.ROL.AsNoTracking() on p.ROL_ID equals r.ROL_ID
            //    join m in _db.MODULO.AsNoTracking() on p.MODULO_ID equals m.MODULO_ID
            //    join s0 in _db.SUBMODULO.AsNoTracking() on p.SUBMODULO_ID equals s0.SUBMODULO_ID into sjoin
            //    from s in sjoin.DefaultIfEmpty()
            //    where !p.ELIMINADO
            //    select new PermisoReporteRow
            //    {
            //        RolId = r.ROL_ID,
            //        RolNombre = r.ROL_NOMBRE,

            //        ModuloId = m.MODULO_ID,
            //        ModuloNombre = m.MODULO_NOMBRE,

            //        SubmoduloId = p.SUBMODULO_ID,
            //        SubmoduloNombre = (s != null ? s.SUBMODULO_NOMBRE : "Permiso a TODO el módulo"),

            //        PuedeVer = p.PUEDE_VER,
            //        PuedeCrear = p.PUEDE_CREAR,
            //        PuedeEditar = p.PUEDE_EDITAR,
            //        PuedeEliminar = p.PUEDE_ELIMINAR,

            //        FechaCreacion = p.FECHA_CREACION
            //    };

            var qry =
               from p in _db.PERMISOS.AsNoTracking()
               join r in _db.ROL.AsNoTracking() on p.ROL_ID equals r.ROL_ID
               join m in _db.MODULO.AsNoTracking() on p.MODULO_ID equals m.MODULO_ID
               join s0 in _db.SUBMODULO.AsNoTracking() on p.SUBMODULO_ID equals s0.SUBMODULO_ID into sjoin
               from s in sjoin.DefaultIfEmpty()
               where !p.ELIMINADO
               select new PermisoReporteRow
               {
                   RolId = r.ROL_ID,
                   RolNombre = r.ROL_NOMBRE,

                   ModuloId = m.MODULO_ID,
                   ModuloNombre = m.MODULO_NOMBRE,

                   SubmoduloId = p.SUBMODULO_ID,
                   SubmoduloNombre = (s != null ? s.SUBMODULO_NOMBRE : "Permiso a TODO el módulo"),

                   PuedeVer = p.PUEDE_VER,
                   PuedeCrear = p.PUEDE_CREAR,
                   PuedeEditar = p.PUEDE_EDITAR,
                   PuedeEliminar = p.PUEDE_ELIMINAR,

                   FechaCreacion = p.FECHA_CREACION
               };

            // Búsqueda general (rol / módulo / submódulo)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                qry = qry.Where(x =>
                    EF.Functions.Like(x.RolId, term) ||
                    EF.Functions.Like(x.RolNombre, term) ||
                    EF.Functions.Like(x.ModuloNombre, term) ||
                    EF.Functions.Like(x.SubmoduloNombre, term));
            }

            // Rango de fechas por FECHA_CREACION del permiso
            if (desde.HasValue) qry = qry.Where(x => x.FechaCreacion >= desde.Value.Date);
            if (hasta.HasValue) qry = qry.Where(x => x.FechaCreacion < hasta.Value.Date.AddDays(1));

            return qry
                .OrderBy(x => x.RolNombre)
                .ThenBy(x => x.ModuloNombre)
                .ThenBy(x => x.SubmoduloNombre);
        }

        private string GetUsuarioActual()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                if (!string.IsNullOrWhiteSpace(User.Identity!.Name)) return User.Identity.Name!;
                string[] keys = {
                System.Security.Claims.ClaimTypes.Name,
                System.Security.Claims.ClaimTypes.GivenName,
                System.Security.Claims.ClaimTypes.Email,
                "name","usuario","user","UserName","UsuarioNombre"
            };
                foreach (var k in keys)
                {
                    var c = User.FindFirst(k);
                    if (c != null && !string.IsNullOrWhiteSpace(c.Value))
                        return c.Value;
                }
            }
            var ses = HttpContext?.Session?.GetString("UsuarioNombre")
                   ?? HttpContext?.Session?.GetString("UserName");
            if (!string.IsNullOrWhiteSpace(ses)) return ses!;
            return "Admin";
        }

        // ====== /Permisos/Exportar?formato=pdf|xlsx&q=&fDesde=&fHasta=&by= ======
        [HttpGet]
        public async Task<IActionResult> Exportar(
            string formato = "pdf",
            string? q = null,
            string? fDesde = null,
            string? fHasta = null,
            string? by = null)
        {
            // 1) Tomar SIEMPRE del querystring (compatibilidad con dropdown compartido)
            var qs = Request?.Query;
            string qParam = !string.IsNullOrWhiteSpace(qs?["q"]) ? qs!["q"].ToString() : q;
            string fDesdeParam = !string.IsNullOrWhiteSpace(qs?["fDesde"]) ? qs!["fDesde"].ToString() : fDesde;
            string fHastaParam = !string.IsNullOrWhiteSpace(qs?["fHasta"]) ? qs!["fHasta"].ToString() : fHasta;
            string byParam = !string.IsNullOrWhiteSpace(qs?["by"]) ? qs!["by"].ToString() : by;

            // 2) Fechas
            DateTime? desde = ParseDate(fDesdeParam);
            DateTime? hasta = ParseDate(fHastaParam);

            // 3) Data
            var datos = await BuildPermisosDetalleQuery(qParam, desde, hasta).ToListAsync();

            // 4) ViewData tipado para Rotativa
            var pdfViewData = new ViewDataDictionary<IEnumerable<PermisoReporteRow>>(
                new EmptyModelMetadataProvider(),
                new ModelStateDictionary())
            {
                Model = datos ?? new List<PermisoReporteRow>()
            };
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
                        var footerUrl = Url.Action("PdfFooter", "Permisos", null, Request.Scheme);

                        var pdf = new ViewAsPdf("~/Views/Permisos/PermisosPdf.cshtml")
                        {
                            //ViewData = pdfViewData,
                            //PageSize = Size.Letter,
                            //PageOrientation = Orientation.Portrait,
                            //PageMargins = new Margins(10, 10, 20, 12),
                            //CustomSwitches = "--disable-smart-shrinking --print-media-type " +
                            //                 $"--footer-html \"{footerUrl}\" --footer-spacing 4"
                            ViewData = pdfViewData,
                            PageSize = Size.Letter,
                            PageOrientation = Orientation.Portrait,
                            PageMargins = new Margins(10, 10, 20, 12),
                            CustomSwitches = "--encoding utf-8 --disable-smart-shrinking --print-media-type " +
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
                        var xlsx = _exportSvc.GenerarExcelPermisosDetalle(datos);
                        return File(
                            xlsx,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"Reporte_Permisos_{stamp}.xlsx");
                    }

                default:
                    return BadRequest("Formato no soportado. Use pdf | xlsx.");
            }
        }









        [HttpGet]
        public async Task<IActionResult> Details(string rolId)
        {
            if (string.IsNullOrWhiteSpace(rolId))
                return RedirectToAction(nameof(Index));

            var vm = new PermisoListVM
            {
                RolId = rolId,
                RolNombre = await _db.ROL
                    .Where(r => r.ROL_ID == rolId)
                    .Select(r => r.ROL_NOMBRE)
                    .FirstOrDefaultAsync() ?? rolId
            };

            // Catálogos activos
            var mods = await _db.MODULO
                .Where(m => !m.ELIMINADO && m.ESTADO == "ACTIVO")
                .Select(m => new { m.MODULO_ID, m.MODULO_NOMBRE })
                .OrderBy(m => m.MODULO_NOMBRE)
                .ToListAsync();

            var subs = await _db.SUBMODULO
                .Where(s => !s.ELIMINADO && s.ESTADO == "ACTIVO")
                .Select(s => new { s.SUBMODULO_ID, s.SUBMODULO_NOMBRE, s.MODULO_ID })
                .OrderBy(s => s.SUBMODULO_NOMBRE)
                .ToListAsync();

            // Permisos del rol (no eliminados)
            var perms = await _db.PERMISOS
                .Where(p => p.ROL_ID == rolId && !p.ELIMINADO)
                .Select(p => new
                {
                    p.MODULO_ID,
                    p.SUBMODULO_ID,
                    p.PUEDE_VER,
                    p.PUEDE_CREAR,
                    p.PUEDE_EDITAR,
                    p.PUEDE_ELIMINAR
                })
                .ToListAsync();

            // Índices: nivel módulo y nivel submódulo
            var modLevel = perms
                .Where(p => p.SUBMODULO_ID == null)
                .GroupBy(p => p.MODULO_ID, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key!,
                    g => new
                    {
                        Ver = g.Any(x => x.PUEDE_VER),
                        Crear = g.Any(x => x.PUEDE_CREAR),
                        Editar = g.Any(x => x.PUEDE_EDITAR),
                        Eliminar = g.Any(x => x.PUEDE_ELIMINAR)
                    },
                    StringComparer.OrdinalIgnoreCase
                );

            var subLevel = perms
                .Where(p => p.SUBMODULO_ID != null)
                .GroupBy(p => new { p.MODULO_ID, p.SUBMODULO_ID })
                .ToDictionary(
                    g => (Mod: g.Key.MODULO_ID!, Sub: g.Key.SUBMODULO_ID!),
                    g => new
                    {
                        Ver = g.Any(x => x.PUEDE_VER),
                        Crear = g.Any(x => x.PUEDE_CREAR),
                        Editar = g.Any(x => x.PUEDE_EDITAR),
                        Eliminar = g.Any(x => x.PUEDE_ELIMINAR)
                    }
                );

            var result = new List<PermisoItem>();

            foreach (var m in mods)
            {
                modLevel.TryGetValue(m.MODULO_ID, out var mLvl);
                bool moduloTienePermiso = mLvl != null && (mLvl.Ver || mLvl.Crear || mLvl.Editar || mLvl.Eliminar);

                // 1) Si el módulo tiene permiso a nivel módulo => mostramos SOLO esa fila
                if (moduloTienePermiso)
                {
                    result.Add(new PermisoItem
                    {
                        ModuloId = m.MODULO_ID,
                        ModuloNombre = m.MODULO_NOMBRE,
                        SubmoduloId = null,
                        SubmoduloNombre = "Permiso a TODO el módulo",
                        PuedeVer = mLvl!.Ver,
                        PuedeCrear = mLvl.Crear,
                        PuedeEditar = mLvl.Editar,
                        PuedeEliminar = mLvl.Eliminar,
                        Estado = "ACTIVO"
                    });

                    // NOTA: no listamos submódulos heredados para no “llenar” la vista.
                    // Si quisieras además mostrar submódulos con permisos EXPLÍCITOS, agrega este bloque:
                    /*
                    foreach (var s in subs.Where(x => x.MODULO_ID == m.MODULO_ID))
                    {
                        if (subLevel.TryGetValue((m.MODULO_ID, s.SUBMODULO_ID), out var sLvl) &&
                            (sLvl.Ver || sLvl.Crear || sLvl.Editar || sLvl.Eliminar))
                        {
                            result.Add(new PermisoItem
                            {
                                ModuloId = m.MODULO_ID,
                                ModuloNombre = m.MODULO_NOMBRE,
                                SubmoduloId = s.SUBMODULO_ID,
                                SubmoduloNombre = s.SUBMODULO_NOMBRE,
                                // Si prefieres ver solo lo explícito, usa sLvl.* directamente:
                                PuedeVer = sLvl.Ver,
                                PuedeCrear = sLvl.Crear,
                                PuedeEditar = sLvl.Editar,
                                PuedeEliminar = sLvl.Eliminar,
                                Estado = "ACTIVO"
                            });
                        }
                    }
                    */
                    continue; // ya agregamos lo necesario del módulo
                }

                // 2) Si NO hay permiso a nivel módulo, incluimos SOLO submódulos con permisos EXPLÍCITOS
                var anySubAdded = false;
                foreach (var s in subs.Where(x => x.MODULO_ID == m.MODULO_ID))
                {
                    if (subLevel.TryGetValue((m.MODULO_ID, s.SUBMODULO_ID), out var sLvl) &&
                        (sLvl.Ver || sLvl.Crear || sLvl.Editar || sLvl.Eliminar))
                    {
                        result.Add(new PermisoItem
                        {
                            ModuloId = m.MODULO_ID,
                            ModuloNombre = m.MODULO_NOMBRE,
                            SubmoduloId = s.SUBMODULO_ID,
                            SubmoduloNombre = s.SUBMODULO_NOMBRE,
                            PuedeVer = sLvl.Ver,
                            PuedeCrear = sLvl.Crear,
                            PuedeEditar = sLvl.Editar,
                            PuedeEliminar = sLvl.Eliminar,
                            Estado = "ACTIVO"
                        });
                        anySubAdded = true;
                    }
                }

                // Si no hubo submódulos con permisos, no agregamos nada de este módulo.
            }

            vm.Permisos = result;
            return View(vm);
        }




























        //KaryAuthorize("CREAR")



        [HttpGet]
        public async Task<IActionResult> Create(string? rolId = null, string? moduloId = null)
        {
            var vm = new PermisoBulkVM
            {
                Roles = await _db.ROL
                    .Where(r => !r.ELIMINADO && r.ESTADO == "ACTIVO")
                    .OrderBy(r => r.ROL_NOMBRE)
                    .Select(r => new SelectListItem { Value = r.ROL_ID, Text = r.ROL_NOMBRE })
                    .ToListAsync(),
                Modulos = await _db.MODULO
                    .Where(m => !m.ELIMINADO && m.ESTADO == "ACTIVO")
                    .OrderBy(m => m.MODULO_NOMBRE)
                    .Select(m => new SelectListItem { Value = m.MODULO_ID, Text = m.MODULO_NOMBRE })
                    .ToListAsync(),
                RolId = rolId,
                ModuloId = moduloId
            };

            return View("CreateBulk", vm);
        }





        [HttpGet]
        public async Task<IActionResult> SubmodsEstado(string moduloId, string rolId)
        {
            if (string.IsNullOrWhiteSpace(moduloId) || string.IsNullOrWhiteSpace(rolId))
                return Json(Enumerable.Empty<object>());

            // Submódulos activos del módulo
            var submods = await _db.SUBMODULO
                .Where(s => s.MODULO_ID == moduloId && !s.ELIMINADO && s.ESTADO == "ACTIVO")
                .OrderBy(s => s.SUBMODULO_NOMBRE)
                .Select(s => new { s.SUBMODULO_ID, s.SUBMODULO_NOMBRE })
                .ToListAsync();

            // Permisos existentes para ese rol y módulo (incluye nivel módulo: SUBMODULO_ID NULL)
            var perms = await _db.PERMISOS
                .Where(p => p.ROL_ID == rolId && p.MODULO_ID == moduloId && !p.ELIMINADO && p.ESTADO == "ACTIVO")
                .Select(p => new { p.SUBMODULO_ID, p.PUEDE_VER, p.PUEDE_CREAR, p.PUEDE_EDITAR, p.PUEDE_ELIMINAR })
                .ToListAsync();

            // Nivel MÓDULO (SUBMODULO_ID = NULL)
            var pModulo = perms.FirstOrDefault(p => p.SUBMODULO_ID == null);

            var data = new List<object>
    {
        new {
            value = "",
            text = "Permiso a TODO el módulo",
            exists = pModulo != null,
            ver = pModulo?.PUEDE_VER     ?? false,
            crear = pModulo?.PUEDE_CREAR ?? false,
            editar = pModulo?.PUEDE_EDITAR ?? false,
            eliminar = pModulo?.PUEDE_ELIMINAR ?? false
        }
    };

            // Resto de submódulos
            var dict = perms
                .Where(p => p.SUBMODULO_ID != null)
                .ToDictionary(p => p.SUBMODULO_ID!, p => p);

            data.AddRange(submods.Select(s =>
            {
                var found = dict.TryGetValue(s.SUBMODULO_ID, out var p);
                return new
                {
                    value = s.SUBMODULO_ID,
                    text = s.SUBMODULO_NOMBRE,
                    exists = found,
                    ver = p?.PUEDE_VER ?? false,
                    crear = p?.PUEDE_CREAR ?? false,
                    editar = p?.PUEDE_EDITAR ?? false,
                    eliminar = p?.PUEDE_ELIMINAR ?? false
                };
            }));

            return Json(data);
        }










        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkAssign(PermisoBulkVM vm)
        {
            if (string.IsNullOrWhiteSpace(vm.RolId) || string.IsNullOrWhiteSpace(vm.ModuloId))
            {
                TempData["SwalErr"] = "Rol y Módulo son obligatorios.";
                return RedirectToAction(nameof(Create), new { rolId = vm.RolId, moduloId = vm.ModuloId });
            }

            // 1) Items marcados en el form (solo los que traen al menos una acción y no están ya asignados)
            var items = (vm.Items ?? new List<PermisoBulkItem>())
                .Where(i => (i.Ver || i.Crear || i.Editar || i.Eliminar) && !i.YaAsignado)
                .ToList();

            if (!items.Any())
            {
                TempData["SwalWarn"] = "No hay acciones seleccionadas para asignar.";
                return RedirectToAction(nameof(Create), new { rolId = vm.RolId, moduloId = vm.ModuloId });
            }

            // 2) Si marcaron "— Permiso a TODO el módulo —" (SUBMODULO_ID = null/""), replicar a submódulos
            var allItem = items.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.SubmoduloId));
            if (allItem != null)
            {
                var subIds = await _db.SUBMODULO
                    .Where(s => s.MODULO_ID == vm.ModuloId && !s.ELIMINADO && s.ESTADO == "ACTIVO")
                    .Select(s => s.SUBMODULO_ID)
                    .ToListAsync();

                var explicitSubs = new HashSet<string>(
                    items.Where(x => !string.IsNullOrWhiteSpace(x.SubmoduloId)).Select(x => x.SubmoduloId!),
                    StringComparer.OrdinalIgnoreCase);

                var existingSubs = await _db.PERMISOS
                    .Where(p => p.ROL_ID == vm.RolId && p.MODULO_ID == vm.ModuloId && !p.ELIMINADO)
                    .Select(p => p.SUBMODULO_ID)
                    .ToListAsync();
                var existingSubsSet = new HashSet<string?>(existingSubs, StringComparer.OrdinalIgnoreCase);

                foreach (var sid in subIds)
                {
                    if (explicitSubs.Contains(sid)) continue;
                    if (existingSubsSet.Contains(sid)) continue;

                    items.Add(new PermisoBulkItem
                    {
                        SubmoduloId = sid,
                        Ver = allItem.Ver,
                        Crear = allItem.Crear,
                        Editar = allItem.Editar,
                        Eliminar = allItem.Eliminar,
                        YaAsignado = false
                    });
                }
            }

            if (!items.Any())
            {
                TempData["SwalWarn"] = "No se realizaron cambios (posibles duplicados o filas ya asignadas).";
                return RedirectToAction(nameof(Create), new { rolId = vm.RolId, moduloId = vm.ModuloId });
            }

            // 3) Transacción + reservar correlativos
            await using var tx = await _db.Database.BeginTransactionAsync();
            var ids = await _corr.NextPermisosRangeAsync(items.Count);

            int insertados = 0;
            int idxId = 0;
            var affectedSubs = new HashSet<string?>(StringComparer.OrdinalIgnoreCase);

            foreach (var i in items)
            {
                string? subId = string.IsNullOrWhiteSpace(i.SubmoduloId) ? null : i.SubmoduloId;

                bool existe = await _db.PERMISOS.AnyAsync(p =>
                    p.ROL_ID == vm.RolId &&
                    p.MODULO_ID == vm.ModuloId &&
                    p.SUBMODULO_ID == subId &&
                    !p.ELIMINADO);

                if (existe) continue;

                bool ver = i.Ver || i.Crear || i.Editar || i.Eliminar;

                var perm = new PERMISOS
                {
                    PERMISOS_ID = ids[idxId++],
                    ROL_ID = vm.RolId,
                    MODULO_ID = vm.ModuloId,
                    SUBMODULO_ID = subId,
                    PUEDE_VER = ver,
                    PUEDE_CREAR = i.Crear,
                    PUEDE_EDITAR = i.Editar,
                    PUEDE_ELIMINAR = i.Eliminar,
                    CREADO_POR = User.Identity?.Name ?? "SYSTEM",
                    ESTADO = "ACTIVO"
                };

                _db.PERMISOS.Add(perm);
                affectedSubs.Add(perm.SUBMODULO_ID);
                insertados++;
            }

            if (insertados == 0)
            {
                TempData["SwalWarn"] = "No se realizaron cambios (posibles duplicados o filas ya asignadas).";
                return RedirectToAction(nameof(Create), new { rolId = vm.RolId, moduloId = vm.ModuloId });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // *** ✅ FIX CRÍTICO: Invalidar caché después de insertar ***
            foreach (var subId in affectedSubs)
                _perms.InvalidatePermission(vm.RolId, vm.ModuloId, subId);
            _perms.InvalidateModuleLevel(vm.RolId, vm.ModuloId);

            // Modal de éxito
            TempData["SavedOk"] = true;
            TempData["SavedCount"] = insertados;
            TempData["SavedRol"] = await _db.ROL
                .Where(r => r.ROL_ID == vm.RolId)
                .Select(r => r.ROL_NOMBRE)
                .FirstOrDefaultAsync();
            TempData["SavedModulo"] = await _db.MODULO
                .Where(m => m.MODULO_ID == vm.ModuloId)
                .Select(m => m.MODULO_NOMBRE)
                .FirstOrDefaultAsync();

            return RedirectToAction(nameof(Create), new { rolId = vm.RolId, moduloId = vm.ModuloId });
        }


































        //KaryAuthorize("EDITAR")
        [HttpGet]
        public async Task<IActionResult> EditBulk(string? rolId = null, string? moduloId = null)
        {
            var roles = await _db.ROL
                .Where(r => !r.ELIMINADO && r.ESTADO == "ACTIVO")
                .OrderBy(r => r.ROL_NOMBRE)
                .Select(r => new SelectListItem { Value = r.ROL_ID, Text = r.ROL_NOMBRE })
                .ToListAsync();

            // ★ Si viene rolId, deja SOLO ese rol en el dropdown (para que no pueda cambiarlo)
            if (!string.IsNullOrWhiteSpace(rolId))
                roles = roles.Where(x => x.Value == rolId).ToList();

            var vm = new PermisoBulkVM
            {
                Roles = roles,
                Modulos = await _db.MODULO
                    .Where(m => !m.ELIMINADO && m.ESTADO == "ACTIVO")
                    .OrderBy(m => m.MODULO_NOMBRE)
                    .Select(m => new SelectListItem { Value = m.MODULO_ID, Text = m.MODULO_NOMBRE })
                    .ToListAsync(),
                RolId = rolId,
                ModuloId = moduloId
            };

            return View("EditBulk", vm);
        }





        [HttpGet]
        public async Task<IActionResult> SubmodsDetalle(string rolId, string moduloId)
        {
            if (string.IsNullOrWhiteSpace(rolId) || string.IsNullOrWhiteSpace(moduloId))
                return Json(Enumerable.Empty<object>());

            // Traer permisos explícitos (activos e inactivos, no eliminados)
            var perms = await _db.PERMISOS
                .AsNoTracking()
                .Where(p => p.ROL_ID == rolId
                         && p.MODULO_ID == moduloId
                         && !p.ELIMINADO)
                .Select(p => new {
                    p.SUBMODULO_ID,
                    p.PUEDE_VER,
                    p.PUEDE_CREAR,
                    p.PUEDE_EDITAR,
                    p.PUEDE_ELIMINAR,
                    p.ESTADO
                })
                .ToListAsync();

            var modPerm = perms.FirstOrDefault(x => x.SUBMODULO_ID == null);
            bool modExiste = (modPerm != null);
            bool modActivo = modExiste && string.Equals(modPerm.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase);

            var data = new List<object> {
        new {
            value    = "",
            text     = "Permiso a TODO el módulo",
            esModulo = true,
            asignado = modExiste,          // existe fila explícita (activa o inactiva)
            activo   = modActivo,          // útil si quieres pintar distinto
            ver      = modPerm?.PUEDE_VER      ?? false,
            crear    = modPerm?.PUEDE_CREAR    ?? false,
            editar   = modPerm?.PUEDE_EDITAR   ?? false,
            eliminar = modPerm?.PUEDE_ELIMINAR ?? false
        }
    };

            var subs = await _db.SUBMODULO
                .AsNoTracking()
                .Where(s => s.MODULO_ID == moduloId && !s.ELIMINADO && s.ESTADO == "ACTIVO")
                .OrderBy(s => s.SUBMODULO_NOMBRE)
                .Select(s => new { s.SUBMODULO_ID, s.SUBMODULO_NOMBRE })
                .ToListAsync();

            foreach (var s in subs)
            {
                var p = perms.FirstOrDefault(x => x.SUBMODULO_ID == s.SUBMODULO_ID);

                bool existe = (p != null);
                bool activo = existe && string.Equals(p.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase);

                // Herencia visual solo para mostrar valores (no confundir con "asignado")
                bool v = p?.PUEDE_VER ?? (modPerm?.PUEDE_VER ?? false);
                bool cr = p?.PUEDE_CREAR ?? (modPerm?.PUEDE_CREAR ?? false);
                bool ed = p?.PUEDE_EDITAR ?? (modPerm?.PUEDE_EDITAR ?? false);
                bool el = p?.PUEDE_ELIMINAR ?? (modPerm?.PUEDE_ELIMINAR ?? false);

                data.Add(new
                {
                    value = s.SUBMODULO_ID,
                    text = s.SUBMODULO_NOMBRE,
                    esModulo = false,
                    asignado = existe,   // existe fila explícita (activa o inactiva)
                    activo = activo,   // puedes usarlo para estilos si quieres
                    ver = v,
                    crear = cr,
                    editar = ed,
                    eliminar = el
                });
            }

            return Json(data);
        }





        //, KaryAuthorize("EDITAR")


        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveBulk(PermisoBulkVM vm)
        {
            if (string.IsNullOrWhiteSpace(vm.RolId) || string.IsNullOrWhiteSpace(vm.ModuloId))
            {
                TempData["SwalErr"] = "Rol y Módulo son obligatorios.";
                return RedirectToAction(nameof(EditBulk), new { rolId = vm.RolId, moduloId = vm.ModuloId });
            }

            // Solo filas que el usuario tocó
            var items = (vm.Items ?? new List<PermisoBulkItem>())
                        .Where(i => i.Touched)
                        .ToList();

            if (!items.Any())
            {
                TempData["SwalWarn"] = "No se detectaron cambios.";
                return RedirectToAction(nameof(EditBulk), new { rolId = vm.RolId, moduloId = vm.ModuloId });
            }

            // Normalizar claves
            static string Key(string? subId) => subId?.Trim().ToUpperInvariant() ?? "__NULL__";
            var subIdsSolicitados = items
                .Select(i => string.IsNullOrWhiteSpace(i.SubmoduloId) ? null : i.SubmoduloId!.Trim())
                .ToList();

            var subIdsNoNull = subIdsSolicitados.Where(s => s != null).Cast<string>().ToList();
            bool incluyeNivelModulo = subIdsSolicitados.Any(s => s == null);



            var existentes = await _db.PERMISOS
                .Where(p => p.ROL_ID == vm.RolId
                         && p.MODULO_ID == vm.ModuloId
                         && !p.ELIMINADO    // ✅ ver activos e inactivos
                         && (
                              (subIdsNoNull.Count > 0 && subIdsNoNull.Contains(p.SUBMODULO_ID)) ||
                              (incluyeNivelModulo && p.SUBMODULO_ID == null)
                            )
                      )
                .ToListAsync();


            var map = existentes.ToDictionary(e => Key(e.SUBMODULO_ID), e => e, StringComparer.OrdinalIgnoreCase);

            // Detectar cuáles requieren INSERT
            var aInsertar = new List<(string? subId, bool ver, bool crear, bool editar, bool eliminar)>();
            var afectados = new HashSet<string?>(StringComparer.OrdinalIgnoreCase);
            int cambios = 0;

            foreach (var i in items)
            {
                string? subId = string.IsNullOrWhiteSpace(i.SubmoduloId) ? null : i.SubmoduloId!.Trim();
                string k = Key(subId);

                bool ver = i.Ver || i.Crear || i.Editar || i.Eliminar; // regla: si hay alguna acción, ver=1
                bool crear = i.Crear;
                bool editar = i.Editar;
                bool eliminar = i.Eliminar;

                if (!map.TryGetValue(k, out var existe))
                {
                    // No existe: INSERT siempre (incluso si todo 0/0/0/0 para anular herencia)
                    aInsertar.Add((subId, ver, crear, editar, eliminar));
                    afectados.Add(subId);
                    cambios++;
                }
                else
                {
                    // Existe: UPDATE flags, sin tocar ESTADO/ELIMINADO
                    if (existe.PUEDE_VER != ver ||
                        existe.PUEDE_CREAR != crear ||
                        existe.PUEDE_EDITAR != editar ||
                        existe.PUEDE_ELIMINAR != eliminar)
                    {
                        existe.PUEDE_VER = ver;
                        existe.PUEDE_CREAR = crear;
                        existe.PUEDE_EDITAR = editar;
                        existe.PUEDE_ELIMINAR = eliminar;
                        existe.MODIFICADO_POR = User.Identity?.Name ?? "SYSTEM";
                        existe.FECHA_MODIFICACION = DateTime.Now;
                        afectados.Add(subId);
                        cambios++;
                    }
                }
            }

            if (cambios == 0)
            {
                TempData["SwalWarn"] = "No se detectaron cambios reales en la base de datos.";
                return RedirectToAction(nameof(EditBulk), new { rolId = vm.RolId, moduloId = vm.ModuloId });
            }

            await using var tx = await _db.Database.BeginTransactionAsync();

            // Reservar IDs para los INSERTS en un solo viaje
            if (aInsertar.Count > 0)
            {
                var ids = await _corr.NextPermisosRangeAsync(aInsertar.Count);
                int idx = 0;
                foreach (var (subId, ver, crear, editar, eliminar) in aInsertar)
                {
                    _db.PERMISOS.Add(new PERMISOS
                    {
                        PERMISOS_ID = ids[idx++],
                        ROL_ID = vm.RolId,
                        MODULO_ID = vm.ModuloId,
                        SUBMODULO_ID = subId,
                        PUEDE_VER = ver,
                        PUEDE_CREAR = crear,
                        PUEDE_EDITAR = editar,
                        PUEDE_ELIMINAR = eliminar,
                        CREADO_POR = User.Identity?.Name ?? "SYSTEM",
                        ESTADO = "ACTIVO" // No lo tocamos más en edición
                    });
                }
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // Invalidar caché (submódulos tocados + nivel módulo si aplica)
            foreach (var sub in afectados)
                _perms.InvalidatePermission(vm.RolId, vm.ModuloId, sub);
            _perms.InvalidateModuleLevel(vm.RolId, vm.ModuloId);

            TempData["SwalOk"] = $"Permisos actualizados correctamente ({cambios} cambio(s)).";
            return RedirectToAction(nameof(EditBulk), new { rolId = vm.RolId, moduloId = vm.ModuloId });
        }












        // GET: /Permisos/ModsAsignados?rolId=...
        [HttpGet]
        public async Task<IActionResult> ModsAsignados(string rolId)
        {
            if (string.IsNullOrWhiteSpace(rolId))
                return Json(Enumerable.Empty<string>());

            var mods = await _db.PERMISOS
                .AsNoTracking()
                .Where(p => p.ROL_ID == rolId
                         && !p.ELIMINADO
                         && p.ESTADO == "ACTIVO"
                         && (p.PUEDE_VER || p.PUEDE_CREAR || p.PUEDE_EDITAR || p.PUEDE_ELIMINAR))
                .Select(p => p.MODULO_ID)
                .Distinct()
                .ToListAsync();

            return Json(mods);
        }


        // =========================================================
        // LEGADO/UTILIDADES
        // =========================================================

        // GET: /Permisos/SubmodulosPorModulo?moduloId=...
        [HttpGet]
        public async Task<IActionResult> SubmodulosPorModulo(string moduloId)
        {
            if (string.IsNullOrWhiteSpace(moduloId))
                return Json(Enumerable.Empty<object>());

            var data = await _db.SUBMODULO
                .Where(s => s.MODULO_ID == moduloId && !s.ELIMINADO && s.ESTADO == "ACTIVO")
                .OrderBy(s => s.SUBMODULO_NOMBRE)
                .Select(s => new { value = s.SUBMODULO_ID, text = s.SUBMODULO_NOMBRE })
                .ToListAsync();

            return Json(data);
        }
        //KaryAuthorize("CREAR")
        // POST unitario legacy (si decidís conservarlo)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PermisoEditVM vm)
        {
            if (string.IsNullOrWhiteSpace(vm.RolId) || string.IsNullOrWhiteSpace(vm.ModuloId))
                ModelState.AddModelError(string.Empty, "Rol y Módulo son obligatorios.");

            if (!ModelState.IsValid)
                return RedirectToAction(nameof(Create)); // vuelve a la nueva UI

            bool existe = await _db.PERMISOS.AnyAsync(p =>
                p.ROL_ID == vm.RolId && p.MODULO_ID == vm.ModuloId &&
                p.SUBMODULO_ID == (string.IsNullOrWhiteSpace(vm.SubmoduloId) ? null : vm.SubmoduloId) &&
                !p.ELIMINADO);

            if (existe)
            {
                TempData["Warn"] = "Ya existe un permiso con esa combinación (Rol/Módulo/Submódulo).";
                return RedirectToAction(nameof(Create));
            }

            // ★ Usa correlativo de la serie PER
            var nuevoId = await _corr.NextPermisoAsync();

            var perm = new PERMISOS
            {
                PERMISOS_ID = nuevoId,
                ROL_ID = vm.RolId,
                MODULO_ID = vm.ModuloId,
                SUBMODULO_ID = string.IsNullOrWhiteSpace(vm.SubmoduloId) ? null : vm.SubmoduloId,
                PUEDE_VER = vm.PuedeVer,
                PUEDE_CREAR = vm.PuedeCrear,
                PUEDE_EDITAR = vm.PuedeEditar,
                PUEDE_ELIMINAR = vm.PuedeEliminar,
                CREADO_POR = User.Identity?.Name ?? "SYSTEM",
                ESTADO = "ACTIVO"
            };

            _db.Add(perm);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { rolId = vm.RolId });
        }




        // POST: /Permisos/SaveMultiBulk
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveMultiBulk(PermisoMultiBulkVM vm)
        {
            if (string.IsNullOrWhiteSpace(vm.RolId) || vm.Modules == null || vm.Modules.Count == 0)
            {
                TempData["SwalWarn"] = "No se detectaron cambios.";
                return RedirectToAction(nameof(EditBulk), new { rolId = vm.RolId });
            }

            int cambios = 0;
            var toInsert = new List<PERMISOS>();

            var afectados = new HashSet<(string Mod, string? Sub)>(new ModSubComparer());

            await using var tx = await _db.Database.BeginTransactionAsync();

            foreach (var m in vm.Modules)
            {
                if (string.IsNullOrWhiteSpace(m.ModuloId)) continue;
                var items = (m.Items ?? new()).Where(i => i.Touched).ToList();
                if (items.Count == 0) continue;

                // Normalizar ids solicitados
                var subIds = items
                    .Select(i => string.IsNullOrWhiteSpace(i.SubmoduloId) ? null : i.SubmoduloId!.Trim())
                    .ToList();
                var nonNull = subIds.Where(s => s != null).Cast<string>().ToList();
                bool incluyeNivelModulo = subIds.Any(s => s == null);

                // Existentes (activos e inactivos, no eliminados)
                var existentes = await _db.PERMISOS
                    .Where(p => p.ROL_ID == vm.RolId
                             && p.MODULO_ID == m.ModuloId
                             && !p.ELIMINADO
                             && ((nonNull.Count > 0 && nonNull.Contains(p.SUBMODULO_ID))
                                 || (incluyeNivelModulo && p.SUBMODULO_ID == null)))
                    .ToListAsync();

                var map = existentes.ToDictionary(e => e.SUBMODULO_ID ?? "__NULL__", StringComparer.OrdinalIgnoreCase);

                foreach (var i in items)
                {
                    string? sid = string.IsNullOrWhiteSpace(i.SubmoduloId) ? null : i.SubmoduloId.Trim();
                    string key = sid ?? "__NULL__";

                    bool ver = i.Ver || i.Crear || i.Editar || i.Eliminar;

                    if (!map.TryGetValue(key, out var ex))
                    {
                        toInsert.Add(new PERMISOS
                        {
                            // PERMISOS_ID luego
                            ROL_ID = vm.RolId,
                            MODULO_ID = m.ModuloId,
                            SUBMODULO_ID = sid,
                            PUEDE_VER = ver,
                            PUEDE_CREAR = i.Crear,
                            PUEDE_EDITAR = i.Editar,
                            PUEDE_ELIMINAR = i.Eliminar,
                            CREADO_POR = User.Identity?.Name ?? "SYSTEM",
                            ESTADO = "ACTIVO"
                        });
                        afectados.Add((m.ModuloId, sid));
                        cambios++;
                    }
                    else
                    {
                        if (ex.PUEDE_VER != ver ||
                            ex.PUEDE_CREAR != i.Crear ||
                            ex.PUEDE_EDITAR != i.Editar ||
                            ex.PUEDE_ELIMINAR != i.Eliminar)
                        {
                            ex.PUEDE_VER = ver;
                            ex.PUEDE_CREAR = i.Crear;
                            ex.PUEDE_EDITAR = i.Editar;
                            ex.PUEDE_ELIMINAR = i.Eliminar;
                            ex.MODIFICADO_POR = User.Identity?.Name ?? "SYSTEM";
                            ex.FECHA_MODIFICACION = DateTime.Now;

                            afectados.Add((m.ModuloId, sid));
                            cambios++;
                        }
                    }
                }
            }

            if (toInsert.Count > 0)
            {
                var ids = await _corr.NextPermisosRangeAsync(toInsert.Count);
                for (int i = 0; i < toInsert.Count; i++)
                {
                    toInsert[i].PERMISOS_ID = ids[i];
                    _db.PERMISOS.Add(toInsert[i]);
                }
            }

            if (cambios == 0)
            {
                TempData["SwalWarn"] = "No se detectaron cambios.";
                await tx.RollbackAsync();
                return RedirectToAction(nameof(EditBulk), new { rolId = vm.RolId });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // Invalidación de caché
            foreach (var (mod, sub) in afectados)
            {
                _perms.InvalidatePermission(vm.RolId, mod, sub);
                _perms.InvalidateModuleLevel(vm.RolId, mod);
            }

            TempData["SwalOk"] = $"Permisos actualizados correctamente ({cambios} cambio(s)) en {vm.Modules.Count} módulo(s).";
            return RedirectToAction(nameof(EditBulk), new { rolId = vm.RolId });
        }








        private sealed class ModSubComparer : IEqualityComparer<(string Mod, string? Sub)>
        {
            public bool Equals((string Mod, string? Sub) x, (string Mod, string? Sub) y)
            {
                return string.Equals(x.Mod, y.Mod, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.Sub ?? string.Empty, y.Sub ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode((string Mod, string? Sub) obj)
            {
                int h1 = StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Mod ?? string.Empty);
                int h2 = StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Sub ?? string.Empty);
                return HashCode.Combine(h1, h2);
            }
        }





        // POST: /Permisos/SaveMultiAssign  (Create MultiBulk)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveMultiAssign(PermisoMultiBulkVM vm)
        {
            // Validación mínima
            if (string.IsNullOrWhiteSpace(vm.RolId) || vm.Modules == null || vm.Modules.Count == 0)
            {
                TempData["SwalWarn"] = "No se detectaron acciones para crear.";
                return RedirectToAction(nameof(Create), new { rolId = vm.RolId });
            }

            int insertados = 0;
            var toInsert = new List<PERMISOS>();
            var afectados = new HashSet<(string Mod, string? Sub)>(new ModSubComparer());

            await using var tx = await _db.Database.BeginTransactionAsync();

            foreach (var m in vm.Modules)
            {
                if (string.IsNullOrWhiteSpace(m.ModuloId)) continue;

                // Ítems marcados (en Create ignoramos YaAsignado desde UI: vienen deshabilitados los existentes)
                var items = (m.Items ?? new())
                    .Where(i => (i.Ver || i.Crear || i.Editar || i.Eliminar) && !i.YaAsignado)
                    .ToList();

                if (items.Count == 0) continue;

                // Existentes (para evitar duplicados)
                var existentes = await _db.PERMISOS
                    .Where(p => p.ROL_ID == vm.RolId && p.MODULO_ID == m.ModuloId && !p.ELIMINADO)
                    .Select(p => p.SUBMODULO_ID)
                    .ToListAsync();

                var existsSet = new HashSet<string?>(existentes, StringComparer.OrdinalIgnoreCase);

                // Si viene “Permiso a TODO el módulo” (SUBMODULO_ID vacío/null) => replicar a todos los submódulos activos
                var allItem = items.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.SubmoduloId));
                if (allItem != null)
                {
                    var subIds = await _db.SUBMODULO
                        .Where(s => s.MODULO_ID == m.ModuloId && !s.ELIMINADO && s.ESTADO == "ACTIVO")
                        .Select(s => s.SUBMODULO_ID)
                        .ToListAsync();

                    var explicitSubs = new HashSet<string>(
                        items.Where(x => !string.IsNullOrWhiteSpace(x.SubmoduloId))
                             .Select(x => x.SubmoduloId!),
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var sid in subIds)
                    {
                        if (explicitSubs.Contains(sid)) continue; // ya lo marcaste explícito
                        if (existsSet.Contains(sid)) continue;    // ya existe en BD

                        items.Add(new PermisoBulkItem
                        {
                            SubmoduloId = sid,
                            // Regla de "ver": si hay crear/editar/eliminar, ver = true
                            Ver = allItem.Ver || allItem.Crear || allItem.Editar || allItem.Eliminar,
                            Crear = allItem.Crear,
                            Editar = allItem.Editar,
                            Eliminar = allItem.Eliminar,
                            YaAsignado = false
                        });
                    }
                }

                // Solo INSERTS (Create)
                foreach (var i in items)
                {
                    string? subId = string.IsNullOrWhiteSpace(i.SubmoduloId) ? null : i.SubmoduloId;
                    if (existsSet.Contains(subId)) continue; // evitar duplicado con BD

                    bool ver = i.Ver || i.Crear || i.Editar || i.Eliminar;

                    toInsert.Add(new PERMISOS
                    {
                        // PERMISOS_ID se asigna abajo con correlativos
                        ROL_ID = vm.RolId,
                        MODULO_ID = m.ModuloId,
                        SUBMODULO_ID = subId,
                        PUEDE_VER = ver,
                        PUEDE_CREAR = i.Crear,
                        PUEDE_EDITAR = i.Editar,
                        PUEDE_ELIMINAR = i.Eliminar,
                        CREADO_POR = User.Identity?.Name ?? "SYSTEM",
                        ESTADO = "ACTIVO"
                    });

                    afectados.Add((m.ModuloId, subId));
                    insertados++;
                }
            }

            if (toInsert.Count == 0)
            {
                TempData["SwalWarn"] = "No se realizaron inserciones (posibles duplicados o filas ya asignadas).";
                await tx.RollbackAsync();
                return RedirectToAction(nameof(Create), new { rolId = vm.RolId });
            }

            // Correlativos en batch
            var ids = await _corr.NextPermisosRangeAsync(toInsert.Count);
            for (int i = 0; i < toInsert.Count; i++)
            {
                toInsert[i].PERMISOS_ID = ids[i];
                _db.PERMISOS.Add(toInsert[i]);
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            // Invalidación de caché
            foreach (var (mod, sub) in afectados)
            {
                _perms.InvalidatePermission(vm.RolId, mod, sub);
                _perms.InvalidateModuleLevel(vm.RolId, mod);
            }

            // PRG: usa las mismas claves que tu vista Create ya consume (SavedOk/...):
            TempData["SavedOk"] = true;
            TempData["SavedCount"] = insertados;
            TempData["SavedRol"] = await _db.ROL
                .Where(r => r.ROL_ID == vm.RolId)
                .Select(r => r.ROL_NOMBRE)
                .FirstOrDefaultAsync();
            // Como es multi-módulo, ponemos un texto genérico
            TempData["SavedModulo"] = "(múltiples)";

            // Redirige a Create con el rol seleccionado (tu vista muestra el modal de éxito)
            return RedirectToAction(nameof(Create), new { rolId = vm.RolId });
        }

      





    }
}
