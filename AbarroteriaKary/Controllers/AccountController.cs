using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using AbarroteriaKary.Services;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.Data;
using Microsoft.EntityFrameworkCore;

namespace AbarroteriaKary.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly ILoginService _login;
        private readonly KaryDbContext _db;

        public AccountController(ILoginService login, KaryDbContext db)
        {
            _login = login;
            _db = db;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var usuario = await _login.ObtenerUsuarioPorNombreAsync(model.Usuario);
            if (usuario == null)
            {
                ModelState.AddModelError(string.Empty, "Usuario o contraseña no válidos.");
                return View(model);
            }

            var ok = _login.VerificarPassword(model.Password, usuario.USUARIO_CONTRASENA, usuario.USUARIO_SALT);
            if (!ok)
            {
                ModelState.AddModelError(string.Empty, "Usuario o contraseña no válidos.");
                return View(model);
            }

            // Si requiere cambio inicial, redirigir
            if (usuario.USUARIO_CAMBIOINICIAL)
            {
                // Persistimos un temp flag y pasamos el usuario a la vista de cambio
                TempData["UsuarioCambio"] = usuario.USUARIO_NOMBRE;
                return RedirectToAction(nameof(ChangePassword));
            }

            // Firmar cookie
            var principal = _login.ConstruirClaims(usuario);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            // Si llega aquí sin TempData (ej. link directo), muestre el formulario vacío
            var vm = new ChangePasswordViewModel
            {
                Usuario = TempData["UsuarioCambio"] as string ?? string.Empty
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var usuario = await _db.USUARIO.FirstOrDefaultAsync(u => u.USUARIO_NOMBRE == model.Usuario);
            if (usuario == null)
            {
                ModelState.AddModelError("", "Usuario no encontrado.");
                return View(model);
            }

            // Validación de contraseña actual (si aplica)
            if (!string.IsNullOrEmpty(model.PasswordActual))
            {
                var ok = _login.VerificarPassword(model.PasswordActual, usuario.USUARIO_CONTRASENA, usuario.USUARIO_SALT);
                if (!ok)
                {
                    ModelState.AddModelError("", "La contraseña actual no es correcta.");
                    return View(model);
                }
            }

            // Generar nuevo hash + salt
            var (hash, salt) = _login.GenerarHash(model.PasswordNuevo);
            usuario.USUARIO_CONTRASENA = hash;
            usuario.USUARIO_SALT = salt;
            usuario.USUARIO_CAMBIOINICIAL = false;
            usuario.MODIFICADO_POR = model.Usuario;
            usuario.FECHA_MODIFICACION = DateTime.Now;

            // (Opcional) guardar en HISTORIAL_CONTRASENA
            // _db.HISTORIAL_CONTRASENAs.Add(new HISTORIAL_CONTRASENA { ... });

            await _db.SaveChangesAsync();

            TempData["Msg"] = "Contraseña actualizada.";
            return RedirectToAction(nameof(Login));
        }

        public IActionResult Denied() => View(); // opcional
    }
}
