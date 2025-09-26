using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.ModelsPartial.Paginacion;           // PaginadoViewModel<T>
using AbarroteriaKary.Services.Auditoria;
using AbarroteriaKary.Services.Correlativos;
using AbarroteriaKary.Services.Extensions;                // ToPagedAsync extension
using AbarroteriaKary.Services.Reportes;
using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.InkML;
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
using AbarroteriaKary.Services.Pedidos;


namespace AbarroteriaKary.Controllers
{
    public class PedidosController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;
        private readonly IAuditoriaService _auditoria;
        private readonly IReporteExportService _exportSvc;
        public PedidosController(KaryDbContext context, ICorrelativoService correlativos, IAuditoriaService auditoria, IReporteExportService exportSvc)
        {
            _context = context;
            _correlativos = correlativos;
            _auditoria = auditoria;
            _exportSvc = exportSvc;

        }




        [HttpGet]
        public async Task<IActionResult> Index(string? estado, string? q = null, string? fDesde = null, string? fHasta = null,int page = 1, int pageSize = 10, CancellationToken ct = default)
        {
            // 0) Normaliza estado lógico
            var estadoNorm = (estado ?? "ACTIVO").Trim().ToUpperInvariant();
            if (estadoNorm is not ("ACTIVO" or "INACTIVO" or "TODOS")) estadoNorm = "ACTIVO";

            // 1) Rango de fechas (acepta dd/MM/yyyy o yyyy-MM-dd)
            static DateTime? ParseDate(string? input)
            {
                if (string.IsNullOrWhiteSpace(input)) return null;
                var formats = new[] { "dd/MM/yyyy", "yyyy-MM-dd" };
                if (DateTime.TryParseExact(input, formats, CultureInfo.GetCultureInfo("es-GT"),
                    DateTimeStyles.None, out var d)) return d;
                if (DateTime.TryParse(input, out d)) return d;
                return null;
            }
            DateTime? desde = ParseDate(fDesde);
            DateTime? hasta = ParseDate(fHasta);

            // 2) Base query con LEFT JOINs (PROVEEDOR, PERSONA, ESTADO_PEDIDO)
            var baseQry =
                from ped in _context.PEDIDO.AsNoTracking().Where(p => !p.ELIMINADO)
                join prov0 in _context.PROVEEDOR.AsNoTracking() on ped.PROVEEDOR_ID equals prov0.PROVEEDOR_ID into gprov
                from prov in gprov.DefaultIfEmpty()
                join per0 in _context.PERSONA.AsNoTracking() on prov.PROVEEDOR_ID equals per0.PERSONA_ID into gper
                from per in gper.DefaultIfEmpty()
                join est0 in _context.ESTADO_PEDIDO.AsNoTracking() on ped.ESTADO_PEDIDO_ID equals est0.ESTADO_PEDIDO_ID into gest
                from est in gest.DefaultIfEmpty()
                select new { ped, prov, per, est };

            // 3) Filtro por estado lógico
            if (estadoNorm is "ACTIVO" or "INACTIVO")
                baseQry = baseQry.Where(x => x.ped.ESTADO == estadoNorm);

            // 4) Búsqueda libre (ID pedido, empresa, NIT, correo, teléfono)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                baseQry = baseQry.Where(x =>
                    EF.Functions.Like(x.ped.PEDIDO_ID, term) ||
                    (x.prov != null && EF.Functions.Like(x.prov.EMPRESA, term)) ||
                    (x.per != null && (
                        (x.per.PERSONA_NIT != null && EF.Functions.Like(x.per.PERSONA_NIT, term)) ||
                        (x.per.PERSONA_CORREO != null && EF.Functions.Like(x.per.PERSONA_CORREO, term)) ||
                        (x.per.PERSONA_TELEFONOMOVIL != null && EF.Functions.Like(x.per.PERSONA_TELEFONOMOVIL, term))
                    ))
                );
            }

            // 5) Rango por FECHA_PEDIDO (inclusivo)
            if (desde.HasValue) baseQry = baseQry.Where(x => x.ped.FECHA_PEDIDO >= desde.Value.Date);
            if (hasta.HasValue) baseQry = baseQry.Where(x => x.ped.FECHA_PEDIDO < hasta.Value.Date.AddDays(1));

            // 6) Precomputar conteos de líneas por pedido
            var detCountsQry =
                from d in _context.DETALLE_PEDIDO.AsNoTracking().Where(d => !d.ELIMINADO)
                group d by d.PEDIDO_ID into g
                select new { PedidoId = g.Key, Cant = g.Count() };

            // 7) Join con conteos y proyección al VM (ANTES de paginar)
            var proyectado =
                from x in baseQry
                join dc0 in detCountsQry on x.ped.PEDIDO_ID equals dc0.PedidoId into gdc
                from dc in gdc.DefaultIfEmpty()
                orderby x.ped.PEDIDO_ID
                select new PedidoListItemViewModel
                {
                    PedidoId = x.ped.PEDIDO_ID,
                    Empresa = x.prov != null ? x.prov.EMPRESA : "",

                    //FechaPedidoTxt = x.ped.FECHA_PEDIDO != null

                    //    ? x.ped.FECHA_PEDIDO!.Value.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("es-GT"))
                    //    : null,
                    FechaPedidoTxt = x.ped.FECHA_PEDIDO.ToString("dd/MM/yyyy"),


                    //FechaPedidoTxt = x.ped.FECHA_PEDIDO.HasValue
                    //    ? $"{x.ped.FECHA_PEDIDO:dd/MM/yyyy}"
                    //    : null,

                    FechaEntregaTxt = x.ped.FECHA_ENTREGA_ESTIMADA.HasValue
                        ? $"{x.ped.FECHA_ENTREGA_ESTIMADA:dd/MM/yyyy}"
                        : null,
                    EstadoNombre = x.est != null ? x.est.ESTADO_PEDIDO_NOMBRE : null,
                    Lineas = dc != null ? dc.Cant : 0,
                    ESTADO = x.ped.ESTADO
                };

            // 8) Paginación
            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 10;

            var resultado = await proyectado.ToPagedAsync(page, pageSize, ct);

            // 9) RouteValues + toolbar
            resultado.RouteValues["estado"] = estadoNorm;
            resultado.RouteValues["q"] = q;
            resultado.RouteValues["fDesde"] = desde?.ToString("yyyy-MM-dd");
            resultado.RouteValues["fHasta"] = hasta?.ToString("yyyy-MM-dd");

            ViewBag.Estado = estadoNorm;
            ViewBag.Q = q;
            ViewBag.FDesde = resultado.RouteValues["fDesde"];
            ViewBag.FHasta = resultado.RouteValues["fHasta"];

            return View(resultado);
        }





        // === Utilidad local para parsear fechas (mismo helper) ===
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









[HttpGet]
    public async Task<IActionResult> Details(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta id.");
        id = id.Trim().ToUpperInvariant();

        // Encabezado con joins para mostrar textos legibles
        var hdr = await (
            from ped in _context.PEDIDO.AsNoTracking()
                .Where(p => !p.ELIMINADO && p.PEDIDO_ID == id)
            join prov0 in _context.PROVEEDOR.AsNoTracking() on ped.PROVEEDOR_ID equals prov0.PROVEEDOR_ID into gprov
            from prov in gprov.DefaultIfEmpty()
            join est0 in _context.ESTADO_PEDIDO.AsNoTracking() on ped.ESTADO_PEDIDO_ID equals est0.ESTADO_PEDIDO_ID into gest
            from est in gest.DefaultIfEmpty()
            select new
            {
                ped,
                Empresa = prov != null ? prov.EMPRESA : "",
                EstadoNombre = est != null ? est.ESTADO_PEDIDO_NOMBRE : ""
            }
        ).FirstOrDefaultAsync(ct);

        if (hdr == null) return NotFound("Pedido no encontrado.");

        // Detalle con datos del producto para mostrar en la tabla
        var lineas = await (
            from d in _context.DETALLE_PEDIDO.AsNoTracking()
                .Where(x => !x.ELIMINADO && x.PEDIDO_ID == id)
            join pr0 in _context.PRODUCTO.AsNoTracking() on d.PRODUCTO_ID equals pr0.PRODUCTO_ID into gpr
            from pr in gpr.DefaultIfEmpty()
            orderby d.DETALLE_PEDIDO_ID
            select new PedidoDetalleItemVM
            {
                DetallePedidoId = d.DETALLE_PEDIDO_ID,   // string
                ProductoId = d.PRODUCTO_ID,         // string
                CodigoProducto = pr != null ? pr.PRODUCTO_CODIGO : "",
                NombreProducto = pr != null ? pr.PRODUCTO_NOMBRE : "",
                DescripcionProducto = pr != null ? pr.PRODUCTO_DESCRIPCION : "",
                ImagenUrl = pr != null ? pr.PRODUCTO_IMG : "",
                Cantidad = d.CANTIDAD,            // decimal/int en VM (lo muestras)
                PrecioPedido = d.PRECIO_PEDIDO,
                PrecioVenta = d.PRECIO_VENTA,
                FechaVencimiento = d.FECHA_VENCIMIENTO    // DateOnly?
            }
        ).ToListAsync(ct);

        // Armamos el VM que ya usas en Create/Edit (solo lo mostraremos)
        var vm = new PedidoViewModel
        {
            PedidoId = hdr.ped.PEDIDO_ID,
            ProveedorId = hdr.ped.PROVEEDOR_ID,
            FechaPedido = hdr.ped.FECHA_PEDIDO,    // DateTime
            FechaPosibleEntrega = hdr.ped.FECHA_ENTREGA_ESTIMADA.HasValue
                                    ? new DateTime(hdr.ped.FECHA_ENTREGA_ESTIMADA.Value.Year,
                                                    hdr.ped.FECHA_ENTREGA_ESTIMADA.Value.Month,
                                                    hdr.ped.FECHA_ENTREGA_ESTIMADA.Value.Day)
                                    : (DateTime?)null,
            EstadoPedidoId = hdr.ped.ESTADO_PEDIDO_ID,

            Observacion = hdr.ped.OBSERVACIONES,

            EstadoActivo = (hdr.ped.ESTADO ?? "ACTIVO").Trim().ToUpper() == "ACTIVO",
            Lineas = lineas
        };

        // Textos legibles para la vista (no vamos a renderizar selects)
        ViewBag.ProveedorEmpresa = hdr.Empresa;
        ViewBag.EstadoNombre = hdr.EstadoNombre;

        return View(vm);
    }






    //-------------------------REPORTES---------------------------
    [AllowAnonymous]
        [HttpGet]
        public IActionResult PdfFooter()
        {
            // Pie estándar compartido
            return View("~/Views/Shared/Reportes/_PdfFooter.cshtml");
        }

        // GET: /Pedidos/PedidoPdf/PED000123
        [HttpGet("Pedidos/PedidosPdf/{id}")]
        public async Task<IActionResult> PedidoPdf(string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta id.");

            id = id.Trim().ToUpper();

            // ===== Encabezado (PEDIDO + PROVEEDOR + PERSONA + ESTADO_PEDIDO) =====
            var hdr = await (
                from ped in _context.PEDIDO.AsNoTracking()
                    .Where(p => !p.ELIMINADO && p.PEDIDO_ID == id)
                join prov0 in _context.PROVEEDOR on ped.PROVEEDOR_ID equals prov0.PROVEEDOR_ID into gprov
                from prov in gprov.DefaultIfEmpty()
                join per0 in _context.PERSONA on prov.PROVEEDOR_ID equals per0.PERSONA_ID into gper
                from per in gper.DefaultIfEmpty()
                join est0 in _context.ESTADO_PEDIDO on ped.ESTADO_PEDIDO_ID equals est0.ESTADO_PEDIDO_ID into gest
                from est in gest.DefaultIfEmpty()
                select new PedidoReporteHeaderViewModel
                {
                    PedidoId = ped.PEDIDO_ID,
                    Empresa = prov.EMPRESA,
                    ProveedorNit = per.PERSONA_NIT,
                    ProveedorDireccion = per.PERSONA_DIRECCION,
                    ProveedorTelefono = (per.PERSONA_TELEFONOMOVIL ?? per.PERSONA_TELEFONOCASA),
                    ProveedorCorreo = per.PERSONA_CORREO,

                    FechaGenerada = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                    // FECHA_PEDIDO en tu modelo es DateTime? (según tu código)
                    //FechaPedido = (ped.FECHA_PEDIDO == default || ped.FECHA_PEDIDO == null)
                    //                ? null : ped.FECHA_PEDIDO!.Value.ToString("dd/MM/yyyy"),
                    FechaPedido = ped.FECHA_PEDIDO.ToString("dd/MM/yyyy"),

                    // FECHA_ENTREGA_ESTIMADA en DB es DateOnly? -> to string
                    FechaEntregaEstimada = ped.FECHA_ENTREGA_ESTIMADA.HasValue
                                    ? $"{ped.FECHA_ENTREGA_ESTIMADA:dd/MM/yyyy}"
                                    : null,
                    EstadoNombre = est.ESTADO_PEDIDO_NOMBRE,
                    Usuario = GetUsuarioActual() // tu helper de Empleados
                }
            ).FirstOrDefaultAsync(ct);

            if (hdr == null) return NotFound("Pedido no encontrado.");

            // ===== Detalle (DETALLE_PEDIDO + PRODUCTO) =====
            var det = await (
                from d in _context.DETALLE_PEDIDO.AsNoTracking()
                    .Where(x => !x.ELIMINADO && x.PEDIDO_ID == id)
                join pr0 in _context.PRODUCTO on d.PRODUCTO_ID equals pr0.PRODUCTO_ID into gpr
                from pr in gpr.DefaultIfEmpty()
                orderby d.DETALLE_PEDIDO_ID
                select new PedidoReporteDetalleViewModel
                {
                    CorrelativoDetalle = d.DETALLE_PEDIDO_ID,
                    CodigoProducto = pr.PRODUCTO_CODIGO,
                    //CodigoProducto = pr.PRODUCTO_CODIGO,

                    NombreProducto = pr.PRODUCTO_NOMBRE,
                    DescripcionProducto = pr.PRODUCTO_DESCRIPCION,
                    ImagenUrl = pr.PRODUCTO_IMG,
                    Cantidad = d.CANTIDAD
                }
            ).ToListAsync(ct);

            // ==== ViewData tipado (patrón de Empleados) ====
            var pdfViewData = new ViewDataDictionary<IEnumerable<PedidoReporteDetalleViewModel>>(
                new EmptyModelMetadataProvider(),
                new ModelStateDictionary())
            {
                Model = det ?? new List<PedidoReporteDetalleViewModel>()
            };
            // Pasamos el header por ViewBag (o ViewData), como haces en otros módulos
            pdfViewData["Header"] = hdr;

            // ==== PDF con Rotativa ====
            var footerUrl = Url.Action("PdfFooter", "Pedidos", null, Request.Scheme);

            var pdf = new ViewAsPdf("~/Views/Pedidos/PedidosPdf.cshtml")
            {
                ViewData = pdfViewData,
                PageSize = Size.Letter,
                PageOrientation = Orientation.Portrait,
                PageMargins = new Margins(10, 10, 18, 12),
                CustomSwitches =
                    "--disable-smart-shrinking --print-media-type " +
                    $"--footer-html \"{footerUrl}\" --footer-spacing 4 " +
                    "--load-error-handling ignore --load-media-error-handling ignore"
            };

            //var bytes = await pdf.BuildFile(ControllerContext);
            //Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            //Response.Headers["Pragma"] = "no-cache";
            //Response.Headers["Expires"] = "0";
            //return File(bytes, "application/pdf");
            var bytes = await pdf.BuildFile(ControllerContext);

            // sug. nombre de archivo
            var fileName = $"Pedidos_{id}.pdf";

            // hace que el navegador lo muestre en el visor
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";

            return File(bytes, "application/pdf");
        }

        // (mismo helper que ya tienes en Empleados)
        private string GetUsuarioActual()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                if (!string.IsNullOrWhiteSpace(User.Identity!.Name))
                    return User.Identity.Name!;
                string[] keys = {
            ClaimTypes.Name, ClaimTypes.GivenName, ClaimTypes.Email,
            "name","usuario","user","UserName","UsuarioNombre"
        };
                foreach (var k in keys)
                {
                    var c = User.FindFirst(k);
                    if (c != null && !string.IsNullOrWhiteSpace(c.Value))
                        return c.Value;
                }
            }
            var ses = HttpContext?.Session?.GetString("UsuarioNombre")
                   ?? HttpContext?.Session?.GetString("UserName");
            if (!string.IsNullOrWhiteSpace(ses)) return ses!;
            return "Admin";
        }












        // =========================================================
        // GET: Pedidos/Create
        // - No reserva ID (usa Peek para mostrar)
        // - Carga combos Proveedor/Estado (ESTADO_PEDIDO)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            var vm = new PedidoViewModel
            {
                PedidoId = await _correlativos.PeekNextPedidosIdAsync(ct),
                FechaPedido = DateTime.Today,
                EstadoActivo = true
            };

            //await CargarCombosAsync(vm, ct);

            await CargarCombosAsync(vm, ct, "create");


            // Preseleccionar "BORRADOR" si existe
            //vm.EstadoPedidoId ??= await _context.ESTADO_PEDIDO
            //    .AsNoTracking()
            //    .Where(e => !e.ELIMINADO && e.ESTADO == "ACTIVO" && e.ESTADO_PEDIDO_NOMBRE == "BORRADOR")
            //    .Select(e => e.ESTADO_PEDIDO_ID)
            //    .FirstOrDefaultAsync(ct);

            vm.EstadoPedidoId ??= (await _context.ESTADO_PEDIDO.AsNoTracking()
                .Where(e => !e.ELIMINADO && e.ESTADO == "ACTIVO" && e.ESTADO_PEDIDO_NOMBRE == EstadosPedido.BORRADOR)
                .Select(e => e.ESTADO_PEDIDO_ID)
                .FirstOrDefaultAsync(ct));

            return View(vm);
        }





        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PedidoViewModel vm, CancellationToken ct)
        {
            // ===== 1) Validaciones por DataAnnotations/IValidatableObject =====
            if (!ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(vm.PedidoId))
                    vm.PedidoId = await _correlativos.PeekNextPedidosIdAsync(ct);

                //await CargarCombosAsync(vm, ct);
                await CargarCombosAsync(vm, ct, "create");

                return View(vm);
            }

            // ===== 2) Validaciones de negocio (encabezado) =====
            // Proveedor ACTIVO
            bool proveedorOk = await _context.PROVEEDOR
                .AsNoTracking()
                .AnyAsync(p => !p.ELIMINADO && p.ESTADO == "ACTIVO" && p.PROVEEDOR_ID == vm.ProveedorId, ct);
            if (!proveedorOk)
                ModelState.AddModelError(nameof(vm.ProveedorId), "El proveedor no existe o está inactivo.");

            // Estado de pedido ACTIVO
            bool estadoOk = await _context.ESTADO_PEDIDO
                .AsNoTracking()
                .AnyAsync(e => !e.ELIMINADO && e.ESTADO == "ACTIVO" && e.ESTADO_PEDIDO_ID == vm.EstadoPedidoId, ct);
            if (!estadoOk)
                ModelState.AddModelError(nameof(vm.EstadoPedidoId), "El estado de pedido no existe o está inactivo.");

            // Fechas coherentes (por claridad, aunque ya lo validas en IValidatableObject)
            if (vm.FechaPedido.HasValue && vm.FechaPosibleEntrega.HasValue &&
                vm.FechaPosibleEntrega.Value.Date < vm.FechaPedido.Value.Date)
            {
                ModelState.AddModelError(nameof(vm.FechaPosibleEntrega),
                    "La fecha posible de entrega no puede ser anterior a la fecha del pedido.");
            }


            //--------------------------------------

            var nombreDestino = EstadosPedido.Normalizar(await GetNombreEstadoByIdAsync(vm.EstadoPedidoId, ct));
            if (nombreDestino == EstadosPedido.PENDIENTE)
            {
                if (!vm.FechaPosibleEntrega.HasValue)
                    ModelState.AddModelError(nameof(vm.FechaPosibleEntrega), "La fecha posible de entrega es obligatoria para PENDIENTE.");
                else if (vm.FechaPedido.HasValue && vm.FechaPosibleEntrega.Value.Date < vm.FechaPedido.Value.Date)
                    ModelState.AddModelError(nameof(vm.FechaPosibleEntrega), "La fecha posible de entrega no puede ser anterior a la fecha del pedido.");
            }

            if (!ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(vm.PedidoId))
                    vm.PedidoId = await _correlativos.PeekNextPedidosIdAsync(ct);
                await CargarCombosAsync(vm, ct, "create");
                return View(vm);
            }


            //--------------------------------------








            // ===== 3) Hardening del detalle: limpiar, consolidar, validar =====
            vm.Lineas = vm.Lineas?
                .Where(l => l is not null && !string.IsNullOrWhiteSpace(l.ProductoId))
                .Select(l => new PedidoDetalleItemVM
                {
                    ProductoId = (l.ProductoId ?? string.Empty).Trim().ToUpper(),
                    Cantidad = l.Cantidad
                })
                .GroupBy(l => l.ProductoId)
                .Select(g => new PedidoDetalleItemVM
                {
                    ProductoId = g.Key,
                    Cantidad = g.Sum(x => x.Cantidad)
                })
                .Where(l => l.Cantidad > 0)
                .ToList()
                ?? new List<PedidoDetalleItemVM>();

            if (vm.Lineas.Count == 0)
                ModelState.AddModelError(nameof(vm.Lineas), "Debe agregar al menos un producto al pedido.");

            // Cantidades enteras positivas
            for (int i = 0; i < vm.Lineas.Count; i++)
                _ = ToPositiveInt(vm.Lineas[i].Cantidad, nameof(PedidoDetalleItemVM.Cantidad), ModelState, $"Lineas[{i}].");

            // (Opcional recomendado) existencia de cada producto ACTIVO
            for (int i = 0; i < vm.Lineas.Count; i++)
            {
                var prodId = vm.Lineas[i].ProductoId;
                bool productoOk = await _context.PRODUCTO
                    .AsNoTracking()
                    .AnyAsync(p => !p.ELIMINADO && p.ESTADO == "ACTIVO" && p.PRODUCTO_ID == prodId, ct);

                if (!productoOk)
                    ModelState.AddModelError($"Lineas[{i}].ProductoId", $"El producto {prodId} no existe o está inactivo.");
            }

            if (!ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(vm.PedidoId))
                    vm.PedidoId = await _correlativos.PeekNextPedidosIdAsync(ct);

                await CargarCombosAsync(vm, ct);
                return View(vm);
            }

            // ===== 4) Inserción en BD (transacción) =====
            var ahora = DateTime.Now;
            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync();

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                // 4.1) Pedir correlativo definitivo del PEDIDO
                var pedidoId = await _correlativos.NextPedidosIdAsync(ct);
                vm.PedidoId = pedidoId;

                var estadoLogico = vm.EstadoActivo ? "ACTIVO" : "INACTIVO";

                // 4.2) Encabezado
                var pedido = new PEDIDO
                {
                    PEDIDO_ID = pedidoId,
                    PROVEEDOR_ID = vm.ProveedorId,

                    // Ajusta a tu entidad (DateTime? vs DateOnly? y el nombre real de la columna)
                    FECHA_PEDIDO = vm.FechaPedido ?? ahora,                 // si tu entidad es DateTime?
                    FECHA_ENTREGA_ESTIMADA = ToDateOnly(vm.FechaPosibleEntrega), // si es DateOnly?
                                                                                 // Si tu entidad usa FECHA_ENTREGA_POSIBLE:
                                                                                 // FECHA_ENTREGA_POSIBLE = ToDateOnly(vm.FechaPosibleEntrega),

                    ESTADO_PEDIDO_ID = vm.EstadoPedidoId,
                    OBSERVACIONES = string.IsNullOrWhiteSpace(vm.Observacion) ? null : vm.Observacion.Trim(),
                    //OBSERVACIONES = vm.Observacion,

                    ESTADO = estadoLogico,
                    ELIMINADO = false,
                    CREADO_POR = usuarioNombre,
                    FECHA_CREACION = ahora
                };
                // 1) Guardas encabezado
                _context.Add(pedido);
                await _context.SaveChangesAsync(ct);

                // 2) Rango de IDs para detalle
                var idsRango = await _correlativos.NextDetallePedidosRangeAsync(vm.Lineas.Count, ct);
                if (idsRango.Count != vm.Lineas.Count)
                {
                    await tx.RollbackAsync(ct);
                    ModelState.AddModelError(string.Empty, "No se pudo reservar el bloque de IDs para detalle.");
                    await CargarCombosAsync(vm, ct);
                    return View(vm);
                }

                // 3) Todas las líneas
                for (int i = 0; i < vm.Lineas.Count; i++)
                {
                    var linea = vm.Lineas[i];
                    int cantidadEntera = ToPositiveInt(linea.Cantidad, nameof(PedidoDetalleItemVM.Cantidad), ModelState, $"Lineas[{i}].");
                    if (!ModelState.IsValid)
                    {
                        await tx.RollbackAsync(ct);
                        await CargarCombosAsync(vm, ct);
                        return View(vm);
                    }

                    var det = new DETALLE_PEDIDO
                    {
                        DETALLE_PEDIDO_ID = idsRango[i],
                        PEDIDO_ID = pedidoId,
                        PRODUCTO_ID = linea.ProductoId,
                        CANTIDAD = cantidadEntera,
                        ESTADO = "ACTIVO",
                        ELIMINADO = false,
                        CREADO_POR = usuarioNombre,
                        FECHA_CREACION = ahora
                    };
                    _context.Add(det);
                }

                // 4) Un solo SaveChanges para todo el detalle
                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);


                // ... después de Commit y ANTES del Redirect:
                TempData["PedidoCreadoOk"] = true;
                TempData["PedidoCreadoId"] = pedido.PEDIDO_ID;

                //return RedirectToAction(nameof(Details), new { id = pedido.PEDIDO_ID });
                // En vez de Details:
                return RedirectToAction(nameof(Create));

            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError(string.Empty, $"Error BD: {ex.GetBaseException().Message}");

                if (string.IsNullOrWhiteSpace(vm.PedidoId))
                    vm.PedidoId = await _correlativos.PeekNextPedidosIdAsync(ct);

                await CargarCombosAsync(vm, ct);
                return View(vm);
            }
        }




        // =========================================================
        // Helpers
        // =========================================================

        /// <summary>
        /// Carga combos de Proveedores y Estados (ESTADO_PEDIDO).
        /// </summary>
        //private async Task CargarCombosAsync(PedidoViewModel vm, CancellationToken ct)
        //{
        //    vm.Proveedores = await _context.PROVEEDOR
        //        .AsNoTracking()
        //        .Where(p => !p.ELIMINADO && p.ESTADO == "ACTIVO")
        //        .OrderBy(p => p.EMPRESA)                  // ← usar EMPRESA
        //        .Select(p => new SelectListItem
        //        {
        //            Value = p.PROVEEDOR_ID,
        //            Text = p.EMPRESA                      // ← mostrar EMPRESA
        //        })
        //        .ToListAsync(ct);

        //    vm.Estados = await _context.ESTADO_PEDIDO
        //        .AsNoTracking()
        //        .Where(e => !e.ELIMINADO && e.ESTADO == "ACTIVO")
        //        .OrderBy(e => e.ESTADO_PEDIDO_NOMBRE)
        //        .Select(e => new SelectListItem
        //        {
        //            Value = e.ESTADO_PEDIDO_ID,
        //            Text = e.ESTADO_PEDIDO_NOMBRE
        //        })
        //        .ToListAsync(ct);
        //}



        private async Task<string?> GetNombreEstadoByIdAsync(string estadoId, CancellationToken ct)
        {
            return await _context.ESTADO_PEDIDO
                .AsNoTracking()
                .Where(e => !e.ELIMINADO && e.ESTADO == "ACTIVO" && e.ESTADO_PEDIDO_ID == estadoId)
                .Select(e => e.ESTADO_PEDIDO_NOMBRE)
                .FirstOrDefaultAsync(ct);
        }



        private async Task<string?> GetIdEstadoByNombreAsync(string nombre, CancellationToken ct)
        {
            // CERRADO ⇄ FINALIZADO
            var n = EstadosPedido.Normalizar(nombre);
            return await _context.ESTADO_PEDIDO
                .AsNoTracking()
                .Where(e => !e.ELIMINADO && e.ESTADO == "ACTIVO")
                .Where(e => e.ESTADO_PEDIDO_NOMBRE == n || (n == EstadosPedido.CERRADO && e.ESTADO_PEDIDO_NOMBRE == "FINALIZADO"))
                .Select(e => e.ESTADO_PEDIDO_ID)
                .FirstOrDefaultAsync(ct);
        }




        private static readonly Dictionary<string, int> _ordenEstados = new(StringComparer.OrdinalIgnoreCase)
        {
            [EstadosPedido.BORRADOR] = 1,
            [EstadosPedido.PENDIENTE] = 2,
            [EstadosPedido.ENVIADO] = 3,
            [EstadosPedido.RECIBIDO] = 4,
            [EstadosPedido.ANULADO] = 5,
            // CERRADO/FINALIZADO NO se lista en el combo de edición, pero lo mostramos si es el estado actual (deshabilitado)
        };








        /// <summary>
        /// Carga combos de Estados según pantalla.
        /// Create: BORRADOR, PENDIENTE.
        /// Edit: BORRADOR, PENDIENTE, ENVIADO, RECIBIDO, ANULADO. (Nunca CERRADO)
        /// </summary>
                private async Task CargarCombosAsync(PedidoViewModel vm, CancellationToken ct, string modo = "create", string? estadoActualNombre = null)
                {
            // Proveedores (su mismo criterio)
            vm.Proveedores = await _context.PROVEEDOR
                .AsNoTracking()
                .Where(p => !p.ELIMINADO && p.ESTADO == "ACTIVO")
                //.OrderBy(p => p.PROVEEDOR_ID)
                //.Select(p => new SelectListItem { Value = p.PROVEEDOR_ID, Text = p.PROVEEDOR_ID })
                //.ToListAsync(ct);

                .OrderBy(p => p.EMPRESA)                  // ← usar EMPRESA
                .Select(p => new SelectListItem
                {
                    Value = p.PROVEEDOR_ID,
                    Text = p.EMPRESA                      // ← mostrar EMPRESA
                })
                .ToListAsync(ct);


            // ===== Catálogo de estados =====
            var estados = await _context.ESTADO_PEDIDO
                .AsNoTracking()
                .Where(e => !e.ELIMINADO && e.ESTADO == "ACTIVO")
                .Select(e => new { e.ESTADO_PEDIDO_ID, e.ESTADO_PEDIDO_NOMBRE })
                .ToListAsync(ct);

            // Estado actual (por id)
            var estadoActual = estados.FirstOrDefault(e => e.ESTADO_PEDIDO_ID == vm.EstadoPedidoId);
            var nombreActualNorm = EstadosPedido.Normalizar(estadoActual?.ESTADO_PEDIDO_NOMBRE);

            // Visibles por pantalla
            var visibles = string.Equals(modo, "create", StringComparison.OrdinalIgnoreCase)
                ? new HashSet<string>(new[] { EstadosPedido.BORRADOR, EstadosPedido.PENDIENTE }, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(new[] { EstadosPedido.BORRADOR, EstadosPedido.PENDIENTE, EstadosPedido.ENVIADO, EstadosPedido.RECIBIDO, EstadosPedido.ANULADO }, StringComparer.OrdinalIgnoreCase);

            // Helper de ranking
            int Rank(string nombreNorm) => _ordenEstados.TryGetValue(nombreNorm, out var r) ? r : int.MaxValue;

            // Construir items visibles y ORDENAR por nuestro ranking fijo
            var itemsVisibles = estados
                .Select(e => new
                {
                    e.ESTADO_PEDIDO_ID,
                    Nombre = e.ESTADO_PEDIDO_NOMBRE,
                    NombreNorm = EstadosPedido.Normalizar(e.ESTADO_PEDIDO_NOMBRE)
                })
                .Where(e => visibles.Contains(e.NombreNorm))
                .OrderBy(e => Rank(e.NombreNorm))
                .ThenBy(e => e.Nombre) // desempate estable, por si acaso
                .Select(e => new SelectListItem
                {
                    Value = e.ESTADO_PEDIDO_ID,
                    Text = e.Nombre,
                    Selected = (e.ESTADO_PEDIDO_ID == vm.EstadoPedidoId)
                })
                .ToList();

            // Si el estado actual NO está en visibles (p.ej. CERRADO/FINALIZADO), lo agregamos deshabilitado y seleccionado al inicio.
            if (!string.IsNullOrWhiteSpace(vm.EstadoPedidoId) && itemsVisibles.All(i => i.Value != vm.EstadoPedidoId))
            {
                var eAct = estadoActual;
                if (eAct != null)
                {
                    itemsVisibles.Insert(0, new SelectListItem
                    {
                        Value = eAct.ESTADO_PEDIDO_ID,
                        Text = eAct.ESTADO_PEDIDO_NOMBRE, // puede ser FINALIZADO
                        Selected = true,
                        Disabled = true
                    });
                }
            }

            // Preselección BORRADOR SOLO si venimos de Create y no hay estado en VM
            if (string.Equals(modo, "create", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(vm.EstadoPedidoId))
            {
                var idBorrador = estados
                    .Where(e => EstadosPedido.Normalizar(e.ESTADO_PEDIDO_NOMBRE) == EstadosPedido.BORRADOR)
                    .Select(e => e.ESTADO_PEDIDO_ID)
                    .FirstOrDefault();

                vm.EstadoPedidoId = idBorrador ?? itemsVisibles.FirstOrDefault()?.Value;

                // Marcar seleccionado
                foreach (var it in itemsVisibles) it.Selected = (it.Value == vm.EstadoPedidoId);
            }

            vm.Estados = itemsVisibles;
            vm.EstadoPedidoNombre = estadoActual?.ESTADO_PEDIDO_NOMBRE;

            // (Opcional) bandera para la vista
            ViewBag.EsCerrado = (nombreActualNorm == EstadosPedido.CERRADO);
        }




        // ================== Búsqueda de productos (JSON) ==================
        // GET: /Pedidos/BuscarProductos?q=tortrix&top=20
        [HttpGet]
        public async Task<IActionResult> BuscarProductos(string? q, int top = 20, CancellationToken ct = default)
        {
            // Sanitizar parámetros
            q ??= string.Empty;
            q = q.Trim();

            // Capar top para no sobrecargar
            top = (top <= 0) ? 20 : Math.Min(top, 50);

            // Base query: productos activos y no eliminados
            var baseQuery = _context.PRODUCTO
                .AsNoTracking()
                .Where(p => !p.ELIMINADO && p.ESTADO == "ACTIVO");

            // Filtro por código / nombre / descripción (LIKE %q%)
            if (!string.IsNullOrEmpty(q))
            {
                var qLike = $"%{q}%";
                baseQuery = baseQuery.Where(p =>
                    EF.Functions.Like(p.PRODUCTO_ID, qLike) || // Agregado hoy 24092025
                    EF.Functions.Like(p.PRODUCTO_CODIGO, qLike) ||
                    EF.Functions.Like(p.PRODUCTO_NOMBRE, qLike) ||
                    EF.Functions.Like(p.PRODUCTO_DESCRIPCION, qLike));
            }

            // Proyección a DTO para el autocompletado
            var lista = await baseQuery
                .OrderBy(p => p.PRODUCTO_NOMBRE)
                .Take(top)
                .Select(p => new ProductoBusquedaItemVM
                {
                    ProductoId = p.PRODUCTO_ID,
                    CodigoProducto = p.PRODUCTO_CODIGO,
                    NombreProducto = p.PRODUCTO_NOMBRE,
                    DescripcionProducto = p.PRODUCTO_DESCRIPCION,
                    // Ajuste este campo al nombre real en su DB si difiere:
                    // Ejemplos comunes: PRODUCTO_IMAGEN_URL, IMAGEN_URL, FOTO_URL, etc.
                    ImagenUrl = p.PRODUCTO_IMG,
                    // Info extra opcional: UM / stock si su modelo lo tiene
                    // Ejemplos: p.UNIDAD_MEDIDA.UNIDAD_MEDIDA_NOMBRE, p.STOCK_ACTUAL
                    InfoRapida = null
                })
                .ToListAsync(ct);

            return Ok(lista);
        }




        // ================== Obtener producto por Id (JSON) ==================
        // GET: /Pedidos/ProductoPorId?id=PROD00001
        [HttpGet]
        public async Task<IActionResult> ProductoPorId(string? id, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Debe especificar el id del producto.");

            id = id.Trim().ToUpper();

            var p = await _context.PRODUCTO
                .AsNoTracking()
                .Where(x => !x.ELIMINADO && x.ESTADO == "ACTIVO" && x.PRODUCTO_ID == id)
                .Select(x => new
                {
                    x.PRODUCTO_ID,
                    x.PRODUCTO_CODIGO,
                    x.PRODUCTO_NOMBRE,
                    x.PRODUCTO_DESCRIPCION,
                    // Ajuste el nombre del campo de imagen si es distinto:
                    ImagenUrl = x.PRODUCTO_IMG,
                    // Si maneja precio de compra en producto, úselo como sugerencia inicial:
                    //PrecioCompra = x.PRECIO_COMPRA // <-- si no existe, deje 0m
                })
                .FirstOrDefaultAsync(ct);

            if (p == null) return NotFound("Producto no encontrado o inactivo.");

            var vm = new ProductoSeleccionadoVM
            {
                ProductoId = p.PRODUCTO_ID,
                CodigoProducto = p.PRODUCTO_CODIGO ?? string.Empty,
                NombreProducto = p.PRODUCTO_NOMBRE ?? string.Empty,
                DescripcionProducto = p.PRODUCTO_DESCRIPCION,
                ImagenUrl = p.ImagenUrl,
                Cantidad = 1,                 // default en panel
                //PrecioUnitario = p.PrecioCompra is decimal d && d > 0 ? d : 0m
            };

            return Ok(vm);
        }



        // DateTime? -> DateOnly?
        private static DateOnly? ToDateOnly(DateTime? dt)
            => dt.HasValue ? DateOnly.FromDateTime(dt.Value) : (DateOnly?)null;

        // decimal -> int (entero positivo). Reporta error en ModelState si no cumple.
        private static int ToPositiveInt(decimal value, string fieldName, ModelStateDictionary modelState, string keyPrefix = "")
        {
            if (value <= 0)
            {
                modelState.AddModelError($"{keyPrefix}{fieldName}", "Debe ser mayor que cero.");
                return 0;
            }
            if (value != Math.Truncate(value))
            {
                modelState.AddModelError($"{keyPrefix}{fieldName}", "Debe ser un número entero.");
                return 0;
            }
            return Convert.ToInt32(value);
        }





        // PedidosController.cs  (solo lo nuevo/ajustado para Edit)
        [HttpGet]
        public async Task<IActionResult> Edit(string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta id.");
            id = id.Trim().ToUpper();

            // Cabecera
            var ped = await _context.PEDIDO
                .AsNoTracking()
                .FirstOrDefaultAsync(p => !p.ELIMINADO && p.PEDIDO_ID == id, ct);

            if (ped == null) return NotFound("Pedido no encontrado.");



            // ... dentro del Edit (GET) donde haces la query de 'lineas'
            var lineas = await (
                from d in _context.DETALLE_PEDIDO.AsNoTracking()
                    .Where(x => !x.ELIMINADO && x.PEDIDO_ID == id)
                join pr0 in _context.PRODUCTO.AsNoTracking()
                    on d.PRODUCTO_ID equals pr0.PRODUCTO_ID into gpr
                from pr in gpr.DefaultIfEmpty()
                orderby d.DETALLE_PEDIDO_ID
                select new PedidoDetalleItemVM
                {
                    DetallePedidoId = d.DETALLE_PEDIDO_ID,   // string
                    ProductoId = d.PRODUCTO_ID,
                    Cantidad = d.CANTIDAD,
                    PrecioPedido = d.PRECIO_PEDIDO,
                    PrecioVenta = d.PRECIO_VENTA,
                    FechaVencimiento = d.FECHA_VENCIMIENTO,   // DateOnly?

                    // >>> estos 4 son los que la vista muestra en las columnas:

                    CodigoProducto = pr != null ? pr.PRODUCTO_CODIGO : "",
                    //PedidoId = pr != null ? pr.PRODUCTO_ID : "",

                    NombreProducto = pr != null ? pr.PRODUCTO_NOMBRE : "",
                    DescripcionProducto = pr != null ? pr.PRODUCTO_DESCRIPCION : "",
                    ImagenUrl = pr != null ? pr.PRODUCTO_IMG : ""
                }
            ).ToListAsync(ct);


            //var vm = new PedidoViewModel
            //{
            //    PedidoId = ped.PEDIDO_ID,
            //    ProveedorId = ped.PROVEEDOR_ID,
            //    FechaPedido = ped.FECHA_PEDIDO, // DB: DateTime NOT NULL
            //    FechaPosibleEntrega = ped.FECHA_ENTREGA_ESTIMADA.HasValue
            //        ? new DateTime(ped.FECHA_ENTREGA_ESTIMADA.Value.Year, ped.FECHA_ENTREGA_ESTIMADA.Value.Month, ped.FECHA_ENTREGA_ESTIMADA.Value.Day)
            //        : (DateTime?)null,
            //    EstadoPedidoId = ped.ESTADO_PEDIDO_ID,
            //    Observacion = ped.OBSERVACIONES,
            //    EstadoActivo = (ped.ESTADO ?? "ACTIVO").Trim().ToUpper() == "ACTIVO",
            //    Lineas = lineas
            //};



            var estadoActualNombre = await _context.ESTADO_PEDIDO
                .AsNoTracking()
                .Where(e => !e.ELIMINADO && e.ESTADO == "ACTIVO" && e.ESTADO_PEDIDO_ID == ped.ESTADO_PEDIDO_ID)
                .Select(e => e.ESTADO_PEDIDO_NOMBRE)
                .FirstOrDefaultAsync(ct);

            var vm = new PedidoViewModel
            {
                PedidoId = ped.PEDIDO_ID,
                ProveedorId = ped.PROVEEDOR_ID,
                FechaPedido = ped.FECHA_PEDIDO, // DateTime NOT NULL
                FechaPosibleEntrega = ped.FECHA_ENTREGA_ESTIMADA.HasValue
                    ? new DateTime(ped.FECHA_ENTREGA_ESTIMADA.Value.Year, ped.FECHA_ENTREGA_ESTIMADA.Value.Month, ped.FECHA_ENTREGA_ESTIMADA.Value.Day)
                    : (DateTime?)null,
                EstadoPedidoId = ped.ESTADO_PEDIDO_ID,
                EstadoPedidoNombre = estadoActualNombre,
                Observacion = ped.OBSERVACIONES,
                EstadoActivo = (ped.ESTADO ?? "ACTIVO").Trim().ToUpper() == "ACTIVO",
                Lineas = lineas
            };



            await CargarCombosAsync(vm, ct, "edit", estadoActualNombre);
            return View(vm);

            //await CargarCombosAsync(vm, ct);
            //return View(vm);
        }







        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, PedidoViewModel vm, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta id.");
            id = id.Trim().ToUpper();
            if (id != vm.PedidoId?.Trim().ToUpper()) return BadRequest("Inconsistencia de id.");

            if (!ModelState.IsValid)
            {
                await CargarCombosAsync(vm, ct, "edit");
                return View(vm);
            }

            var ped = await _context.PEDIDO.FirstOrDefaultAsync(p => !p.ELIMINADO && p.PEDIDO_ID == id, ct);
            if (ped == null) return NotFound("Pedido no encontrado.");

            var detalleDb = await _context.DETALLE_PEDIDO
                .Where(d => !d.ELIMINADO && d.PEDIDO_ID == id)
                .OrderBy(d => d.DETALLE_PEDIDO_ID)
                .ToListAsync(ct);

            // === Normalizaciones/validaciones de líneas (igual a su lógica existente) ===
            vm.Lineas = (vm.Lineas ?? new List<PedidoDetalleItemVM>())
                .Where(l => l != null && !string.IsNullOrWhiteSpace(l.ProductoId))
                .Select(l => new PedidoDetalleItemVM
                {
                    DetallePedidoId = string.IsNullOrWhiteSpace(l.DetallePedidoId) ? null : l.DetallePedidoId!.Trim(),
                    ProductoId = (l.ProductoId ?? "").Trim().ToUpper(),
                    Cantidad = l.Cantidad,
                    PrecioPedido = l.PrecioPedido ?? 0m,
                    PrecioVenta = l.PrecioVenta ?? 0m,
                    FechaVencimiento = l.FechaVencimiento
                })
                .ToList();

            if (vm.Lineas.Count == 0)
                ModelState.AddModelError(nameof(vm.Lineas), "Debe agregar al menos un producto.");

            for (int i = 0; i < vm.Lineas.Count; i++)
                _ = ToPositiveInt(vm.Lineas[i].Cantidad, nameof(PedidoDetalleItemVM.Cantidad), ModelState, $"Lineas[{i}].");

            // Precios no negativos
            for (int i = 0; i < vm.Lineas.Count; i++)
            {
                if (vm.Lineas[i].PrecioPedido < 0)
                    ModelState.AddModelError($"Lineas[{i}].PrecioPedido", "Precio compra no puede ser negativo.");
                if (vm.Lineas[i].PrecioVenta < 0)
                    ModelState.AddModelError($"Lineas[{i}].PrecioVenta", "Precio venta no puede ser negativo.");
            }

            // Fechas coherentes
            if (vm.FechaPedido.HasValue && vm.FechaPosibleEntrega.HasValue &&
                vm.FechaPosibleEntrega.Value.Date < vm.FechaPedido.Value.Date)
            {
                ModelState.AddModelError(nameof(vm.FechaPosibleEntrega),
                    "La fecha posible de entrega no puede ser anterior a la fecha del pedido.");
            }

            // Proveedor y Estado existen/ACTIVOS
            bool proveedorOk = await _context.PROVEEDOR.AsNoTracking()
                .AnyAsync(p => !p.ELIMINADO && p.ESTADO == "ACTIVO" && p.PROVEEDOR_ID == vm.ProveedorId, ct);
            if (!proveedorOk)
                ModelState.AddModelError(nameof(vm.ProveedorId), "El proveedor no existe o está inactivo.");

            bool estadoOk = await _context.ESTADO_PEDIDO.AsNoTracking()
                .AnyAsync(e => !e.ELIMINADO && e.ESTADO == "ACTIVO" && e.ESTADO_PEDIDO_ID == vm.EstadoPedidoId, ct);
            if (!estadoOk)
                ModelState.AddModelError(nameof(vm.EstadoPedidoId), "El estado de pedido no existe o está inactivo.");

            if (!ModelState.IsValid)
            {
                await CargarCombosAsync(vm, ct, "edit");
                return View(vm);
            }

            // ================== TRANSICIÓN ORIGEN → DESTINO ==================
            var nombreOrigen = EstadosPedido.Normalizar(await GetNombreEstadoByIdAsync(ped.ESTADO_PEDIDO_ID, ct));
            var nombreDestino = EstadosPedido.Normalizar(await GetNombreEstadoByIdAsync(vm.EstadoPedidoId, ct));

            // Seguridad: CERRADO no debe seleccionarse manualmente
            if (nombreDestino == EstadosPedido.CERRADO)
            {
                ModelState.AddModelError(nameof(vm.EstadoPedidoId), "El estado CERRADO es automático al aplicar inventario.");
                await CargarCombosAsync(vm, ct, "edit");
                return View(vm);
            }

            // Por ahora NO usamos roles. Cuando los tenga, descomente y use isAdmin donde corresponda.
            // bool isAdmin = User.IsInRole("Admin");
            bool isAdmin = false;

            // Reapertura desde ANULADO: la controlaremos con el checkbox "Activo" por ahora.
            // Si el usuario marca "Activo", interpretamos que quiere reabrir.
            bool quiereReactivar = vm.EstadoActivo;

            // Matriz de transiciones permitidas (sin roles activos por ahora)
            bool permitido = (nombreOrigen, nombreDestino) switch
            {
                (var o, var d) when o == d => true, // edición sin cambio de estado

                (EstadosPedido.BORRADOR, EstadosPedido.PENDIENTE) => true,
                (EstadosPedido.BORRADOR, EstadosPedido.ANULADO) => true,

                (EstadosPedido.PENDIENTE, EstadosPedido.BORRADOR) => true,
                (EstadosPedido.PENDIENTE, EstadosPedido.ENVIADO) => true,
                (EstadosPedido.PENDIENTE, EstadosPedido.ANULADO) => true,

                (EstadosPedido.ENVIADO, EstadosPedido.RECIBIDO) => true,
                (EstadosPedido.ENVIADO, EstadosPedido.ANULADO) => true,

                // (Opcional) volver de ENVIADO → PENDIENTE (cuando tenga roles, habilite para Admin)
                // (EstadosPedido.ENVIADO,   EstadosPedido.PENDIENTE) => isAdmin,

                // Reapertura desde ANULADO (sin roles): permitida solo si "Activo" viene marcado.
                (EstadosPedido.ANULADO, EstadosPedido.BORRADOR) => quiereReactivar /* || isAdmin */,
                (EstadosPedido.ANULADO, EstadosPedido.PENDIENTE) => quiereReactivar /* || isAdmin */,

                _ => false
            };

            // Bloqueos extra: nunca permitir acciones “hacia atrás” si ya estaba CERRADO/FINALIZADO
            if (nombreOrigen == EstadosPedido.CERRADO)
            {
                // Evita ANULAR un CERRADO
                if (nombreDestino == EstadosPedido.ANULADO)
                    permitido = false;

                // Evita reabrir un CERRADO
                if (nombreDestino == EstadosPedido.BORRADOR || nombreDestino == EstadosPedido.PENDIENTE)
                    permitido = false;
            }

            if (!permitido)
            {
                ModelState.AddModelError(nameof(vm.EstadoPedidoId), $"Transición no permitida: {nombreOrigen} → {nombreDestino}.");
                await CargarCombosAsync(vm, ct, "edit");
                return View(vm);
            }

            // ================== REGLAS POR DESTINO ==================
            switch (nombreDestino)
            {
                case EstadosPedido.PENDIENTE:
                    // Ya NO exigimos FechaPosibleEntrega aquí.
                    // (Si viene informada, la coherencia con FechaPedido ya la valida arriba.)
                    break;

                case EstadosPedido.ENVIADO:
                    // Aquí SÍ es obligatoria la fecha posible de entrega.
                    if (!vm.FechaPosibleEntrega.HasValue)
                        ModelState.AddModelError(nameof(vm.FechaPosibleEntrega),
                            "La fecha posible de entrega es obligatoria para ENVIADO.");
                    break;

                case EstadosPedido.RECIBIDO:
                    // Seguimos usando FECHA_ENTREGA_ESTIMADA como fecha efectiva de recepción
                    if (!vm.FechaPosibleEntrega.HasValue)
                        ModelState.AddModelError(nameof(vm.FechaPosibleEntrega),
                            "Debe especificar la fecha (FECHA ENTREGA ESTIMADA) para marcar como RECIBIDO.");
                    break;

                case EstadosPedido.ANULADO:
                    if (nombreOrigen == EstadosPedido.CERRADO)
                        ModelState.AddModelError(nameof(vm.EstadoPedidoId),
                            "No se puede anular un pedido que ya fue cerrado.");
                    break;
            }

            // Reglas adicionales para REAPERTURA (origen = ANULADO)
            if (nombreOrigen == EstadosPedido.ANULADO &&
                (nombreDestino == EstadosPedido.BORRADOR || nombreDestino == EstadosPedido.PENDIENTE))
            {
                // Sin roles: exigimos que el usuario haya marcado "Activo".
                if (!quiereReactivar)
                    ModelState.AddModelError(nameof(vm.EstadoActivo), "Para reabrir un pedido anulado debe activarlo (marque 'Activo').");

                // Si reabre directamente a PENDIENTE, validar fecha igual que en PENDIENTE.
                if (nombreDestino == EstadosPedido.PENDIENTE)
                {
                    if (!vm.FechaPosibleEntrega.HasValue)
                        ModelState.AddModelError(nameof(vm.FechaPosibleEntrega), "La fecha posible de entrega es obligatoria al reabrir a PENDIENTE.");
                    else if (vm.FechaPedido.HasValue && vm.FechaPosibleEntrega.Value.Date < vm.FechaPedido.Value.Date)
                        ModelState.AddModelError(nameof(vm.FechaPosibleEntrega), "La fecha posible de entrega no puede ser anterior a la fecha del pedido.");
                }
            }

            if (!ModelState.IsValid)
            {
                await CargarCombosAsync(vm, ct, "edit");
                return View(vm);
            }


            // ===== Persistencia (transacción) =====
            var ahora = DateTime.Now;
            var usuario = await _auditoria.GetUsuarioNombreAsync();

            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            try
            {
                // 1) Cabecera
                ped.PROVEEDOR_ID = vm.ProveedorId!;
                ped.OBSERVACIONES = string.IsNullOrWhiteSpace(vm.Observacion) ? null : vm.Observacion.Trim();
                ped.ESTADO = vm.EstadoActivo ? "ACTIVO" : "INACTIVO";
                ped.FECHA_PEDIDO = vm.FechaPedido ?? ped.FECHA_PEDIDO;

                // FECHA_ENTREGA_ESTIMADA = fecha planificada / recibida (según transición)
                ped.FECHA_ENTREGA_ESTIMADA = ToDateOnly(vm.FechaPosibleEntrega);

                // Trazabilidad por auditoría (sin nuevas columnas):
                ped.MODIFICADO_POR = usuario;
                ped.FECHA_MODIFICACION = ahora;

                // 2) Detalle: su UP-SERT actual (editar/insertar/eliminar lógico)
                var dbMap = detalleDb.ToDictionary(d => d.DETALLE_PEDIDO_ID);
                var idsVmPresentes = new HashSet<string>();

                // 2.a) Editar existentes
                foreach (var l in vm.Lineas.Where(x => !string.IsNullOrWhiteSpace(x.DetallePedidoId)))
                {
                    var key = l.DetallePedidoId!.Trim();
                    if (!dbMap.TryGetValue(key, out var d)) continue;

                    d.PRODUCTO_ID = l.ProductoId;
                    d.CANTIDAD = ToPositiveInt(l.Cantidad, nameof(PedidoDetalleItemVM.Cantidad), ModelState);
                    d.PRECIO_PEDIDO = l.PrecioPedido ?? 0m;
                    d.PRECIO_VENTA = l.PrecioVenta ?? 0m;
                    d.SUBTOTAL = d.CANTIDAD * (d.PRECIO_PEDIDO ?? 0m);
                    d.FECHA_VENCIMIENTO = l.FechaVencimiento.HasValue ? l.FechaVencimiento.Value : (DateOnly?)null;

                    d.MODIFICADO_POR = usuario;
                    d.FECHA_MODIFICACION = ahora;
                    idsVmPresentes.Add(d.DETALLE_PEDIDO_ID);
                }

                // 2.b) Insertar nuevas
                var nuevas = vm.Lineas.Where(x => string.IsNullOrWhiteSpace(x.DetallePedidoId)).ToList();
                if (nuevas.Count > 0)
                {
                    var idsRango = await _correlativos.NextDetallePedidosRangeAsync(nuevas.Count, ct);
                    if (idsRango.Count != nuevas.Count)
                    {
                        await tx.RollbackAsync(ct);
                        ModelState.AddModelError(string.Empty, "No se pudo reservar el bloque de IDs para detalle.");
                        await CargarCombosAsync(vm, ct, "edit", nombreOrigen);
                        return View(vm);
                    }

                    for (int i = 0; i < nuevas.Count; i++)
                    {
                        var n = nuevas[i];
                        int cant = ToPositiveInt(n.Cantidad, nameof(PedidoDetalleItemVM.Cantidad), ModelState);

                        var det = new DETALLE_PEDIDO
                        {
                            DETALLE_PEDIDO_ID = idsRango[i],
                            PEDIDO_ID = ped.PEDIDO_ID,
                            PRODUCTO_ID = n.ProductoId,
                            CANTIDAD = cant,
                            PRECIO_PEDIDO = n.PrecioPedido ?? 0m,
                            PRECIO_VENTA = n.PrecioVenta ?? 0m,
                            SUBTOTAL = cant * (n.PrecioPedido ?? 0m),
                            FECHA_VENCIMIENTO = n.FechaVencimiento.HasValue ? n.FechaVencimiento.Value : (DateOnly?)null,
                            ESTADO = "ACTIVO",
                            ELIMINADO = false,
                            CREADO_POR = usuario,
                            FECHA_CREACION = ahora
                        };
                        _context.Add(det);
                        idsVmPresentes.Add(det.DETALLE_PEDIDO_ID);
                    }
                }

                // 2.c) Soft-delete de las que no vinieron
                foreach (var d in detalleDb)
                {
                    if (!idsVmPresentes.Contains(d.DETALLE_PEDIDO_ID))
                    {
                        d.ELIMINADO = true;
                        d.ESTADO = "INACTIVO";
                        d.ELIMINADO_POR = usuario;
                        d.FECHA_ELIMINACION = ahora;
                        d.MODIFICADO_POR = usuario;
                        d.FECHA_MODIFICACION = ahora;
                    }
                }

                // 3) Efectos específicos de transición SIN tocar la DB:
                // - ENVIADO: usamos FECHA_MODIFICACION=ahora y MODIFICADO_POR=usuario como "traza de envío".
                // - RECIBIDO: usamos FECHA_ENTREGA_ESTIMADA (ya mapeada arriba) como "fecha de recepción".
                // - ANULADO: marcar INACTIVO ya se hizo con vm.EstadoActivo si desea, o forzar aquí:
                if (nombreDestino == EstadosPedido.ANULADO) ped.ESTADO = "INACTIVO";

                // 4) Aplicar estado destino
                ped.ESTADO_PEDIDO_ID = vm.EstadoPedidoId!;

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                TempData["UpdatedOk"] = true;
                TempData["UpdatedName"] = ped.PEDIDO_ID;
                return RedirectToAction(nameof(Edit), new { id = ped.PEDIDO_ID });
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync(ct);
                ModelState.AddModelError(string.Empty, $"Error BD: {ex.GetBaseException().Message}");
                await CargarCombosAsync(vm, ct, "edit", EstadosPedido.Normalizar(await GetNombreEstadoByIdAsync(ped.ESTADO_PEDIDO_ID, ct)));
                return View(vm);
            }
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cerrar(string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta id.");
            id = id.Trim().ToUpper();

            var ped = await _context.PEDIDO
                .FirstOrDefaultAsync(p => !p.ELIMINADO && p.PEDIDO_ID == id, ct);
            if (ped is null) return NotFound("Pedido no encontrado.");

            var nombreActual = EstadosPedido.Normalizar(
                await GetNombreEstadoByIdAsync(ped.ESTADO_PEDIDO_ID, ct));

            if (nombreActual != EstadosPedido.RECIBIDO)
                return BadRequest("Solo se puede cerrar un pedido en estado RECIBIDO.");

            // Traer detalle + (opcional) info de producto para mensajes
            var detalles = await (
                from d in _context.DETALLE_PEDIDO
                join pr0 in _context.PRODUCTO on d.PRODUCTO_ID equals pr0.PRODUCTO_ID into gpr
                from pr in gpr.DefaultIfEmpty()
                where !d.ELIMINADO && d.PEDIDO_ID == id
                orderby d.DETALLE_PEDIDO_ID
                select new
                {
                    d.DETALLE_PEDIDO_ID,
                    d.PRODUCTO_ID,
                    d.PRECIO_PEDIDO,
                    d.PRECIO_VENTA,
                    d.FECHA_VENCIMIENTO,
                    ProductoCodigo = pr != null ? pr.PRODUCTO_CODIGO : d.PRODUCTO_ID,
                    ProductoNombre = pr != null ? pr.PRODUCTO_NOMBRE : null
                }
            ).ToListAsync(ct);

            // ===== Validaciones: precios > 0 y vencimiento obligatorio =====
            var errores = new List<string>();
            foreach (var d in detalles)
            {
                var faltas = new List<string>();
                if (!(d.PRECIO_PEDIDO.HasValue) || d.PRECIO_PEDIDO <= 0) faltas.Add("Precio compra (> 0)");
                if (!(d.PRECIO_VENTA.HasValue) || d.PRECIO_VENTA <= 0) faltas.Add("Precio venta (> 0)");
                if (!d.FECHA_VENCIMIENTO.HasValue) faltas.Add("Fecha vencimiento");

                if (faltas.Count > 0)
                {
                    var etiqueta = !string.IsNullOrWhiteSpace(d.ProductoNombre)
                        ? $"{d.ProductoCodigo} - {d.ProductoNombre}"
                        : d.ProductoCodigo;

                    errores.Add($"• {etiqueta}: {string.Join(", ", faltas)}");
                }
            }

            if (errores.Count > 0)
            {
                // Enviamos errores para mostrarlos en modal (JS en la vista)
                TempData["CerrarErrores"] = string.Join("\n", errores);
                return RedirectToAction(nameof(Edit), new { id });
            }

            // TODO: inventario real (entradas/costeo). Aquí simulamos OK.
            var okAplicacionInventario = true;

            if (!okAplicacionInventario)
            {
                TempData["CerrarErrores"] = "No se pudo aplicar inventario. Revise lotes/stock/costos.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            // Transición a CERRADO (o FINALIZADO en tu catálogo)
            var idCerrado = await GetIdEstadoByNombreAsync(EstadosPedido.CERRADO, ct);
            ped.ESTADO_PEDIDO_ID = idCerrado ?? ped.ESTADO_PEDIDO_ID;
            ped.MODIFICADO_POR = await _auditoria.GetUsuarioNombreAsync();
            ped.FECHA_MODIFICACION = DateTime.Now;

            await _context.SaveChangesAsync(ct);
            TempData["CerradoOk"] = true;

            return RedirectToAction(nameof(Edit), new { id });
        }






















        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Edit(string id, PedidoViewModel vm, CancellationToken ct)
        //{
        //    if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta id.");
        //    id = id.Trim().ToUpper();
        //    if (id != vm.PedidoId?.Trim().ToUpper()) return BadRequest("Inconsistencia de id.");

        //    // ===== Validaciones de DataAnnotations =====
        //    if (!ModelState.IsValid)
        //    {
        //        await CargarCombosAsync(vm, ct);
        //        return View(vm);
        //    }

        //    // ===== Carga actual de BD =====
        //    var ped = await _context.PEDIDO
        //        .FirstOrDefaultAsync(p => !p.ELIMINADO && p.PEDIDO_ID == id, ct);
        //    if (ped == null) return NotFound("Pedido no encontrado.");

        //    var detalleDb = await _context.DETALLE_PEDIDO
        //        .Where(d => !d.ELIMINADO && d.PEDIDO_ID == id)
        //        .OrderBy(d => d.DETALLE_PEDIDO_ID)
        //        .ToListAsync(ct);

        //    // ===== Normaliza/hardening del detalle entrante =====
        //    vm.Lineas = (vm.Lineas ?? new List<PedidoDetalleItemVM>())
        //        .Where(l => l != null && !string.IsNullOrWhiteSpace(l.ProductoId))
        //        .Select(l => new PedidoDetalleItemVM
        //        {
        //            DetallePedidoId = string.IsNullOrWhiteSpace(l.DetallePedidoId) ? null : l.DetallePedidoId!.Trim(),
        //            ProductoId = (l.ProductoId ?? "").Trim().ToUpper(),
        //            Cantidad = l.Cantidad,                 // (decimal en VM; se normaliza abajo con ToPositiveInt)
        //            PrecioPedido = l.PrecioPedido ?? 0m,
        //            PrecioVenta = l.PrecioVenta ?? 0m,
        //            FechaVencimiento = l.FechaVencimiento          // DateOnly?
        //        })
        //        .ToList();

        //    if (vm.Lineas.Count == 0)
        //        ModelState.AddModelError(nameof(vm.Lineas), "Debe agregar al menos un producto.");

        //    // Cantidades enteras positivas
        //    for (int i = 0; i < vm.Lineas.Count; i++)
        //        _ = ToPositiveInt(vm.Lineas[i].Cantidad, nameof(PedidoDetalleItemVM.Cantidad), ModelState, $"Lineas[{i}].");

        //    // Precios no-negativos
        //    for (int i = 0; i < vm.Lineas.Count; i++)
        //    {
        //        if (vm.Lineas[i].PrecioPedido < 0)
        //            ModelState.AddModelError($"Lineas[{i}].PrecioPedido", "Precio compra no puede ser negativo.");
        //        if (vm.Lineas[i].PrecioVenta < 0)
        //            ModelState.AddModelError($"Lineas[{i}].PrecioVenta", "Precio venta no puede ser negativo.");
        //    }

        //    // Fechas coherentes en cabecera
        //    if (vm.FechaPedido.HasValue && vm.FechaPosibleEntrega.HasValue &&
        //        vm.FechaPosibleEntrega.Value.Date < vm.FechaPedido.Value.Date)
        //    {
        //        ModelState.AddModelError(nameof(vm.FechaPosibleEntrega),
        //            "La fecha posible de entrega no puede ser anterior a la fecha del pedido.");
        //    }

        //    // Valida proveedor/estado activos (igual que Create)
        //    bool proveedorOk = await _context.PROVEEDOR
        //        .AsNoTracking()
        //        .AnyAsync(p => !p.ELIMINADO && p.ESTADO == "ACTIVO" && p.PROVEEDOR_ID == vm.ProveedorId, ct);
        //    if (!proveedorOk)
        //        ModelState.AddModelError(nameof(vm.ProveedorId), "El proveedor no existe o está inactivo.");

        //    bool estadoOk = await _context.ESTADO_PEDIDO
        //        .AsNoTracking()
        //        .AnyAsync(e => !e.ELIMINADO && e.ESTADO == "ACTIVO" && e.ESTADO_PEDIDO_ID == vm.EstadoPedidoId, ct);
        //    if (!estadoOk)
        //        ModelState.AddModelError(nameof(vm.EstadoPedidoId), "El estado de pedido no existe o está inactivo.");

        //    // (Opcional) existencia de producto ACTIVO
        //    for (int i = 0; i < vm.Lineas.Count; i++)
        //    {
        //        var prodId = vm.Lineas[i].ProductoId;
        //        bool productoOk = await _context.PRODUCTO.AsNoTracking()
        //            .AnyAsync(p => !p.ELIMINADO && p.ESTADO == "ACTIVO" && p.PRODUCTO_ID == prodId, ct);
        //        if (!productoOk)
        //            ModelState.AddModelError($"Lineas[{i}].ProductoId", $"El producto {prodId} no existe o está inactivo.");
        //    }

        //    if (!ModelState.IsValid)
        //    {
        //        await CargarCombosAsync(vm, ct);
        //        return View(vm);
        //    }

        //    // ===== Detectar "sin cambios" (cabecera + detalle) =====
        //    bool headerChanged =
        //        ped.PROVEEDOR_ID != vm.ProveedorId ||
        //        ped.ESTADO_PEDIDO_ID != vm.EstadoPedidoId ||
        //        ped.OBSERVACIONES != (string.IsNullOrWhiteSpace(vm.Observacion) ? null : vm.Observacion.Trim()) ||
        //        (ped.ESTADO ?? "ACTIVO") != (vm.EstadoActivo ? "ACTIVO" : "INACTIVO") ||
        //        ped.FECHA_PEDIDO != (vm.FechaPedido ?? ped.FECHA_PEDIDO) ||
        //        (ped.FECHA_ENTREGA_ESTIMADA?.ToString("yyyy-MM-dd") ?? "") !=
        //        (vm.FechaPosibleEntrega?.ToString("yyyy-MM-dd") ?? "");

        //    bool detailChanged = false;
        //    {
        //        // Diccionario por id string
        //        var byId = detalleDb.ToDictionary(d => d.DETALLE_PEDIDO_ID);

        //        // IDs presentes en el VM (string, sin HasValue en string)
        //        var idsVm = new HashSet<string>(
        //            vm.Lineas
        //              .Where(l => !string.IsNullOrWhiteSpace(l.DetallePedidoId))
        //              .Select(l => l.DetallePedidoId!.Trim())
        //        );

        //        // 1) Cambios/ediciones en filas existentes
        //        foreach (var l in vm.Lineas.Where(x => !string.IsNullOrWhiteSpace(x.DetallePedidoId)))
        //        {
        //            var key = l.DetallePedidoId!.Trim();
        //            if (!byId.TryGetValue(key, out var d))
        //            {
        //                // id enviado que no existe -> lo tratamos como cambio
        //                detailChanged = true; break;
        //            }

        //            // compara campos relevantes
        //            bool same =
        //                string.Equals(d.PRODUCTO_ID, l.ProductoId, StringComparison.OrdinalIgnoreCase) &&
        //                d.CANTIDAD == (int)l.Cantidad &&
        //                (d.PRECIO_PEDIDO ?? 0m) == (l.PrecioPedido ?? 0m) &&
        //                (d.PRECIO_VENTA ?? 0m) == (l.PrecioVenta ?? 0m) &&
        //                (d.FECHA_VENCIMIENTO?.ToString("yyyy-MM-dd") ?? "") ==
        //                (l.FechaVencimiento?.ToString("yyyy-MM-dd") ?? "");

        //            if (!same) { detailChanged = true; break; }
        //        }

        //        // 2) Nuevas líneas (sin id)
        //        if (!detailChanged && vm.Lineas.Any(x => string.IsNullOrWhiteSpace(x.DetallePedidoId)))
        //            detailChanged = true;

        //        // 3) Eliminadas (set equality)
        //        if (!detailChanged)
        //        {
        //            var idsDb = new HashSet<string>(detalleDb.Select(d => d.DETALLE_PEDIDO_ID));
        //            if (!idsVm.SetEquals(idsDb)) detailChanged = true;
        //        }
        //    }

        //    if (!headerChanged && !detailChanged)
        //    {
        //        TempData["NoChanges"] = true;
        //        return RedirectToAction(nameof(Edit), new { id });
        //    }

        //    // ===== Persistencia (transacción) =====
        //    var ahora = DateTime.Now;
        //    var usuario = await _auditoria.GetUsuarioNombreAsync();

        //    await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        //    try
        //    {
        //        // 1) Cabecera
        //        ped.PROVEEDOR_ID = vm.ProveedorId!;
        //        ped.ESTADO_PEDIDO_ID = vm.EstadoPedidoId!;

        //        ped.OBSERVACIONES = string.IsNullOrWhiteSpace(vm.Observacion)
        //        ? null
        //        : vm.Observacion.Trim();

        //        ped.ESTADO = vm.EstadoActivo ? "ACTIVO" : "INACTIVO";
        //        ped.FECHA_PEDIDO = vm.FechaPedido ?? ped.FECHA_PEDIDO;

        //        ped.FECHA_ENTREGA_ESTIMADA = vm.FechaPosibleEntrega.HasValue
        //            ? DateOnly.FromDateTime(vm.FechaPosibleEntrega.Value)
        //            : (DateOnly?)null;

        //        ped.MODIFICADO_POR = usuario;
        //        ped.FECHA_MODIFICACION = ahora;

        //        // 2) Detalle (upsert)
        //        var dbMap = detalleDb.ToDictionary(d => d.DETALLE_PEDIDO_ID);
        //        var idsVmPresentes = new HashSet<string>();

        //        // 2.a) Editar existentes (id string, sin .HasValue)
        //        foreach (var l in vm.Lineas.Where(x => !string.IsNullOrWhiteSpace(x.DetallePedidoId)))
        //        {
        //            var key = l.DetallePedidoId!.Trim();
        //            if (!dbMap.TryGetValue(key, out var d)) continue; // seguridad

        //            d.PRODUCTO_ID = l.ProductoId;
        //            d.CANTIDAD = ToPositiveInt(l.Cantidad, nameof(PedidoDetalleItemVM.Cantidad), ModelState);
        //            d.PRECIO_PEDIDO = l.PrecioPedido ?? 0m;
        //            d.PRECIO_VENTA = l.PrecioVenta ?? 0m;
        //            d.SUBTOTAL = d.CANTIDAD * (d.PRECIO_PEDIDO ?? 0m);

        //            d.FECHA_VENCIMIENTO = l.FechaVencimiento.HasValue
        //                ? l.FechaVencimiento.Value                    // VM usa DateOnly?
        //                : (DateOnly?)null;

        //            d.MODIFICADO_POR = usuario;
        //            d.FECHA_MODIFICACION = ahora;

        //            idsVmPresentes.Add(d.DETALLE_PEDIDO_ID);
        //        }

        //        // 2.b) Insertar nuevas (sin id)
        //        var nuevas = vm.Lineas.Where(x => string.IsNullOrWhiteSpace(x.DetallePedidoId)).ToList();
        //        if (nuevas.Count > 0)
        //        {
        //            var idsRango = await _correlativos.NextDetallePedidosRangeAsync(nuevas.Count, ct); // List<string>
        //            if (idsRango.Count != nuevas.Count)
        //            {
        //                await tx.RollbackAsync(ct);
        //                ModelState.AddModelError(string.Empty, "No se pudo reservar el bloque de IDs para detalle.");
        //                await CargarCombosAsync(vm, ct);
        //                return View(vm);
        //            }

        //            for (int i = 0; i < nuevas.Count; i++)
        //            {
        //                var n = nuevas[i];
        //                int cant = ToPositiveInt(n.Cantidad, nameof(PedidoDetalleItemVM.Cantidad), ModelState);
        //                //int cant = ToPositiveInt(n.Cantidad, nameof(PedidoViewModel.PedidoDetalleItemVM.Cantidad), ModelState);


        //                var det = new DETALLE_PEDIDO
        //                {
        //                    DETALLE_PEDIDO_ID = idsRango[i],             // string
        //                    PEDIDO_ID = ped.PEDIDO_ID,
        //                    PRODUCTO_ID = n.ProductoId,
        //                    CANTIDAD = cant,
        //                    PRECIO_PEDIDO = n.PrecioPedido ?? 0m,
        //                    PRECIO_VENTA = n.PrecioVenta ?? 0m,
        //                    SUBTOTAL = cant * (n.PrecioPedido ?? 0m),
        //                    FECHA_VENCIMIENTO = n.FechaVencimiento.HasValue ? n.FechaVencimiento.Value : (DateOnly?)null,
        //                    ESTADO = "ACTIVO",
        //                    ELIMINADO = false,
        //                    CREADO_POR = usuario,
        //                    FECHA_CREACION = ahora
        //                };
        //                _context.Add(det);
        //                idsVmPresentes.Add(det.DETALLE_PEDIDO_ID);
        //            }
        //        }

        //        // 2.c) Soft-delete (los que estaban en BD y no vinieron ahora)
        //        foreach (var d in detalleDb)
        //        {
        //            if (!idsVmPresentes.Contains(d.DETALLE_PEDIDO_ID))
        //            {
        //                d.ELIMINADO = true;
        //                d.ESTADO = "INACTIVO";
        //                d.ELIMINADO_POR = usuario;
        //                d.FECHA_ELIMINACION = ahora;
        //                d.MODIFICADO_POR = usuario;
        //                d.FECHA_MODIFICACION = ahora;
        //            }
        //        }

        //        await _context.SaveChangesAsync(ct);
        //        await tx.CommitAsync(ct);

        //        TempData["UpdatedOk"] = true;
        //        TempData["UpdatedName"] = ped.PEDIDO_ID;

        //        return RedirectToAction(nameof(Edit));
        //    }
        //    catch (DbUpdateException ex)
        //    {
        //        await tx.RollbackAsync(ct);
        //        ModelState.AddModelError(string.Empty, $"Error BD: {ex.GetBaseException().Message}");
        //        await CargarCombosAsync(vm, ct);
        //        return View(vm);
        //    }
        //}










































        // GET: Pedidos/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pEDIDO = await _context.PEDIDO
                .Include(p => p.ESTADO_PEDIDO)
                .Include(p => p.PROVEEDOR)
                .FirstOrDefaultAsync(m => m.PEDIDO_ID == id);
            if (pEDIDO == null)
            {
                return NotFound();
            }

            return View(pEDIDO);
        }

        // POST: Pedidos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var pEDIDO = await _context.PEDIDO.FindAsync(id);
            if (pEDIDO != null)
            {
                _context.PEDIDO.Remove(pEDIDO);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PEDIDOExists(string id)
        {
            return _context.PEDIDO.Any(e => e.PEDIDO_ID == id);
        }
    }
}
