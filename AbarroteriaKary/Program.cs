
using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using Microsoft.EntityFrameworkCore;
using System;
using AbarroteriaKary.Services.Correlativos; 




var builder = WebApplication.CreateBuilder(args);





// MVC
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<KaryDbContext>(option =>
option.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


// Registro del servicio de correlativos
builder.Services.AddScoped<ICorrelativoService, CorrelativoService>();



// Session
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Kary.Session";
    options.IdleTimeout = TimeSpan.FromMinutes(30); // ajuste según su política
    options.Cookie.IsEssential = true;
});

// Antiforgery (opcional, ya usamos [ValidateAntiForgeryToken])
builder.Services.AddAntiforgery();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // <-- importante: antes de UseEndpoints

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Login}/{action=Index}/{id?}");

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
//    pattern: "{controller=Login}/{action=Index}/{id?}");

//app.Run();
