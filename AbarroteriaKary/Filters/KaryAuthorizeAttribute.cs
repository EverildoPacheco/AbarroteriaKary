using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;                // AllowAnonymous
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Authorization;           // IAllowAnonymousFilter
using Microsoft.AspNetCore.Http;
using AbarroteriaKary.Services.Security;

namespace AbarroteriaKary.Filters
{
    /// Uso: [KaryAuthorize("VER")] en acción o controlador
    /// - Obtiene el ROL_ID del usuario autenticado
    /// - Mapea Controller/Action a SUBMODULO.SUBMODULO_RUTA = "/Controller/Action"
    /// - Soporta AJAX: devuelve 401/403 en vez de redirigir
    public class KaryAuthorizeAttribute : TypeFilterAttribute
    {
        public KaryAuthorizeAttribute(string op) : base(typeof(KaryAuthorizeFilter))
        {
            Arguments = new object[] { op };
        }

        private class KaryAuthorizeFilter : IAsyncActionFilter
        {
            private readonly IKaryPermissionService _svc;
            private readonly string _op;

            public KaryAuthorizeFilter(IKaryPermissionService svc, string op)
            {
                _svc = svc;
                _op = op;
            }

            public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
            {
                // 0) Respetar [AllowAnonymous] en la acción o el controlador
                if (context.Filters.OfType<IAllowAnonymousFilter>().Any())
                {
                    await next();
                    return;
                }

                var http = context.HttpContext;
                var user = http.User;

                // ¿Es petición AJAX? (header estándar + Accept json)
                bool isAjax =
                    http.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                    http.Request.Headers["Accept"].Any(x => x.Contains("application/json"));

                // 1) Usuario no autenticado => 401 o Challenge (redirige a Login según cookie auth)
                if (user?.Identity?.IsAuthenticated != true)
                {
                    if (isAjax)
                    {
                        context.Result = new UnauthorizedResult(); // 401
                    }
                    else
                    {
                        context.Result = new ChallengeResult();    // respeta CookieOptions.LoginPath
                    }
                    return;
                }

                // 2) Obtener ROL_ID del usuario (claim principal)
                var rolId = user.FindFirst("ROL_ID")?.Value;

                // (Opcional) Fallback si algún día el claim no está: intenta desde sesión
                if (string.IsNullOrWhiteSpace(rolId))
                {
                    rolId = http.Session.GetString("RolId"); // si lo guardas ahí
                }

                // Si aún no lo tenemos, denegar por seguridad
                if (string.IsNullOrWhiteSpace(rolId))
                {
                    // Autenticado pero sin rol => 403
                    if (isAjax)
                        context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                    else
                        context.Result = new RedirectToActionResult("AccesoDenegado", "Home", null);
                    return;
                }

                // 3) Resolver permiso por ruta actual
                var controller = (string)context.RouteData.Values["controller"];
                var action = (string)context.RouteData.Values["action"];

                var ok = await _svc.HasPermissionByRouteAsync(rolId, controller, action, _op);
                if (!ok)
                {
                    // Autenticado pero sin permiso ⇒ 403
                    if (isAjax)
                        context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                    else
                        context.Result = new RedirectToActionResult("AccesoDenegado", "Home", null);
                    return;
                }

                // 4) OK — continuar
                await next();
            }
        }
    }
}
