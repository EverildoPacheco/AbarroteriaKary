//Services/Security/KaryPermissionService
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using AbarroteriaKary.Data;

namespace AbarroteriaKary.Services.Security
{
    /// Servicio central: resuelve si un ROL (o USUARIO->ROL) tiene cierto permiso
    /// a nivel de módulo/submódulo y operación (Ver/Crear/Editar/Eliminar).
    public interface IKaryPermissionService
    {
        Task<bool> HasPermissionByRouteAsync(string rolId, string controller, string action, string op /* VER/CREAR/EDITAR/ELIMINAR */);
        Task<bool> HasPermissionAsync(string rolId, string moduloId, string submoduloId, string op);

        // ★ Helpers para invalidar caché cuando se crean/actualizan permisos
        void InvalidatePermission(string rolId, string moduloId, string submoduloId);
        void InvalidateModuleLevel(string rolId, string moduloId);  // submoduloId = null
    }

    public class KaryPermissionService : IKaryPermissionService
    {
        private readonly KaryDbContext _db;
        private readonly IMemoryCache _cache;

        // TTL “normal” para permisos existentes
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        public KaryPermissionService(KaryDbContext db, IMemoryCache cache)
        {
            _db = db; _cache = cache;
        }

        // ===========================
        // Mapeo por ruta Controller/Action
        // ===========================
        public async Task<bool> HasPermissionByRouteAsync(string rolId, string controller, string action, string op)
        {
            var ruta = $"/{(controller ?? "").Trim()}/{(action ?? "").Trim()}";

            // 1) Intentar submódulo por ruta exacta
            var sub = await _db.SUBMODULO
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.SUBMODULO_RUTA == ruta &&
                    s.ELIMINADO == false &&
                    s.ESTADO == "ACTIVO");

            if (sub != null)
                return await HasPermissionAsync(rolId, sub.MODULO_ID, sub.SUBMODULO_ID, op);

            // 2) Fallback por módulo (tolerante singular/plural; sin StringComparison)
            var ctrlUp = (controller ?? "").Trim().ToUpperInvariant();
            var ctrlSing = ToSingular(ctrlUp);
            var ctrlPlur = ToPlural(ctrlUp);

            var modulo = await _db.MODULO
                .AsNoTracking()
                .FirstOrDefaultAsync(m =>
                    m.ELIMINADO == false &&
                    m.ESTADO == "ACTIVO" &&
                    (
                        m.MODULO_ID.ToUpper() == ctrlUp ||
                        m.MODULO_ID.ToUpper() == ctrlSing ||
                        m.MODULO_ID.ToUpper() == ctrlPlur ||
                        m.MODULO_NOMBRE.ToUpper() == ctrlUp ||
                        m.MODULO_NOMBRE.ToUpper() == ctrlSing ||
                        m.MODULO_NOMBRE.ToUpper() == ctrlPlur
                    ));

            if (modulo != null)
                return await HasPermissionAsync(rolId, modulo.MODULO_ID, null, op);

            // Si no mapea nada, denegar
            return false;
        }

        private static string ToSingular(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.EndsWith("ES", StringComparison.Ordinal)) return s.Substring(0, s.Length - 2); // ROLES->ROL
            if (s.EndsWith("S", StringComparison.Ordinal)) return s.Substring(0, s.Length - 1); // PERMISOS->PERMISO
            return s;
        }

        private static string ToPlural(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            if (s.EndsWith("L", StringComparison.Ordinal)) return s + "ES"; // ROL->ROLES
            if (s.EndsWith("ION", StringComparison.Ordinal)) return s + "ES"; // CION->CIONES
            if (!s.EndsWith("S", StringComparison.Ordinal)) return s + "S";  // PERMISO->PERMISOS
            return s;
        }

        // ===========================
        // Núcleo por Ids (con caché)
        // ===========================

        // Clase interna para no usar tipos anónimos en caché
        private sealed class PermFlags
        {
            public bool Ver { get; init; }
            public bool Crear { get; init; }
            public bool Editar { get; init; }
            public bool Eliminar { get; init; }
        }

        //public async Task<bool> HasPermissionAsync(string rolId, string moduloId, string submoduloId, string op)
        //{
        //    var key = Key(rolId, moduloId, submoduloId);

        //    // 1) Intentar obtener del caché
        //    if (!_cache.TryGetValue(key, out PermFlags perms))
        //    {
        //        // 2) Consultar BD
        //        perms = await _db.PERMISOS
        //            .AsNoTracking()
        //            .Where(p => p.ROL_ID == rolId
        //                        && p.MODULO_ID == moduloId
        //                        && p.SUBMODULO_ID == submoduloId    // EF traduce null-safe (IS NULL)
        //                        && p.ELIMINADO == false
        //                        && p.ESTADO == "ACTIVO")
        //            .Select(p => new PermFlags
        //            {
        //                Ver = p.PUEDE_VER,
        //                Crear = p.PUEDE_CREAR,
        //                Editar = p.PUEDE_EDITAR,
        //                Eliminar = p.PUEDE_ELIMINAR
        //            })
        //            .FirstOrDefaultAsync();

        //        // 3) *** Punto clave ***
        //        //    Si NO hay fila -> NO cacheamos (evita “falso negativo” prolongado).
        //        if (perms != null)
        //        {
        //            _cache.Set(key, perms, CacheTtl);
        //        }
        //    }

        //    if (perms == null) return false;

        //    return (op ?? "").ToUpperInvariant() switch
        //    {
        //        "VER" => perms.Ver,
        //        "CREAR" => perms.Crear,
        //        "EDITAR" => perms.Editar,
        //        "ELIMINAR" => perms.Eliminar,
        //        _ => false
        //    };
        //}


        public async Task<bool> HasPermissionAsync(string rolId, string moduloId, string submoduloId, string op)
        {
            // 1) Intentar permiso explícito a nivel SUBMÓDULO
            var keySub = Key(rolId, moduloId, submoduloId);
            if (!_cache.TryGetValue(keySub, out PermFlags flagsSub))
            {
                flagsSub = await _db.PERMISOS
                    .AsNoTracking()
                    .Where(p => p.ROL_ID == rolId
                             && p.MODULO_ID == moduloId
                             && p.SUBMODULO_ID == submoduloId
                             && !p.ELIMINADO
                             && p.ESTADO == "ACTIVO")
                    .Select(p => new PermFlags { Ver = p.PUEDE_VER, Crear = p.PUEDE_CREAR, Editar = p.PUEDE_EDITAR, Eliminar = p.PUEDE_ELIMINAR })
                    .FirstOrDefaultAsync();
                if (flagsSub != null) _cache.Set(keySub, flagsSub, CacheTtl);
            }
            if (flagsSub != null)
            {
                return (op ?? "").ToUpperInvariant() switch
                {
                    "VER" => flagsSub.Ver,
                    "CREAR" => flagsSub.Crear,
                    "EDITAR" => flagsSub.Editar,
                    "ELIMINAR" => flagsSub.Eliminar,
                    _ => false
                };
            }

            // 2) Fallback: permiso a nivel MÓDULO (SUBMODULO_ID = NULL)
            var keyMod = Key(rolId, moduloId, null);
            if (!_cache.TryGetValue(keyMod, out PermFlags flagsMod))
            {
                flagsMod = await _db.PERMISOS
                    .AsNoTracking()
                    .Where(p => p.ROL_ID == rolId
                             && p.MODULO_ID == moduloId
                             && p.SUBMODULO_ID == null
                             && !p.ELIMINADO
                             && p.ESTADO == "ACTIVO")
                    .Select(p => new PermFlags { Ver = p.PUEDE_VER, Crear = p.PUEDE_CREAR, Editar = p.PUEDE_EDITAR, Eliminar = p.PUEDE_ELIMINAR })
                    .FirstOrDefaultAsync();
                if (flagsMod != null) _cache.Set(keyMod, flagsMod, CacheTtl);
            }
            if (flagsMod != null)
            {
                return (op ?? "").ToUpperInvariant() switch
                {
                    "VER" => flagsMod.Ver,
                    "CREAR" => flagsMod.Crear,
                    "EDITAR" => flagsMod.Editar,
                    "ELIMINAR" => flagsMod.Eliminar,
                    _ => false
                };
            }

            // 3) Nada explícito ni a nivel módulo
            return false;
        }






        // ===========================
        // Invalidación de caché (llamar desde controladores tras guardar)
        // ===========================
        public void InvalidatePermission(string rolId, string moduloId, string submoduloId)
            => _cache.Remove(Key(rolId, moduloId, submoduloId));

        public void InvalidateModuleLevel(string rolId, string moduloId)
            => _cache.Remove(Key(rolId, moduloId, null)); // permiso a nivel de módulo

        // ===========================
        // Helpers internos
        // ===========================
        private static string Key(string rolId, string moduloId, string submoduloId)
        {
            // Normalizamos para evitar claves duplicadas por casing
            var r = (rolId ?? "").Trim().ToUpperInvariant();
            var m = (moduloId ?? "").Trim().ToUpperInvariant();
            var s = (submoduloId ?? "NONE").Trim().ToUpperInvariant();
            return $"perm:{r}:{m}:{s}";
        }
    }
}
