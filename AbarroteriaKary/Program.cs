using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using AbarroteriaKary.Data;
using AbarroteriaKary.Services;

var builder = WebApplication.CreateBuilder(args);

// DbContext (ya lo tiene; asegúrese del nombre real del contexto)
builder.Services.AddDbContext<KaryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Autenticación por Cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";          // redirección si no está autenticado
        options.AccessDeniedPath = "/Account/Denied";  // opcional
        options.Cookie.Name = "Kary.Auth";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

// Servicio de Login / Seguridad
builder.Services.AddScoped<ILoginService, LoginService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // <— Importante: antes de UseAuthorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();













//using AbarroteriaKary.Data;
//using AbarroteriaKary.Models;
//using Microsoft.EntityFrameworkCore;
//using System;

//var builder = WebApplication.CreateBuilder(args);


//// Conexion EFCORE
//builder.Services.AddDbContext<KaryDbContext>(option =>
//option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


//// Add services to the container.
//builder.Services.AddControllersWithViews();


//var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Home/Error");
//}
//app.UseStaticFiles();

//app.UseRouting();

//app.UseAuthorization();

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Usuarios}/{action=Index}/{id?}");

//app.Run();
