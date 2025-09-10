
using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using AbarroteriaKary.Services;
using AbarroteriaKary.Services.Auditoria;
using AbarroteriaKary.Services.Correlativos;
using Microsoft.AspNetCore.Authentication.Cookies; // arriba
using Microsoft.EntityFrameworkCore;
using System;
using Rotativa.AspNetCore;
using AbarroteriaKary.Services.Reportes;   // <-- AGREGAR





var builder = WebApplication.CreateBuilder(args);





// MVC
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<KaryDbContext>(option =>
option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


// Registro del servicio de correlativos
builder.Services.AddScoped<ICorrelativoService, CorrelativoService>();

builder.Services.AddHttpContextAccessor();                   // requerido por AuditoriaService
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();


//Correo Electronico
builder.Services.Configure<AbarroteriaKary.Services.Mail.SmtpOptions>(
    builder.Configuration.GetSection("Mail"));
builder.Services.AddScoped<AbarroteriaKary.Services.Mail.IEmailSender,
                           AbarroteriaKary.Services.Mail.SmtpEmailSender>();

//REportes
builder.Services.AddScoped<IReporteExportService, ReporteExportService>(); // <-- AGREGAR



//Autenticacion
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Index";
        options.AccessDeniedPath = "/Login/Index";
        options.SlidingExpiration = true;

        // Opcionales recomendados:
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;      // o Strict si su flujo lo permite
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // requiere HTTPS (ya usa UseHttpsRedirection)
        // options.Cookie.Name = "Kary.Auth";
    });

builder.Services.AddAuthorization();





// Session
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Kary.Session";
    options.IdleTimeout = TimeSpan.FromHours(8); // en lugar de 30 min, si lo desea
    options.Cookie.IsEssential = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});


// Antiforgery (opcional, ya usamos [ValidateAntiForgeryToken])
builder.Services.AddAntiforgery();

var app = builder.Build();
RotativaConfiguration.Setup(app.Environment.WebRootPath, "Rotativa"); // usa wwwroot/Rotativa


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // <-- importante: antes de UseEndpoints

app.UseAuthentication();   // ★ antes de UseAuthorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

app.Run();

