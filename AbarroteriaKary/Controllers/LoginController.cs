using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using AbarroteriaKary.Data;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.Services.Security;
using AbarroteriaKary.Models;

namespace AbarroteriaKary.Controllers
{
    public class LoginController : Controller
    {
        private readonly KaryDbContext _context;

        public LoginController(KaryDbContext context)
        {
            _context = context;
        }

        // GET: /Login
        public IActionResult Index()
        {
            // Prefill "Usuario" si guardamos cookie "KARY_USER"
            if (Request.Cookies.TryGetValue("KARY_USER", out var usuarioCookie))
            {
                ViewBag.UsuarioCookie = usuarioCookie;
            }
            return View();
        }

        // POST: /Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(LoginViewModels model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // 1) Buscar usuario activo y no eliminado
            var usuario = _context.USUARIO
                .AsNoTracking()
                .FirstOrDefault(u => u.USUARIO_NOMBRE == model.Usuario && u.ELIMINADO == false);

            if (usuario == null)
            {
                AgregarBitacora(null, "LOGIN_FALLIDO", $"Usuario inexistente: {model.Usuario}");
                ModelState.AddModelError("", "El usuario ingresado no existe.");
                return View(model);
            }

            // 2) Validar ESTADO textual = 'ACTIVO'
            if (!string.Equals(usuario.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase))
            {
                AgregarBitacora(usuario.USUARIO_ID, "LOGIN_FALLIDO", "Usuario deshabilitado (ESTADO != ACTIVO)");
                ModelState.AddModelError("", "El usuario está deshabilitado. Contacte al administrador.");
                return View(model);
            }

            // 3) Validar contraseña contra VARBINARY hash + SALT
            bool passwordOk = false;

            // Nota: USUARIO_CONTRASENA y USUARIO_SALT son byte[] por VARBINARY en BD
            var userFull = _context.USUARIO
                .First(u => u.USUARIO_ID == usuario.USUARIO_ID); // recargar con tracking para futuras actualizaciones si se desea

            if (userFull.USUARIO_SALT != null && userFull.USUARIO_SALT.Length > 0)
            {
                // Esquema moderno PBKDF2
                passwordOk = PasswordHasher.Verify(model.Password, userFull.USUARIO_SALT, userFull.USUARIO_CONTRASENA);
            }
            else
            {
                // Compatibilidad: usuarios legados con SHA256 plano en VARBINARY
                passwordOk = PasswordHasher.VerifyLegacySha256(model.Password, userFull.USUARIO_CONTRASENA);
            }

            if (!passwordOk)
            {
                AgregarBitacora(usuario.USUARIO_ID, "LOGIN_FALLIDO", "Contraseña incorrecta");
                ModelState.AddModelError("", "La contraseña es incorrecta.");
                return View(model);
            }

            // 4) Recordarme: guardamos el nombre de usuario en cookie (comodidad para prellenar)
            if (model.Recordarme)
            {
                Response.Cookies.Append(
                    "KARY_USER",
                    model.Usuario,
                    new CookieOptions { Expires = DateTimeOffset.Now.AddDays(30), HttpOnly = false, IsEssential = true }
                );
            }
            else
            {
                Response.Cookies.Delete("KARY_USER");
            }

            // 5) ¿Cambio inicial obligatorio?
            if (userFull.USUARIO_CAMBIOINICIAL)
            {
                HttpContext.Session.SetString("UsuarioId", userFull.USUARIO_ID);
                AgregarBitacora(userFull.USUARIO_ID, "LOGIN_OK_CAMBIO_INICIAL", "Acceso con cambio inicial requerido");
                return RedirectToAction("CambiarContrasena", "Login");
            }

            // 6) Login OK: setear sesión mínima
            HttpContext.Session.SetString("UsuarioId", userFull.USUARIO_ID);
            HttpContext.Session.SetString("UsuarioNombre", userFull.USUARIO_NOMBRE);



            AgregarBitacora(userFull.USUARIO_ID, "LOGIN_OK", "Acceso correcto");
            return RedirectToAction("Inicio", "Home");
        }

        // GET: /Login/CambiarContrasena
        public IActionResult CambiarContrasena()
        {
            // Proteger ruta: requiere sesión
            var uid = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(uid))
                return RedirectToAction("Index");

            return View();
        }

        // POST: /Login/CambiarContrasena
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CambiarContrasena(CambioContrasenaViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var uid = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(uid))
                return RedirectToAction("Index");

            var usuario = _context.USUARIO.FirstOrDefault(u => u.USUARIO_ID == uid && u.ELIMINADO == false);
            if (usuario == null)
            {
                ModelState.AddModelError("", "No se encontró el usuario.");
                return View(model);
            }

           

            // 2) Generar nuevo SALT + HASH PBKDF2 y actualizar
            var newSalt = PasswordHasher.GenerateSalt();
            var newHash = PasswordHasher.Hash(model.NuevaContrasena, newSalt);

            // Guardar historial ANTES o DESPUÉS; aquí guardamos el nuevo estado
            var historial = new HISTORIAL_CONTRASENA
            {
                HISTORIAL_ID = IdGenerator.NewId10(),
                USUARIO_ID = usuario.USUARIO_ID,
                HASH = newHash,
                SALT = newSalt,
                FECHA_CREACION = DateTime.Now,
                CREADO_POR = usuario.USUARIO_NOMBRE,
                ESTADO = "ACTIVO",
                ELIMINADO = false
            };
            _context.HISTORIAL_CONTRASENA.Add(historial);

            usuario.USUARIO_CONTRASENA = newHash;
            usuario.USUARIO_SALT = newSalt;
            usuario.USUARIO_CAMBIOINICIAL = false; // ya cambió
            usuario.MODIFICADO_POR = usuario.USUARIO_NOMBRE;
            usuario.FECHA_MODIFICACION = DateTime.Now;

            _context.SaveChanges();

            AgregarBitacora(usuario.USUARIO_ID, "CAMBIO_CONTRASENA", "Contraseña actualizada");
            TempData["Mensaje"] = "Contraseña actualizada correctamente.";

            return RedirectToAction("Inicio", "Home");
        }

        //// GET: /Login/Logout
        //public IActionResult Logout()
        //{
        //    AgregarBitacora(HttpContext.Session.GetString("UsuarioId"), "LOGOUT", "Salida de sesión");
        //    HttpContext.Session.Clear();
        //    HttpContext.Response.Cookies.Delete(".AspNetCore.Session");
        //    return RedirectToAction("Index", "Login");
        //}


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            var uid = HttpContext.Session.GetString("UsuarioId");
            AgregarBitacora(uid, "LOGOUT", "Salida de sesión");

            HttpContext.Session.Clear();
            Response.Cookies.Delete(".AspNetCore.Session");

            // Si guardó una cookie “Recordarme”, no es necesario borrarla para cerrar sesión,
            // porque solo rellena el usuario en el login; si quiere, puede dejarla.
            return RedirectToAction("Index", "Login");
        }




        /// <summary>
        /// Inserta en BITACORA (opcional, útil para auditoría).
        /// </summary>
        private void AgregarBitacora(string usuarioId, string accion, string detalle)
        {
            try
            {
                var bit = new BITACORA
                {
                    BITACORA_ID = IdGenerator.NewId10(),
                    USUARIO_ID = usuarioId,
                    ACCION = accion,
                    DETALLE = detalle,
                    CREADO_POR = usuarioId ?? "SYSTEM",
                    FECHA_CREACION = DateTime.Now,
                    ESTADO = "ACTIVO",
                    ELIMINADO = false
                };
                _context.BITACORA.Add(bit);
                _context.SaveChanges();
            }
            catch
            {
                // No romper flujo de login si la bitácora falla
            }
        }
    }
}
