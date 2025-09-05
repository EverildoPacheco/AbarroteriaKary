using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.Services.Mail;
using AbarroteriaKary.Services.Security;
using Microsoft.AspNetCore.Authentication;                // ★ nuevo
using Microsoft.AspNetCore.Authentication.Cookies;        // ★ nuevo
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;  // Base64Url
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Net.Mail;
using System.Security.Claims;                             // ★ nuevo
using System.Security.Cryptography;                   // ← para Rfc2898DeriveBytes en el fallback
using System.Text;

namespace AbarroteriaKary.Controllers
{
    public class LoginController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ILogger<LoginController> _logger;

        public LoginController(KaryDbContext context, ILogger<LoginController> logger)
        {
            _context = context;
            _logger = logger;

        }

        // GET: /Login
        public IActionResult Index()
        {
            // Prefill "Usuario" si guardamos cookie "KARY_USER"
            if (Request.Cookies.TryGetValue("KARY_USER", out var usuarioCookie))
                ViewBag.UsuarioCookie = usuarioCookie;

            // Si venimos de un token vencido/ inválido, mostrar alerta en el form
            if (TempData["LoginError"] is string tokErr && !string.IsNullOrWhiteSpace(tokErr))
                ModelState.AddModelError(string.Empty, tokErr);

            return View();
        }

        // POST: /Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(LoginViewModels model)
        {
            if (!ModelState.IsValid)
                return View(model);


            // ★ 1) NORMALIZAR el usuario de entrada (elige Upper o Lower; aquí uso Upper)
            var inputUser = (model.Usuario ?? string.Empty).Trim().ToUpperInvariant();

            // ★ 2) USAR inputUser en la consulta (no model.Usuario)
            var usuario = await _context.USUARIO
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.USUARIO_NOMBRE == inputUser && u.ELIMINADO == false);
            //// 1) Buscar usuario activo y no eliminado (por nombre de usuario)
            //var usuario = await _context.USUARIO         // ★ async
            //    .AsNoTracking()
            //    .FirstOrDefaultAsync(u => u.USUARIO_NOMBRE == model.Usuario && u.ELIMINADO == false); // ★ async

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

            // Recargar con tracking para poder MIGRAR hash si hace falta
            var userFull = await _context.USUARIO       // ★ async
                .FirstAsync(u => u.USUARIO_ID == usuario.USUARIO_ID); // ★ async

            if (userFull.USUARIO_SALT != null && userFull.USUARIO_SALT.Length > 0)
            {
                // Camino principal: PBKDF2 con TU PasswordHasher (100,000 iter)
                passwordOk = PasswordHasher.Verify(model.Password, userFull.USUARIO_SALT, userFull.USUARIO_CONTRASENA);

                // Fallback COMPATIBILIDAD: probar con 120,000 (usuarios creados antes del fix)
                if (!passwordOk)
                {
                    const int OldIterations = 120_000;
                    passwordOk = VerifyWithIterations(model.Password, userFull.USUARIO_SALT, userFull.USUARIO_CONTRASENA, OldIterations);

                    if (passwordOk)
                    {
                        // MIGRACIÓN TRANSPARENTE al esquema oficial (100k + nuevo salt)
                        var newSalt = PasswordHasher.GenerateSalt();
                        var newHash = PasswordHasher.Hash(model.Password, newSalt);

                        userFull.USUARIO_SALT = newSalt;
                        userFull.USUARIO_CONTRASENA = newHash;
                        userFull.MODIFICADO_POR = userFull.USUARIO_NOMBRE;
                        userFull.FECHA_MODIFICACION = DateTime.Now;
                        await _context.SaveChangesAsync(); // ★ async
                    }
                }
            }
            else
            {
                // Compatibilidad: usuarios legados con SHA256 plano en VARBINARY (SALT NULL)
                passwordOk = PasswordHasher.VerifyLegacySha256(model.Password, userFull.USUARIO_CONTRASENA);

                if (passwordOk)
                {
                    // MIGRAR a PBKDF2 (100k) inmediatamente
                    var newSalt = PasswordHasher.GenerateSalt();
                    var newHash = PasswordHasher.Hash(model.Password, newSalt);

                    userFull.USUARIO_SALT = newSalt;
                    userFull.USUARIO_CONTRASENA = newHash;
                    userFull.MODIFICADO_POR = userFull.USUARIO_NOMBRE;
                    userFull.FECHA_MODIFICACION = DateTime.Now;
                    await _context.SaveChangesAsync(); // ★ async
                }
            }

            if (!passwordOk)
            {
                AgregarBitacora(usuario.USUARIO_ID, "LOGIN_FALLIDO", "Contraseña incorrecta");
                ModelState.AddModelError("", "La contraseña es incorrecta.");
                return View(model);
            }

            // 4) Recordarme
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

            // Claims + SignIn
            var claims = new List<Claim>
    {
        new Claim("USUARIO_ID", userFull.USUARIO_ID),
        new Claim("UsuarioNombre", userFull.USUARIO_NOMBRE),
        new Claim(ClaimTypes.Name, userFull.USUARIO_NOMBRE),
    };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = true
                }
            );

            AgregarBitacora(userFull.USUARIO_ID, "LOGIN_OK", "Acceso correcto");
            return RedirectToAction("Inicio", "Home");
        }





        // Verifica PBKDF2/SHA256 con un conteo de iteraciones distinto (compatibilidad)
        private static bool VerifyWithIterations(string password, byte[] salt, byte[] storedHash, int iterations)
        {
            if (salt == null || storedHash == null) return false;
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            var computed = pbkdf2.GetBytes(storedHash.Length); // respeta el tamaño del hash guardado (32 bytes)
            return CryptographicOperations.FixedTimeEquals(computed, storedHash);
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

       







        [HttpPost]
        [ValidateAntiForgeryToken]
        //public IActionResult Logout()
        public async Task<IActionResult> Logout()    // ★ ahora async

        {
            
            //return RedirectToAction("Index", "Login");
            var uid = HttpContext.Session.GetString("UsuarioId");
            AgregarBitacora(uid, "LOGOUT", "Salida de sesión");

            // ★ cerrar cookie de autenticación
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            HttpContext.Session.Clear();
            Response.Cookies.Delete(".AspNetCore.Session");
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









        //---------------------------- CONTROLADORES PARA ENVIO DE CORREO-----------------------------------------/////


        //GET: EnviaCorreo
        [HttpGet]
        public IActionResult EnviaCorreo()
        {
            return View(new RecoveryRequestViewModel());
        }


        //POST: EnviaCorreo

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviaCorreo(RecoveryRequestViewModel vm, [FromServices] IEmailSender mail)
        {
            // 0) Validaciones de DataAnnotations (Required/EmailAddress)
            if (!ModelState.IsValid)
            {
                TempData["RecoveryError"] = "El correo ingresado no es válido.";
                return View(vm);
            }

            // 1) Normalizar correo y validar formato en servidor
            var email = (vm.Email ?? string.Empty).Trim().ToLowerInvariant();
            if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            {
                TempData["RecoveryError"] = "Formato de correo inválido.";
                return View(vm);
            }

            // 2) PERSONA por correo (primero ignorando estado para dar mensaje específico)
            var personaByEmail = await _context.PERSONA
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PERSONA_CORREO.ToLower() == email && !p.ELIMINADO);

            if (personaByEmail == null)
            {
                TempData["RecoveryError"] = "El correo no está registrado en el sistema.";
                return View(vm);
            }
            if (!string.Equals(personaByEmail.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase))
            {
                TempData["RecoveryError"] = "La persona asociada a este correo está INACTIVA.";
                return View(vm);
            }

            // 3) EMPLEADO vinculado (PERSONA_ID == EMPLEADO_ID)
            var empleado = await _context.EMPLEADO
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.EMPLEADO_ID == personaByEmail.PERSONA_ID && !e.ELIMINADO);

            if (empleado == null)
            {
                TempData["RecoveryError"] = "No se encontró un empleado asociado a este correo.";
                return View(vm);
            }
            if (!string.Equals(empleado.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase))
            {
                TempData["RecoveryError"] = "El empleado asociado a este correo está INACTIVO.";
                return View(vm);
            }

            // 4) USUARIO del empleado
            var usuario = await _context.USUARIO
                .FirstOrDefaultAsync(u => u.EMPLEADO_ID == empleado.EMPLEADO_ID && !u.ELIMINADO);

            if (usuario == null)
            {
                TempData["RecoveryError"] = "No se encontró un usuario asociado al empleado.";
                return View(vm);
            }
            if (!string.Equals(usuario.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase))
            {
                TempData["RecoveryError"] = "El usuario asociado está INACTIVO.";
                return View(vm);
            }

            // 5) Invalidar tokens anteriores activos/no usados (defensivo)
            var ahora = DateTime.Now;
            var creadoPor = usuario.USUARIO_NOMBRE ?? "SYSTEM";

            var prevTokens = await _context.TOKEN_RECUPERACION
                .Where(t => t.USUARIO_ID == usuario.USUARIO_ID
                            && !t.ELIMINADO
                            && t.ESTADO == "ACTIVO"
                            && !t.TOKEN_USADO
                            && t.TOKEN_EXPIRA > ahora)
                .ToListAsync();

            foreach (var t in prevTokens)
            {
                t.ESTADO = "INACTIVO";
                t.ELIMINADO = true;
                t.MODIFICADO_POR = creadoPor;
                t.FECHA_MODIFICACION = ahora;
            }
            if (prevTokens.Count > 0) await _context.SaveChangesAsync();

            // 6) Generar token: CRUDO para URL, HASH (HEX) para BD
            var rawTokenBytes = RandomNumberGenerator.GetBytes(32);
            var rawToken = WebEncoders.Base64UrlEncode(rawTokenBytes);  // seguro para URL

            var tokenHashHex = Sha256Hex(rawToken); // almacenamos SOLO el hash
            var expira = ahora.AddMinutes(15);

            var tk = new TOKEN_RECUPERACION
            {
                TOKEN_ID = IdGenerator.NewId10(),
                USUARIO_ID = usuario.USUARIO_ID,
                TOKEN_VALOR = tokenHashHex,   // HASH (HEX) del token crudo
                TOKEN_EXPIRA = expira,
                TOKEN_USADO = false,
                USADO_EN = null,

                // Auditoría
                CREADO_POR = creadoPor,
                FECHA_CREACION = ahora,
                MODIFICADO_POR = null,
                FECHA_MODIFICACION = null,
                ELIMINADO = false,
                ELIMINADO_POR = null,
                FECHA_ELIMINACION = null,
                ESTADO = "ACTIVO"
            };

            _context.TOKEN_RECUPERACION.Add(tk);
            await _context.SaveChangesAsync();

            // 7) URL absoluta al endpoint de Recovery (envías el token CRUDO)
            var url = Url.Action(nameof(Recovery), "Login", new { token = rawToken }, protocol: Request.Scheme);

            // 8) Armar y enviar correo
            var nombre = $"{personaByEmail.PERSONA_PRIMERNOMBRE} {personaByEmail.PERSONA_PRIMERAPELLIDO}".Trim();
            var logoAbsUrl = $"{Request.Scheme}://{Request.Host}/img/LOGO.png";   // URL absoluta del logo

            var html = EmailTemplates.BuildRecoveryEmailHtml(nombre, url, expira, logoAbsUrl);

            try
            {
                await mail.SendAsync(vm.Email, "🔐 Recupera tu contraseña — Abarrotería Kary", html);
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, "SMTP error enviando correo de recuperación");
                TempData["RecoveryError"] = "No se pudo enviar el correo en este momento. Intenta nuevamente.";
                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando correo de recuperación");
                TempData["RecoveryError"] = "Ocurrió un error al enviar el correo.";
                return View(vm);
            }

            // 9) Éxito → mensaje (tu JS redirige al Login al cerrar el modal)
            TempData["RecoveryInfo"] = "Te enviamos un enlace de recuperación a tu correo.";
            return RedirectToAction(nameof(EnviaCorreo));
        }




        // Helper SHA256 → HEX
        private static string Sha256Hex(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }



        [HttpGet]
        public async Task<IActionResult> Recovery(string token)
        {
            const string errorMsg = "Tu enlace de recuperación ha expirado o no es válido. Solicita uno nuevo.";

            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["LoginError"] = errorMsg;
                return RedirectToAction("Index"); // Login/Index
            }

            var tokenHashHex = Sha256Hex(token);
            var ahora = DateTime.Now;

            var tk = await _context.TOKEN_RECUPERACION
                .AsNoTracking()
                .FirstOrDefaultAsync(t =>
                    t.TOKEN_VALOR == tokenHashHex &&
                    !t.ELIMINADO &&
                    t.ESTADO == "ACTIVO" &&
                    !t.TOKEN_USADO &&
                    t.TOKEN_EXPIRA >= ahora);

            if (tk == null)
            {
                TempData["LoginError"] = errorMsg;
                return RedirectToAction("Index"); // Login/Index
            }

            // OK → reusar la misma vista de cambio de contraseña en “modo recuperación”
            ViewBag.FromRecovery = true;
            ViewBag.RecoveryToken = token; // token crudo para el POST
            return View("CambiarContrasena", new CambioContrasenaViewModel());
        }





        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CambiarContrasenaPorToken(CambioContrasenaViewModel model, string token)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.FromRecovery = true;
                ViewBag.RecoveryToken = token;
                return View("CambiarContrasena", model);
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Mensaje"] = "El enlace de recuperación no es válido o ha expirado.";
                return RedirectToAction("Index");
            }

            var tokenHashHex = Sha256Hex(token);
            var ahora = DateTime.Now;

            var tk = await _context.TOKEN_RECUPERACION
                .FirstOrDefaultAsync(t =>
                    t.TOKEN_VALOR == tokenHashHex &&
                    !t.ELIMINADO &&
                    t.ESTADO == "ACTIVO" &&
                    !t.TOKEN_USADO &&
                    t.TOKEN_EXPIRA >= ahora);

            if (tk == null)
            {
                TempData["Mensaje"] = "El enlace de recuperación no es válido o ha expirado.";
                return RedirectToAction("Index");
            }

            var usuario = await _context.USUARIO.FirstOrDefaultAsync(u => u.USUARIO_ID == tk.USUARIO_ID && !u.ELIMINADO);
            if (usuario == null)
            {
                TempData["Mensaje"] = "No se pudo completar la recuperación.";
                return RedirectToAction("Index");
            }

            // 1) Hash PBKDF2 (tu servicio)
            var newSalt = PasswordHasher.GenerateSalt();
            var newHash = PasswordHasher.Hash(model.NuevaContrasena, newSalt);

            usuario.USUARIO_SALT = newSalt;
            usuario.USUARIO_CONTRASENA = newHash;
            usuario.USUARIO_CAMBIOINICIAL = false;    // ya la cambió ahora
            usuario.MODIFICADO_POR = usuario.USUARIO_NOMBRE;
            usuario.FECHA_MODIFICACION = ahora;

            // (Opcional) guardar historial
            try
            {
                var historial = new HISTORIAL_CONTRASENA
                {
                    HISTORIAL_ID = IdGenerator.NewId10(),
                    USUARIO_ID = usuario.USUARIO_ID,
                    HASH = newHash,
                    SALT = newSalt,
                    FECHA_CREACION = ahora,
                    CREADO_POR = usuario.USUARIO_NOMBRE,
                    ESTADO = "ACTIVO",
                    ELIMINADO = false
                };
                _context.HISTORIAL_CONTRASENA.Add(historial);
            }
            catch { /* si no existe la tabla, omite */ }

            // 2) Consumir el token
            tk.TOKEN_USADO = true;
            tk.USADO_EN = ahora;
            tk.ESTADO = "INACTIVO";
            tk.MODIFICADO_POR = usuario.USUARIO_NOMBRE;
            tk.FECHA_MODIFICACION = ahora;

            await _context.SaveChangesAsync();

            // Bitácora (opcional)
            //AgregarBitacora(usuario.USUARIO_ID, "RECOVERY_OK", "Contraseña actualizada vía email");

            TempData["Mensaje"] = "Tu contraseña fue actualizada correctamente.";
            return RedirectToAction("Index"); // vuelve al Login
        }


    }
}
