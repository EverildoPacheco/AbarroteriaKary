
//using AbarroteriaKary.Data;
//using AbarroteriaKary.Models;
//using AbarroteriaKary.Services;
//using AbarroteriaKary.Services.Auditoria;
//using AbarroteriaKary.Services.Correlativos;
//using AbarroteriaKary.Services.Reportes;   // <-- AGREGAR
//using AbarroteriaKary.Services.Security;
//using AbarroteriaKary.Services.Ventas;

//using Microsoft.AspNetCore.Authentication.Cookies; // arriba
//using Microsoft.EntityFrameworkCore;
//using Rotativa.AspNetCore;
//using System;





//var builder = WebApplication.CreateBuilder(args);


//// MVC
//builder.Services.AddControllersWithViews();

//builder.Services.AddDbContext<KaryDbContext>(option =>
//option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


//// Registro del servicio de correlativos
//builder.Services.AddScoped<ICorrelativoService, CorrelativoService>();

//builder.Services.AddHttpContextAccessor();                   // requerido por AuditoriaService
//builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();


////Correo Electronico
//builder.Services.Configure<AbarroteriaKary.Services.Mail.SmtpOptions>(
//    builder.Configuration.GetSection("Mail"));
//builder.Services.AddScoped<AbarroteriaKary.Services.Mail.IEmailSender,
//                           AbarroteriaKary.Services.Mail.SmtpEmailSender>();

////REportes
//builder.Services.AddScoped<IReporteExportService, ReporteExportService>(); // <-- AGREGAR

//// Servicios de dominio
//builder.Services.AddScoped<AbarroteriaKary.Services.Inventario.IInventarioPostingService,
//                           AbarroteriaKary.Services.Inventario.InventarioPostingService>();


//// Servicios de Notificaiones

//builder.Services.AddScoped<AbarroteriaKary.Services.INotificacionService, AbarroteriaKary.Services.NotificacionService>();

//// Servicios de Venta

//builder.Services.AddScoped<IVentaTxService, VentaTxService>();



//// PERMISOS
//builder.Services.AddMemoryCache();
//builder.Services.AddScoped<IKaryPermissionService, KaryPermissionService>();


////builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
////    .AddCookie(opt =>
////    {
////        opt.LoginPath = "/Login/Index";             // su ruta real al login
////        opt.AccessDeniedPath = "/Home/AccesoDenegado";
////        opt.SlidingExpiration = true;
////        opt.ExpireTimeSpan = TimeSpan.FromHours(8);
////    });

////builder.Services.AddAuthorization();







////Autenticacion
//builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
//    .AddCookie(options =>
//    {
//        options.LoginPath = "/Login/Index";
//        options.AccessDeniedPath = "/Login/Index";
//        options.SlidingExpiration = true;

//        // Opcionales recomendados:
//        options.ExpireTimeSpan = TimeSpan.FromHours(8);
//        options.Cookie.HttpOnly = true;
//        options.Cookie.SameSite = SameSiteMode.Lax;      // o Strict si su flujo lo permite
//        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // requiere HTTPS (ya usa UseHttpsRedirection)
//        // options.Cookie.Name = "Kary.Auth";
//    });

//builder.Services.AddAuthorization();





//// Session
//builder.Services.AddSession(options =>
//{
//    options.Cookie.Name = ".Kary.Session";
//    options.IdleTimeout = TimeSpan.FromHours(8); // en lugar de 30 min, si lo desea
//    options.Cookie.IsEssential = true;
//    options.Cookie.HttpOnly = true;
//    options.Cookie.SameSite = SameSiteMode.Lax;
//});


//// Antiforgery (opcional, ya usamos [ValidateAntiForgeryToken])
//builder.Services.AddAntiforgery();

//var app = builder.Build();
//RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa"); // usa wwwroot/Rotativa


//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Home/Error");
//    app.UseHsts();
//}

//app.UseHttpsRedirection();
//app.UseStaticFiles();

//app.UseRouting();

//app.UseSession(); // <-- importante: antes de UseEndpoints

//app.UseAuthentication();   // ★ antes de UseAuthorization
//app.UseAuthorization();

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Login}/{action=Index}/{id?}");

//app.Run();


using AbarroteriaKary.Data;
using AbarroteriaKary.Services;
using AbarroteriaKary.Services.Auditoria;
using AbarroteriaKary.Services.Correlativos;
using AbarroteriaKary.Services.Reportes;
using AbarroteriaKary.Services.Security;
using AbarroteriaKary.Services.Ventas;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// 1) MVC + DbContext
// ======================================================
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<KaryDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ======================================================
// 2) Servicios de dominio
// ======================================================
builder.Services.AddScoped<ICorrelativoService, CorrelativoService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();

// Correo
builder.Services.Configure<AbarroteriaKary.Services.Mail.SmtpOptions>(
    builder.Configuration.GetSection("Mail"));
builder.Services.AddScoped<AbarroteriaKary.Services.Mail.IEmailSender,
                           AbarroteriaKary.Services.Mail.SmtpEmailSender>();

// Reportes
builder.Services.AddScoped<IReporteExportService, ReporteExportService>();

// Inventario / Notificaciones / Ventas
builder.Services.AddScoped<AbarroteriaKary.Services.Inventario.IInventarioPostingService,
                           AbarroteriaKary.Services.Inventario.InventarioPostingService>();
builder.Services.AddScoped<AbarroteriaKary.Services.INotificacionService, AbarroteriaKary.Services.NotificacionService>();
builder.Services.AddScoped<IVentaTxService, VentaTxService>();

// Permisos (servicio + caché)
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IKaryPermissionService, KaryPermissionService>();

// ======================================================
// 3) Autenticación + Autorización (UN SOLO BLOQUE)
//    - Emite cookie para los claims (incluye ROL_ID)
//    - Paths consistentes
// ======================================================
var isDev = builder.Environment.IsDevelopment();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index";
        options.AccessDeniedPath = "/Home/AccesoDenegado"; // cree esta vista/acción
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);

        // Seguridad de la cookie
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = isDev ? CookieSecurePolicy.None
                                            : CookieSecurePolicy.Always; // en prod requiere HTTPS
        // options.Cookie.Name = "Kary.Auth"; // opcional
    });

builder.Services.AddAuthorization();

// ======================================================
// 4) Session (usted la usa en Login)
// ======================================================
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Kary.Session";
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.IsEssential = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Antiforgery (si usa AJAX: configure HeaderName en el lugar que corresponda)
// builder.Services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");

var app = builder.Build();
RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa");

// ======================================================
// 5) Pipeline (orden correcto)
// ======================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();          // antes de los endpoints
app.UseAuthentication();   // antes de Authorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}"); // si prefiere, cambie a Home/Inicio

app.Run();
