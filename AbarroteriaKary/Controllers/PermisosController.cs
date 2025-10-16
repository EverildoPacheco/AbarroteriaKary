using AbarroteriaKary.Data;
using AbarroteriaKary.Filters;
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.Services.Correlativos;   // ★ Servicio de correlativos (SEQUENCE)
using AbarroteriaKary.Services.Extensions; // ToPagedAsync
using AbarroteriaKary.Services.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;


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




        public PermisosController(
            KaryDbContext db,
            ICorrelativoService corr,
            IKaryPermissionService perms)
        {
            _db = db;
            _corr = corr;
            _perms = perms;
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

            // 3) Filtro por estado de PERMISO
            if (estadoNorm is "ACTIVO" or "INACTIVO")
                baseQry = baseQry.Where(x => x.p.ESTADO == estadoNorm);

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
    }
}
