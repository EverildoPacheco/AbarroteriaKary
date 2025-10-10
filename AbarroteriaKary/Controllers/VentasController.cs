using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.ModelsPartial.Paginacion;           // PaginadoViewModel<T>
using AbarroteriaKary.Services.Auditoria;
using AbarroteriaKary.Services.Correlativos;
using AbarroteriaKary.Services.Extensions;                // ToPagedAsync extension
using AbarroteriaKary.Services.Reportes;
using AbarroteriaKary.Services.Ventas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;
using System;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;


namespace AbarroteriaKary.Controllers
{
    public class VentasController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;
        private readonly IAuditoriaService _auditoria;
        private readonly IReporteExportService _exportSvc;
        private readonly IVentaTxService _ventaTx; // negocio: FEFO/FIFO, inserciones, caja

        public VentasController(KaryDbContext context, ICorrelativoService correlativos, IAuditoriaService auditoria, IReporteExportService exportSvc, IVentaTxService ventaTx)
        {
            _context = context;
            _correlativos = correlativos;
            _auditoria = auditoria;
            _exportSvc = exportSvc;
            _ventaTx = ventaTx;

        }

        // GET: Ventas


        


        [HttpGet]
        public async Task<IActionResult> Index(
            string? estado = "ACTIVO",
            string? q = null,
            string? fDesde = null,
            string? fHasta = null,
            int page = 1,
            int pageSize = 10,
            CancellationToken ct = default)
        {
            // 1) Resolver CAJA para la barra (igual que en Create)
            var cajaIdUse = await _context.CAJA
                .AsNoTracking()
                .Where(c => !c.ELIMINADO && c.ESTADO == "ACTIVO")
                .OrderBy(c => c.CAJA_ID)
                .Select(c => c.CAJA_ID)
                .FirstOrDefaultAsync(ct);

            ViewBag.CajaId = cajaIdUse; // para el hidden CajaIdActual en la vista

            // 2) Buscar la SESIÓN ABIERTA de esa caja
            var sesionAbiertaId = await _context.CAJA_SESION
                .AsNoTracking()
                .Where(s => !s.ELIMINADO && s.CAJA_ID == cajaIdUse && s.ESTADO_SESION == "ABIERTA")
                .OrderByDescending(s => s.FECHA_APERTURA)
                .Select(s => s.SESION_ID)
                .FirstOrDefaultAsync(ct);

            // 3) Query base (solo ventas de la sesión abierta; si no hay, lista vacía)
            var query = _context.VENTA.AsNoTracking().Where(v => !v.ELIMINADO);

            if (string.IsNullOrWhiteSpace(sesionAbiertaId))
                query = query.Where(_ => false); // sin sesión abierta → vacío
            else
                query = query.Where(v => v.SESION_ID == sesionAbiertaId);

            // --- Filtros restantes sobre el MISMO 'query' ---

            // Estado
            var estadoNorm = (estado ?? "ACTIVO").Trim().ToUpperInvariant();
            if (estadoNorm is not ("ACTIVO" or "INACTIVO" or "TODOS"))
                estadoNorm = "ACTIVO";

            if (estadoNorm is "ACTIVO" or "INACTIVO")
                query = query.Where(v => v.ESTADO == estadoNorm);

            // Fechas (acepta dd/MM/yyyy o yyyy-MM-dd)
            DateTime? desde = ParseDate(fDesde);
            DateTime? hasta = ParseDate(fHasta);
            if (desde.HasValue) query = query.Where(v => v.FECHA >= desde.Value.Date);
            if (hasta.HasValue) query = query.Where(v => v.FECHA < hasta.Value.Date.AddDays(1));

            // Búsqueda
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                query = query.Where(v =>
                    EF.Functions.Like(v.VENTA_ID, term) ||
                    EF.Functions.Like(v.CLIENTE_ID, term) ||
                    EF.Functions.Like(v.USUARIO_ID, term));
            }

            // Orden + Proyección
            var proyectado = query
                .OrderByDescending(v => v.FECHA)
                .ThenBy(v => v.VENTA_ID)
                .Select(v => new VentaViewModel
                {
                    VentaId = v.VENTA_ID,
                    FechaVenta = v.FECHA,
                    ClienteId = v.CLIENTE_ID,
                    UsuarioId = v.USUARIO_ID,
                    TotalDb = v.TOTAL,
                    Auditoria = new AuditoriaViewModel
                    {
                        FechaCreacion = v.FECHA_CREACION,
                        ModificadoPor = v.MODIFICADO_POR,
                        FechaModificacion = v.FECHA_MODIFICACION
                    }
                });

            // Paginación
            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 10;

            var resultado = await proyectado.ToPagedAsync(page, pageSize, ct);

            // RouteValues para pager
            resultado.RouteValues["estado"] = estadoNorm;
            resultado.RouteValues["q"] = q;
            resultado.RouteValues["fDesde"] = desde?.ToString("yyyy-MM-dd");
            resultado.RouteValues["fHasta"] = hasta?.ToString("yyyy-MM-dd");

            // Persistencia toolbar
            ViewBag.Estado = estadoNorm;
            ViewBag.Q = q;
            ViewBag.FDesde = resultado.RouteValues["fDesde"];
            ViewBag.FHasta = resultado.RouteValues["fHasta"];

            // Caja actual (para el botón Caja / JS)
            ViewBag.CajaId = cajaIdUse;

            return View(resultado);
        }



        // === Utilidad local para parsear fechas ===
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






        // Helper para armar el nombre completo (puedes ponerlo como método estático en algún util)
        private static string BuildNombrePersona(PERSONA p) =>
            string.Join(" ", new[]
            {
        p.PERSONA_PRIMERNOMBRE,
        p.PERSONA_SEGUNDONOMBRE,
        p.PERSONA_TERCERNOMBRE,
        p.PERSONA_PRIMERAPELLIDO,
        p.PERSONA_SEGUNDOAPELLIDO,
        p.PERSONA_APELLIDOCASADA
            }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();



        [HttpGet]
        public async Task<IActionResult> Recibo(string id, bool dl = false, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // 1) Venta (header)
            var v = await _context.VENTA.AsNoTracking()
                .FirstOrDefaultAsync(x => x.VENTA_ID == id && !x.ELIMINADO, ct);
            if (v is null) return NotFound();

            // 2) Cliente (PERSONA)
            var cli = await _context.PERSONA.AsNoTracking()
                .Where(p => p.PERSONA_ID == v.CLIENTE_ID && !p.ELIMINADO)
                .Select(p => new AbarroteriaKary.ModelsPartial.ClienteUiDto
                {
                    Id = p.PERSONA_ID,
                    Nombre = (p.PERSONA_PRIMERNOMBRE + " "
                             + (p.PERSONA_SEGUNDONOMBRE ?? "") + " "
                             + (p.PERSONA_TERCERNOMBRE ?? "") + " "
                             + p.PERSONA_PRIMERAPELLIDO + " "
                             + (p.PERSONA_SEGUNDOAPELLIDO ?? "") + " "
                             + (p.PERSONA_APELLIDOCASADA ?? "")).Trim(),
                    Nit = p.PERSONA_NIT
                })
                .FirstOrDefaultAsync(ct);

            // 3) Usuario (vendedor)
            var usr = await _context.USUARIO.AsNoTracking()
                .Where(u => u.USUARIO_ID == v.USUARIO_ID && !u.ELIMINADO)
                .Select(u => new { u.USUARIO_ID, u.USUARIO_NOMBRE })
                .FirstOrDefaultAsync(ct);

            // 4) Recibo asociado (el último)
            var rec = await _context.RECIBO.AsNoTracking()
                .Where(r => r.VENTA_ID == id && !r.ELIMINADO)
                .OrderByDescending(r => r.FECHA)
                .Select(r => new { r.RECIBO_ID, r.METODO_PAGO_ID, r.MONTO, r.FECHA })
                .FirstOrDefaultAsync(ct);

            string? metodoPagoNombre = null;
            if (rec != null)
            {
                metodoPagoNombre = await _context.METODO_PAGO.AsNoTracking()
                    .Where(m => m.METODO_PAGO_ID == rec.METODO_PAGO_ID && !m.ELIMINADO)
                    .Select(m => m.METODO_PAGO_NOMBRE)
                    .FirstOrDefaultAsync(ct);
            }

            // 5) Detalle consolidado por producto (sumando lotes)
            var lineas = await _context.DETALLE_VENTA.AsNoTracking()
                .Where(d => d.VENTA_ID == id && !d.ELIMINADO)
                .Join(_context.PRODUCTO.AsNoTracking(),
                      d => d.PRODUCTO_ID,
                      p => p.PRODUCTO_ID,
                      (d, p) => new { d, p })
                .GroupBy(x => new { x.p.PRODUCTO_ID, x.p.PRODUCTO_NOMBRE, x.d.PRECIO_UNITARIO })
                .Select(g => new AbarroteriaKary.ModelsPartial.ReciboLineaVM
                {
                    ProductoId = g.Key.PRODUCTO_ID,
                    Nombre = g.Key.PRODUCTO_NOMBRE,
                    Cantidad = g.Sum(z => z.d.CANTIDAD),
                    PrecioUnitario = g.Key.PRECIO_UNITARIO
                })
                .OrderBy(l => l.Nombre)
                .ToListAsync(ct);

            // 6) ViewModel para el PDF
            var vm = new AbarroteriaKary.ModelsPartial.ReciboPdfVM
            {
                VentaId = v.VENTA_ID,
                Fecha = v.FECHA,
                ClienteId = v.CLIENTE_ID,
                ClienteNombre = cli?.Nombre ?? "Consumidor Final",
                ClienteNit = string.IsNullOrWhiteSpace(cli?.Nit) ? "CF" : cli!.Nit,
                UsuarioId = v.USUARIO_ID,
                UsuarioNombre = usr?.USUARIO_NOMBRE ?? v.USUARIO_ID,
                ReciboId = rec?.RECIBO_ID,
                MetodoPagoNombre = metodoPagoNombre ?? "N/D",
                Total = v.TOTAL,
                Lineas = lineas,

                // Info fija o tomada de tu configuración/BD:
                SucursalNombre = "Abarrotería Kary",
                SucursalDireccion = "",
                SucursalNit = ""
            };

            // 7) Render del PDF (inline para abrir en pestaña nueva)
            return new ViewAsPdf("_ReciboPdf", vm)
            {
                FileName = $"Recibo_{vm.VentaId}.pdf",
                PageSize = Size.A5,
                PageOrientation = Orientation.Portrait,
                PageMargins = new Margins { Top = 10, Right = 6, Bottom = 6, Left = 10 },
                ContentDisposition = dl ? ContentDisposition.Attachment : ContentDisposition.Inline
            };
        }










        private async Task<string> RequireUserIdAsync(CancellationToken ct)
        {
            // 1) Claims comunes (NameIdentifier / sub / custom)
            var id =
                User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                User.FindFirstValue("usuario_id") ??
                User.FindFirstValue("sub");

            if (!string.IsNullOrWhiteSpace(id))
                return id.Trim().ToUpperInvariant();

            // 2) Fallback por User.Identity.Name (usuario o correo) -> busca en USUARIO
            var login = User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(login))
            {
                var encontrado = await _context.USUARIO.AsNoTracking()
                    .Where(u => !u.ELIMINADO &&
                                (u.USUARIO_ID == login ||
                                 u.USUARIO_NOMBRE == login ||
                                 u.USUARIO_CORREO == login))
                    .Select(u => u.USUARIO_ID)
                    .FirstOrDefaultAsync(ct);

                if (!string.IsNullOrWhiteSpace(encontrado))
                    return encontrado.Trim().ToUpperInvariant();
            }

            // 3) (Opcional) Fallback de desarrollo: comenta si no lo quieres
            // var dev = Environment.GetEnvironmentVariable("KARY_DEV_USUARIO_ID");
            // if (!string.IsNullOrWhiteSpace(dev)) return dev.Trim().ToUpperInvariant();

            // 4) Sin opciones -> error
            throw new InvalidOperationException("No se pudo determinar el USUARIO_ID del usuario autenticado.");
        }


        // En VentasController (o en un BaseAppController)
        private async Task<string> GetUsuarioNombreUiAsync(string usuarioId, CancellationToken ct)
        {
            // Si tu login ya pone el nombre en claims/session, úsalo y evita ir a DB
            var fromClaims = User?.FindFirst("UsuarioNombre")?.Value
                          ?? User?.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(fromClaims))
                return fromClaims!;

            // Fallback: lee de USUARIO.USUARIO_NOMBRE (EF/LINQ, nada de SQL plano)
            var nombre = await _context.USUARIO.AsNoTracking()
                .Where(u => !u.ELIMINADO && u.USUARIO_ID == usuarioId)
                .Select(u => u.USUARIO_NOMBRE)
                .FirstOrDefaultAsync(ct);

            return string.IsNullOrWhiteSpace(nombre) ? usuarioId : nombre!;
        }








        // GET: Ventas/Create
        [HttpGet]
        public async Task<IActionResult> Create(string? cajaId = null, CancellationToken ct = default)
        {

            var usuarioId = await RequireUserIdAsync(ct);
            var usuarioNombre = await GetUsuarioNombreUiAsync(usuarioId, ct);

            // 1) Resolver CAJA_ID (puede venir en query o usar la asignada por defecto)
            var cajaIdUse = (cajaId ?? await _context.CAJA
                .AsNoTracking()
                .Where(c => !c.ELIMINADO && c.ESTADO == "ACTIVO")
                .OrderBy(c => c.CAJA_ID)
                .Select(c => c.CAJA_ID)
                .FirstOrDefaultAsync(ct))?.Trim();

            if (string.IsNullOrWhiteSpace(cajaIdUse))
            {
                TempData["SwalIcon"] = "error";
                TempData["SwalTitle"] = "Sin caja";
                TempData["SwalText"] = "No hay caja activa configurada para este usuario.";
                return RedirectToAction(nameof(Index));
            }

            // 2) Verificar SESIÓN ABIERTA
            var sesion = await _context.CAJA_SESION
                .AsNoTracking()
                .Where(s => !s.ELIMINADO && s.CAJA_ID == cajaIdUse && s.ESTADO_SESION == "ABIERTA")
                .OrderByDescending(s => s.FECHA_APERTURA)
                .FirstOrDefaultAsync(ct);

            if (sesion is null)
            {
                TempData["SwalIcon"] = "warning";
                TempData["SwalTitle"] = "Caja no aperturada";
                TempData["SwalText"] = "Debe aperturar la caja para iniciar una venta.";
                return RedirectToAction(nameof(Index));
            }

            // 3) Armar VM
            var vm = new VentaViewModel
            {
                VentaId = await _correlativos.PeekNextVentaIdAsync(ct), // preview
                SesionId = sesion.SESION_ID,
                FechaVenta = DateTime.Now,
                ClienteId = "CF",          // tu cliente de contado/final predeterminado
                UsuarioId = usuarioId,
                UsuarioNombre = usuarioNombre   // se muestra este

            };

            ViewBag.CajaId = cajaIdUse; // para el header si lo necesitas
            return View(vm);            // → Views/Ventas/Create.cshtml (abajo)
        }

        //// POST: Ventas/Confirmar (form principal con líneas + info de pago)
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Confirmar(VentaViewModel vm, VentaPagoViewModel pago, CancellationToken ct = default)
        //{
        //    var usuarioId = await RequireUserIdAsync(ct);   // <--- aquí

        //    // Normalización mínima
        //    vm.ClienteId = (vm.ClienteId ?? "").Trim().ToUpperInvariant();
        //    vm.UsuarioId = usuarioId; // seguridad: no confiar en form
        //    vm.SesionId = (vm.SesionId ?? "").Trim().ToUpperInvariant();

        //    // Validación básica de servidor
        //    if (!ModelState.IsValid || vm.Lineas == null || vm.Lineas.Count == 0)
        //    {
        //        ModelState.AddModelError("", "Agregue al menos un producto.");
        //        return View("Create", vm);
        //    }

        //    try
        //    {
        //        var usuarioNombre = await _auditoria.GetUsuarioNombreAsync();
        //        var result = await _ventaTx.ConfirmarVentaAsync(vm, pago, usuarioNombre, ct);

        //        //TempData["SavedOk"] = true;
        //        //TempData["SavedVentaId"] = result.VentaId;
        //        //TempData["SavedTotal"] = result.Total;


        //        // ...
        //        TempData["SavedOk"] = true;                  // bool está bien
        //        TempData["SavedVentaId"] = result.VentaId;
        //        // GUARDAR COMO STRING (formateado o invariante)
        //        TempData["SavedTotal"] = result.Total.ToString(CultureInfo.InvariantCulture);
        //        // opcional: también puedes guardar ya formateado para mostrar
        //        TempData["SavedTotalFmt"] = result.Total.ToString("C2", CultureInfo.GetCultureInfo("es-GT"));


        //        //return RedirectToAction(nameof(Details), new { id = result.VentaId });
        //        return RedirectToAction(nameof(Create));
        //    }
        //    catch (InvalidOperationException ex)
        //    {
        //        // Errores de negocio: caja sin sesión, stock insuficiente, etc.
        //        ModelState.AddModelError("", ex.Message);
        //        return View("Create", vm);
        //    }
        //    catch (DbUpdateException ex)
        //    {
        //        ModelState.AddModelError("", $"Error BD: {ex.GetBaseException().Message}");
        //        return View("Create", vm);
        //    }
        //}


        // Controllers/VentasController.cs  (reemplazar el método POST Confirmar)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirmar(VentaViewModel vm, VentaPagoViewModel pago, CancellationToken ct = default)
        {
            var usuarioId = await RequireUserIdAsync(ct);

            // Normalización segura
            vm.ClienteId = (vm.ClienteId ?? "").Trim().ToUpperInvariant();
            vm.UsuarioId = usuarioId;
            vm.SesionId = (vm.SesionId ?? "").Trim().ToUpperInvariant();

            // Mapeo/validación de cliente (CF o real)
            var (okCli, errCli) = await NormalizarClienteAsync(vm, ct);
            if (!okCli) ModelState.AddModelError("", errCli!);

            // Validaciones de negocio previas
            var errores = await ValidarVentaAsync(vm, pago, ct);
            foreach (var e in errores) ModelState.AddModelError("", e);

            if (!ModelState.IsValid)
                return View("Create", vm); // el resumen en la vista mostrará la lista

            try
            {
                var usuarioNombre = await _auditoria.GetUsuarioNombreAsync();
                var result = await _ventaTx.ConfirmarVentaAsync(vm, pago, usuarioNombre, ct);

                TempData["SavedOk"] = true;
                TempData["SavedVentaId"] = result.VentaId;
                TempData["SavedTotal"] = result.Total.ToString(System.Globalization.CultureInfo.InvariantCulture);
                TempData["SavedTotalFmt"] = result.Total.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("es-GT"));

                return RedirectToAction(nameof(Create));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View("Create", vm);
            }
            catch (DbUpdateException ex)
            {
                ModelState.AddModelError("", $"Error BD: {ex.GetBaseException().Message}");
                return View("Create", vm);
            }
        }









        // Controllers/VentasController.cs  (agregar al inicio de la clase)
        private const string CF_CLIENTE_ID = "CLI0000004";

        // Normaliza el ClienteId del ViewModel: si el usuario dejó "CF" o vacío,
        // lo mapeamos al CLIENTE real en BD. Valida que el cliente exista/esté activo.
        private async Task<(bool ok, string? error)> NormalizarClienteAsync(VentaViewModel vm, CancellationToken ct)
        {
            var cli = (vm.ClienteId ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(cli) || cli == "CF")
            {
                vm.ClienteId = CF_CLIENTE_ID; // <-- mapeo a su cliente CF real
                return (true, null);
            }

            var existe = await _context.CLIENTE
                .AsNoTracking()
                .AnyAsync(c => !c.ELIMINADO && c.ESTADO == "ACTIVO" && c.CLIENTE_ID == cli, ct);

            return existe ? (true, null) : (false, $"El cliente '{vm.ClienteId}' no existe o está inactivo.");
        }

        // Valida la venta *antes* de confirmar la transacción de negocio.
        // - cantidades > 0
        // - precios > 0
        // - stock suficiente (consolidado por producto)
        // - método de pago y efectivo >= total
        private async Task<List<string>> ValidarVentaAsync(VentaViewModel vm, VentaPagoViewModel pago, CancellationToken ct)
        {
            var errores = new List<string>();

            // 1) Debe existir al menos una línea
            if (vm.Lineas == null || vm.Lineas.Count == 0)
            {
                errores.Add("Debe agregar al menos un producto a la venta.");
                return errores; // lo demás no tiene sentido validarlo
            }

            // 2) Cantidades y precios
            foreach (var (item, idx) in vm.Lineas.Select((x, i) => (x, i)))
            {
                if (item.Cantidad <= 0)
                    errores.Add($"Línea {idx + 1}: la cantidad para '{item.NombreProducto ?? item.CodigoProducto}' debe ser mayor a 0.");

                if (item.PrecioUnitario <= 0)
                    errores.Add($"Línea {idx + 1}: el precio del producto '{item.NombreProducto ?? item.CodigoProducto}' debe ser mayor a Q0.00.");
            }

            // 3) Stock (consolidado por producto)
            var prodIds = vm.Lineas.Select(l => l.ProductoId).Distinct().ToList();
            var stockDict = await _context.INVENTARIO.AsNoTracking()
                .Where(i => !i.ELIMINADO && prodIds.Contains(i.PRODUCTO_ID))
                .GroupBy(i => i.PRODUCTO_ID)
                .Select(g => new { ProductoId = g.Key, Stock = (decimal?)g.Sum(x => x.STOCK_ACTUAL) ?? 0m })
                .ToDictionaryAsync(x => x.ProductoId, x => x.Stock, ct);

            foreach (var (item, idx) in vm.Lineas.Select((x, i) => (x, i)))
            {
                var stock = stockDict.TryGetValue(item.ProductoId, out var s) ? s : 0m;
                if (item.Cantidad > stock)
                    errores.Add($"Línea {idx + 1}: '{item.NombreProducto ?? item.CodigoProducto}' no tiene stock suficiente. Disponible: {stock:0.##}.");
            }

            // 4) Pago
            // Total calculado en servidor (decimal no nulo)
            var total = vm.Lineas.Sum(l => Math.Round(l.Cantidad * l.PrecioUnitario, 2));
            total = Math.Round(total, 2);

            if (total <= 0m)
                errores.Add("El total de la venta debe ser mayor a Q0.00.");

            // Método de pago requerido
            if (string.IsNullOrWhiteSpace(pago?.MetodoPagoId))
            {
                errores.Add("Debe seleccionar un método de pago.");
            }
            else
            {
                // Buscar el nombre del método para saber si es EFECTIVO
                var mpNombre = await _context.METODO_PAGO.AsNoTracking()
                    .Where(m => !m.ELIMINADO && m.METODO_PAGO_ID == pago.MetodoPagoId)
                    .Select(m => m.METODO_PAGO_NOMBRE)
                    .FirstOrDefaultAsync(ct);

                var esEfectivo = string.Equals(mpNombre?.Trim(), "EFECTIVO", StringComparison.OrdinalIgnoreCase);

                // Normalizamos nullables a decimal
                var recibido = pago.EfectivoRecibido ?? 0m;

                if (esEfectivo)
                {
                    // En EFECTIVO: recibido debe cubrir el total
                    if (recibido < total)
                        errores.Add($"El efectivo recibido (Q{recibido:0.00}) no alcanza para cubrir el total (Q{total:0.00}).");

                    // Calculamos cambio y validamos no negativo
                    var cambioBruto = Math.Round(recibido - total, 2);
                    if (cambioBruto < 0m)
                        errores.Add("El cambio no puede ser negativo.");

                    pago.CambioCalculado = Math.Max(0m, cambioBruto);
                }
                else
                {
                    // Otros métodos (tarjeta/transferencia, etc.): no exigimos efectivo; cambio = 0
                    pago.CambioCalculado = 0m;
                }
            }

            // <-- FALTABA ESTE RETURN
            return errores;
        }







        // GET: Ventas/Details/{id} (luego te doy la vista si quieres)
        [HttpGet]
        public async Task<IActionResult> Details(string id, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var v = await _context.VENTA
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.VENTA_ID == id, ct);

            if (v is null) return NotFound();

            var vm = new VentaViewModel
            {
                VentaId = v.VENTA_ID,
                FechaVenta = v.FECHA,
                ClienteId = v.CLIENTE_ID,
                UsuarioId = v.USUARIO_ID,
                TotalDb = v.TOTAL,
                SesionId = "" // opcional para Details
            };

            // (Si quieres mostrar detalle/líneas aquí, haces Join a DETALLE_VENTA)
            return View(vm);
        }





        // Clientes: PERSONA (NIT) unida a CLIENTE
        [HttpGet]
        public async Task<IActionResult> BuscarClientes(string? q, CancellationToken ct)
        {
            var term = (q ?? "").Trim();
            var baseQry = from c in _context.CLIENTE.AsNoTracking()
                          join per in _context.PERSONA.AsNoTracking()
                            on c.CLIENTE_ID equals per.PERSONA_ID
                          where !c.ELIMINADO && !per.ELIMINADO
                          select new { c, per };

            if (term.Length >= 2)
            {
                var conNombre = baseQry.Select(x => new {
                    x,
                    FullName = (x.per.PERSONA_PRIMERNOMBRE ?? "") + " " +
                               (x.per.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                               (x.per.PERSONA_TERCERNOMBRE ?? "") + " " +
                               (x.per.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                               (x.per.PERSONA_SEGUNDOAPELLIDO ?? "") + " " +
                               (x.per.PERSONA_APELLIDOCASADA ?? "")
                });

                baseQry = conNombre
                    .Where(y => EF.Functions.Like(y.FullName, $"%{term}%") ||
                                (y.x.per.PERSONA_NIT != null && EF.Functions.Like(y.x.per.PERSONA_NIT, $"%{term}%")))
                    .Select(y => y.x);
            }

            var items = await baseQry
                .OrderBy(x => x.per.PERSONA_PRIMERNOMBRE).ThenBy(x => x.per.PERSONA_PRIMERAPELLIDO)
                .Take(10)
                .Select(x => new {
                    clienteId = x.c.CLIENTE_ID,
                    nombre = (x.per.PERSONA_PRIMERNOMBRE ?? "") + " " + (x.per.PERSONA_PRIMERAPELLIDO ?? ""),
                    nit = x.per.PERSONA_NIT,
                    info = x.c.ESTADO
                })
                .ToListAsync(ct);

            // Opción CF al inicio
            //items.Insert(0, new { clienteId = "CF", nombre = "Consumidor Final", nit = "CF", info = "Sin factura" });

            // HTML de cada sugerencia (lo consume el JS como item._html)
            var itemsHtml = items.Select(x => new {
                x.clienteId,
                x.nombre,
                x.nit,
                x.info,
                _html = $@"
                <a class='list-group-item list-group-item-action d-flex gap-3 align-items-center'>
                  <div class='flex-grow-1'>
                    <div class='fw-semibold'>{System.Net.WebUtility.HtmlEncode(x.nombre)}</div>
                    <small class='text-muted'>{System.Net.WebUtility.HtmlEncode(x.nit ?? "")} — {System.Net.WebUtility.HtmlEncode(x.info ?? "")}</small>
                  </div>
                </a>".Replace("<a ", $"<a data-pick='{System.Text.Json.JsonSerializer.Serialize(new { x.clienteId, x.nombre, x.nit })}' ")
            });

            return Json(new { ok = true, items = itemsHtml });
        }








        // =======================================================
        // Buscar PRODUCTOS en INVENTARIO (consolidado por producto).
        // GET: /Ventas/BuscarInventario?q=...
        // Devuelve lista para autocompletar (si luego lo quieres).
        // =======================================================
        // Productos desde INVENTARIO (consolidado + PVP vigente)
        [HttpGet]
        public async Task<IActionResult> BuscarInventario(string? q, CancellationToken ct)
        {
            var term = (q ?? "").Trim();
            if (term.Length < 2) return Json(new { ok = true, items = Array.Empty<object>() });

            var baseQry = from i in _context.INVENTARIO.AsNoTracking()
                          join p in _context.PRODUCTO.AsNoTracking() on i.PRODUCTO_ID equals p.PRODUCTO_ID
                          where !i.ELIMINADO && !p.ELIMINADO &&
                                (EF.Functions.Like(p.PRODUCTO_NOMBRE, $"%{term}%") ||
                                 EF.Functions.Like(p.PRODUCTO_CODIGO, $"%{term}%"))
                          select new { i, p };

            var lista = await baseQry
                .GroupBy(x => new { x.p.PRODUCTO_ID, x.p.PRODUCTO_NOMBRE, x.p.PRODUCTO_CODIGO, x.p.PRODUCTO_IMG })
                .Select(g => new {
                    ProductoId = g.Key.PRODUCTO_ID,
                    Nombre = g.Key.PRODUCTO_NOMBRE,
                    Codigo = g.Key.PRODUCTO_CODIGO,
                    ImagenUrl = g.Key.PRODUCTO_IMG,
                    Stock = g.Sum(y => (decimal?)y.i.STOCK_ACTUAL) ?? 0m,
                    ProxVence = g.Min(y => y.i.FECHA_VENCIMIENTO) // DateOnly?
                })
                .OrderBy(x => x.Nombre)
                .Take(10)
                .ToListAsync(ct);

            var hoy = DateTime.Today;
            var prodIds = lista.Select(x => x.ProductoId).ToList();

            var precios = await _context.PRECIO_HISTORICO.AsNoTracking()
                .Where(ph => prodIds.Contains(ph.PRODUCTO_ID) && ph.DESDE <= hoy && (ph.HASTA == null || ph.HASTA > hoy))
                .GroupBy(ph => ph.PRODUCTO_ID)
                .Select(g => new {
                    ProductoId = g.Key,
                    Precio = g.OrderByDescending(ph => ph.DESDE).Select(ph => ph.PRECIO).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.ProductoId, x => x.Precio, ct);

            var items = lista.Select(x =>
            {
                // DateOnly? -> DateTime? para formatear
                var prox = x.ProxVence.HasValue
                    ? new DateTime(x.ProxVence.Value.Year, x.ProxVence.Value.Month, x.ProxVence.Value.Day)
                    : (DateTime?)null;

                var precio = precios.TryGetValue(x.ProductoId, out var pr) ? pr : 0m;
                var proxTxt = prox?.ToString("dd/MM/yyyy") ?? "-";

                // Escapes para HTML
                var img = System.Net.WebUtility.HtmlEncode(x.ImagenUrl ?? "/img/no-image.png");
                var nombre = System.Net.WebUtility.HtmlEncode(x.Nombre);
                var codigo = System.Net.WebUtility.HtmlEncode(x.Codigo ?? x.ProductoId);

                // data que lee el JS al hacer click
                var pickJson = System.Text.Json.JsonSerializer.Serialize(new { productoId = x.ProductoId });

                // === Tarjetita con imagen, nombre, badge de precio y fila de info (código, stock, vence) ===
                var html = $@"
                <a class='list-group-item list-group-item-action' data-pick='{pickJson}'>
                  <div class='d-flex gap-3'>
                    <img src='{img}' onerror='this.src=""/img/no-image.png""' class='ak-thumb' alt='ref'>
                    <div class='flex-grow-1'>
                      <div class='d-flex justify-content-between align-items-center'>
                        <div class='fw-semibold'>{nombre}</div>
                        <span class='ak-price-badge'>Q {precio:0.00}</span>
                      </div>
                      <div class='small text-muted mt-1'>
                        <span class='me-3'>{codigo}</span>
                        <span class='me-3'>Stock: {x.Stock:0.##}</span>
                        <span class='me-3'>Vence: {proxTxt}</span>
                      </div>
                    </div>
                  </div>
                </a>";

                return new { productoId = x.ProductoId, _html = html };
            }).ToList();

            return Json(new
            {
                ok = true,
                items = items.Select(i => new { i.productoId, i._html })
            });

        }









        // =======================================================
        // Preview rápido (JSON). Opcional si ya usarás el PARCIAL.
        // GET: /Ventas/ProductoPreview?productoId=...
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> ProductoPreview(string productoId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(productoId))
                return Json(new { ok = false, message = "Producto requerido." });

            var q = await (from i in _context.INVENTARIO.AsNoTracking()
                           join p in _context.PRODUCTO.AsNoTracking()
                                 on i.PRODUCTO_ID equals p.PRODUCTO_ID
                           where !i.ELIMINADO && !p.ELIMINADO && i.PRODUCTO_ID == productoId
                           select new { i, p })
                          .GroupBy(x => new { x.p.PRODUCTO_ID, x.p.PRODUCTO_NOMBRE, x.p.PRODUCTO_CODIGO, x.p.PRODUCTO_IMG })
                          .Select(g => new
                          {
                              ProductoId = g.Key.PRODUCTO_ID,
                              Nombre = g.Key.PRODUCTO_NOMBRE,
                              Codigo = g.Key.PRODUCTO_CODIGO,
                              ImagenUrl = g.Key.PRODUCTO_IMG,
                              Stock = g.Sum(y => (decimal?)y.i.STOCK_ACTUAL) ?? 0m,
                              ProxVence = g.Min(y => y.i.FECHA_VENCIMIENTO) // DateOnly?
                          })
                          .FirstOrDefaultAsync(ct);

            if (q == null)
                return Json(new { ok = false, message = "Producto no encontrado en inventario." });

            var hoy = DateTime.Today;
            var pvp = await _context.PRECIO_HISTORICO.AsNoTracking()
                         .Where(ph => ph.PRODUCTO_ID == productoId
                                   && ph.DESDE <= hoy
                                   && (ph.HASTA == null || ph.HASTA > hoy)
                                   && !ph.ELIMINADO)
                         .OrderByDescending(ph => ph.DESDE)
                         .Select(ph => (decimal?)ph.PRECIO)
                         .FirstOrDefaultAsync(ct) ?? 0m;

            return Json(new
            {
                ok = true,
                item = new
                {
                    productoId = q.ProductoId,
                    nombreProducto = q.Nombre,
                    codigoProducto = q.Codigo,
                    imagenUrl = q.ImagenUrl,
                    stockDisponible = q.Stock,
                    precioVigente = pvp,
                    proximoVencimiento = q.ProxVence.HasValue ? q.ProxVence.Value.ToString("yyyy-MM-dd") : null
                }
            });
        }



        // =======================================================
        // PARCIAL del modal para agregar producto a la venta
        // GET: /Ventas/AgregarProductoModal?productoId=...
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> AgregarProductoModal(string productoId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(productoId))
                return BadRequest("Producto requerido.");

            var prod = await _context.PRODUCTO.AsNoTracking()
                .Where(p => !p.ELIMINADO && p.PRODUCTO_ID == productoId)
                .Select(p => new { p.PRODUCTO_ID, p.PRODUCTO_CODIGO, p.PRODUCTO_NOMBRE, p.PRODUCTO_IMG })
                .FirstOrDefaultAsync(ct);
            if (prod == null) return NotFound("Producto no encontrado.");

            var invs = await _context.INVENTARIO.AsNoTracking()
                .Where(i => !i.ELIMINADO && i.PRODUCTO_ID == productoId && i.STOCK_ACTUAL > 0)
                .Select(i => new { i.STOCK_ACTUAL, i.FECHA_VENCIMIENTO })
                .ToListAsync(ct);

            var stock = invs.Sum(i => (decimal)i.STOCK_ACTUAL);
            DateTime? proxVenc = invs.Where(i => i.FECHA_VENCIMIENTO.HasValue)
                .Select(i => new DateTime(i.FECHA_VENCIMIENTO!.Value.Year, i.FECHA_VENCIMIENTO!.Value.Month, i.FECHA_VENCIMIENTO!.Value.Day))
                .OrderBy(d => d).FirstOrDefault();

            var hoy = DateTime.Now;
            var pvp = await _context.PRECIO_HISTORICO.AsNoTracking()
                .Where(h => !h.ELIMINADO && h.PRODUCTO_ID == productoId &&
                            (h.HASTA == null || h.HASTA > hoy) && h.DESDE <= hoy)
                .OrderByDescending(h => h.DESDE)
                .Select(h => (decimal?)h.PRECIO)
                .FirstOrDefaultAsync(ct) ?? 0m;

            var vm = new AbarroteriaKary.ModelsPartial.VentaDetalleItemVM
            {
                ProductoId = prod.PRODUCTO_ID,
                CodigoProducto = prod.PRODUCTO_CODIGO ?? prod.PRODUCTO_ID,
                NombreProducto = prod.PRODUCTO_NOMBRE,
                ImagenUrl = prod.PRODUCTO_IMG,
                StockDisponible = stock,
                ProximoVencimiento = proxVenc,
                PrecioUnitario = pvp,
                Cantidad = 1
            };

            return PartialView("~/Views/Ventas/_AgregarProductoModal.cshtml", vm);
        }







        [HttpGet]
        public async Task<IActionResult> PagoModal(decimal total, CancellationToken ct)
        {
            // Métodos de pago activos
            var items = await _context.METODO_PAGO
                .AsNoTracking()
                .Where(m => !m.ELIMINADO && m.ESTADO == "ACTIVO")
                .OrderBy(m => m.METODO_PAGO_NOMBRE)
                .Select(m => new SelectListItem
                {
                    Value = m.METODO_PAGO_ID,
                    Text = m.METODO_PAGO_NOMBRE
                })
                .ToListAsync(ct);

            var vm = new AbarroteriaKary.ModelsPartial.VentaPagoViewModel
            {
                TotalPagar = total,
                Metodos = items
            };

            return PartialView("~/Views/Ventas/_PagoVentaModal.cshtml", vm);
        }










    }


}
