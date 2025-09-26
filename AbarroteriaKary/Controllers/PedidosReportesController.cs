using AbarroteriaKary.Data;                 // ← tu DbContext
using AbarroteriaKary.ModelsPartial; // ← VMs que agregamos abajo
using AbarroteriaKary.Services.Reportes;    // ← IReporteExportService
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AbarroteriaKary.Controllers
{
    // =========================================================
    // HUB y reportes de Pedidos
    // Rutas relevantes:
    //   GET  /PedidosReportes                -> HUB
    //   GET  /PedidosReportes/ComprasProveedor
    //   GET  /PedidosReportes/ComprasProveedorExport?formato=xlsx|pdf
    //   GET  /PedidosReportes/Cerrado/{id}?formato=pdf|xlsx
    //   GET  /PedidosReportes/Cotizacion/{id}
    // =========================================================
    public class PedidosReportesController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly IReporteExportService _export;

        public PedidosReportesController(KaryDbContext context, IReporteExportService export)
        {
            _context = context;
            _export = export;
        }



        [HttpGet]
        public async Task<IActionResult> BuscarPedidosCerrados(
            string? q, DateTime? desde, DateTime? hasta, string? ProveedorId, int take = 50, CancellationToken ct = default)
        {
            // 1) IDs de estado CERRADO/FINALIZADO
            var cerradosIds = await _context.ESTADO_PEDIDO.AsNoTracking()
                .Where(e => !e.ELIMINADO && e.ESTADO == "ACTIVO" &&
                       (e.ESTADO_PEDIDO_NOMBRE == "CERRADO" || e.ESTADO_PEDIDO_NOMBRE == "FINALIZADO"))
                .Select(e => e.ESTADO_PEDIDO_ID)
                .ToListAsync(ct);

            // 2) Query base: PEDIDO (cerrado) + PROVEEDOR + PERSONA
            var qBase =
                from p in _context.PEDIDO.AsNoTracking()
                    .Where(p => !p.ELIMINADO && cerradosIds.Contains(p.ESTADO_PEDIDO_ID))
                join pr in _context.PROVEEDOR.AsNoTracking() on p.PROVEEDOR_ID equals pr.PROVEEDOR_ID
                join pe in _context.PERSONA.AsNoTracking() on pr.PROVEEDOR_ID equals pe.PERSONA_ID
                select new
                {
                    p.PEDIDO_ID,
                    p.PROVEEDOR_ID,
                    Proveedor = (pe.PERSONA_PRIMERNOMBRE + " " + pe.PERSONA_PRIMERAPELLIDO).Trim(),
                    p.FECHA_PEDIDO,                     // DateTime (DB)
                    FECHA_RECIBIDO = p.FECHA_ENTREGA_ESTIMADA // DateOnly? (regla: usamos este campo como recibido)
                };

            // 3) Filtros
            if (!string.IsNullOrWhiteSpace(ProveedorId) && ProveedorId != "__TODOS__")
            {
                var provId = ProveedorId.Trim();
                qBase = qBase.Where(x => x.PROVEEDOR_ID == provId);
            }

            // por fechas (sobre recibido, que es lo lógico para cerrados)
            if (desde.HasValue)
                qBase = qBase.Where(x => x.FECHA_RECIBIDO.HasValue &&
                                         x.FECHA_RECIBIDO.Value >= DateOnly.FromDateTime(desde.Value.Date));
            if (hasta.HasValue)
                qBase = qBase.Where(x => x.FECHA_RECIBIDO.HasValue &&
                                         x.FECHA_RECIBIDO.Value <= DateOnly.FromDateTime(hasta.Value.Date));

            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim().ToUpper();
                qBase = qBase.Where(x =>
                    x.PEDIDO_ID.ToUpper().Contains(k) ||
                    x.Proveedor.ToUpper().Contains(k)
                );
            }

            // 4) Trae total por pedido desde el detalle
            //    IMPORTANTE: evitamos SUBTOTAL.HasValue (decimal no-nullable no tiene HasValue)
            //    calculando el total siempre como CANTIDAD * (PRECIO_PEDIDO ?? 0m)
            var items = await (
                from x in qBase
                join d in _context.DETALLE_PEDIDO.AsNoTracking() on x.PEDIDO_ID equals d.PEDIDO_ID
                group d by new { x.PEDIDO_ID, x.Proveedor, x.FECHA_PEDIDO, x.FECHA_RECIBIDO } into g
                select new
                {
                    g.Key.PEDIDO_ID,
                    g.Key.Proveedor,
                    g.Key.FECHA_PEDIDO,
                    g.Key.FECHA_RECIBIDO,
                    Total = g.Sum(z => (decimal)z.CANTIDAD * (z.PRECIO_PEDIDO ?? 0m))
                }
            )
            .OrderByDescending(x => x.FECHA_RECIBIDO)  // primero lo más reciente
            .ThenByDescending(x => x.PEDIDO_ID)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

            // 5) Shape final para el picker (strings)
            var result = items.Select(x => new
            {
                pedidoId = x.PEDIDO_ID,
                proveedor = x.Proveedor,
                fechaPedido = x.FECHA_PEDIDO.ToString("dd/MM/yyyy"),
                fechaRecibido = x.FECHA_RECIBIDO.HasValue ? x.FECHA_RECIBIDO.Value.ToString("dd/MM/yyyy") : "-",
                total = x.Total.ToString("N2")
            });

            return Json(result);
        }




        // GET: /PedidosReportes/Hub
        [HttpGet]
        public async Task<IActionResult> Hub(CancellationToken ct)
        {
            // === Proveedores para multiselección (llenar FiltroOpcionVM) ===
            var proveedores = await (
                from pr in _context.PROVEEDOR.AsNoTracking().Where(x => !x.ELIMINADO)
                join pe in _context.PERSONA.AsNoTracking()
                    on pr.PROVEEDOR_ID equals pe.PERSONA_ID
                orderby pe.PERSONA_PRIMERNOMBRE, pe.PERSONA_PRIMERAPELLIDO
                select new FiltroOpcionVM
                {
                    Id = pr.PROVEEDOR_ID,
                    Nombre = (pe.PERSONA_PRIMERNOMBRE + " " + pe.PERSONA_PRIMERAPELLIDO).Trim()
                }
            ).ToListAsync(ct);

            // === Estados de pedido, ordenados lógicamente ===
            var ordenEstados = new[] { "BORRADOR", "PENDIENTE", "ENVIADO", "RECIBIDO", "ANULADO", "CERRADO" };

            var estadosOpc = await _context.ESTADO_PEDIDO
                .AsNoTracking()
                .Where(e => !e.ELIMINADO && e.ESTADO == "ACTIVO")
                .Select(e => new
                {
                    e.ESTADO_PEDIDO_ID,
                    N = (e.ESTADO_PEDIDO_NOMBRE ?? "").Trim().ToUpper()
                })
                .ToListAsync(ct);

            var estadosOrdenados = estadosOpc
                .OrderBy(e =>
                {
                    var idx = Array.IndexOf(ordenEstados, e.N);
                    return idx < 0 ? int.MaxValue : idx;
                })
                .Select(e => new FiltroOpcionVM
                {
                    Id = e.ESTADO_PEDIDO_ID,
                    Nombre = char.ToUpper(e.N[0]) + e.N.Substring(1).ToLower() // "Borrador", "Pendiente", ...
                })
                .ToList();

            // === Filtros por defecto para la vista ===
            var filtros = new ReporteHubFiltroVM
            {
                BaseFecha = "pedido",
                ProveedoresOpciones = proveedores,
                EstadosOpciones = estadosOrdenados
            };

            var vm = new ReporteHubIndexVM   // si usas ReporteHubIndexVM; si no, pasa 'filtros' directo al modelo que uses
            {
                Tipo = "GENERAL",
                Filtros = filtros
            };

            return View(vm); // o View(filtros) si tu vista espera directamente ReporteHubFiltroVM
        }













        // =====================================================
        // GET: /PedidosReportes  (HUB)
        // - Muestra tarjetas y filtros "globales" (opcional).
        // - Los filtros se envían por querystring a cada reporte.
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Index(ReporteHubFiltroVM filtros, CancellationToken ct)
        {
            // Defaults: últimos 30 días, base = "pedido"
            filtros.BaseFecha ??= "pedido";
            filtros.Desde ??= DateTime.Today.AddDays(-30);
            filtros.Hasta ??= DateTime.Today;

            // Combos: proveedores (PERSONA por FK desde PROVEEDOR)
            var proveedores = await (
                from pr in _context.PROVEEDOR.AsNoTracking().Where(x => !x.ELIMINADO && x.ESTADO == "ACTIVO")
                join pe in _context.PERSONA.AsNoTracking().Where(x => !x.ELIMINADO && x.ESTADO == "ACTIVO")
                    on pr.PROVEEDOR_ID equals pe.PERSONA_ID
                orderby pe.PERSONA_PRIMERNOMBRE, pe.PERSONA_PRIMERAPELLIDO
                select new FiltroOpcionVM
                {
                    Id = pr.PROVEEDOR_ID,
                    Nombre = (pe.PERSONA_PRIMERNOMBRE + " " + pe.PERSONA_PRIMERAPELLIDO).Trim()
                }
            ).ToListAsync(ct);

            filtros.ProveedoresOpciones = proveedores;

            // Estados disponibles en catálogo (orden visible)
            filtros.EstadosOpciones = new List<FiltroOpcionVM>
            {
                new() { Id = "BORRADOR",   Nombre = "Borrador" },
                new() { Id = "PENDIENTE",  Nombre = "Pendiente" },
                new() { Id = "ENVIADO",    Nombre = "Enviado" },
                new() { Id = "RECIBIDO",   Nombre = "Recibido" },
                new() { Id = "CERRADO",    Nombre = "Cerrado" },
                new() { Id = "ANULADO",    Nombre = "Anulado" },
            };

            ViewBag.Usuario = User?.Identity?.Name ?? "Admin";
            return View(filtros); // Views/PedidosReportes/Index.cshtml
        }

        // =====================================================
        // GET: /PedidosReportes/ComprasProveedor
        // - Resumen acumulado por proveedor, periodo y base de fecha.
        // - Solo pedidos CERRADOS (y FINALIZADO por compatibilidad).
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ComprasProveedor(ReporteHubFiltroVM f, CancellationToken ct)
        {
            f.BaseFecha ??= "recibido"; // por defecto en compras conviene base "recibido"
            f.Desde ??= DateTime.Today.AddMonths(-1);
            f.Hasta ??= DateTime.Today;

            // Convertir a DateOnly para FECHA_ENTREGA_ESTIMADA
            DateOnly? dDesde = f.Desde.HasValue ? DateOnly.FromDateTime(f.Desde.Value.Date) : (DateOnly?)null;
            DateOnly? dHasta = f.Hasta.HasValue ? DateOnly.FromDateTime(f.Hasta.Value.Date) : (DateOnly?)null;

            // Base de pedidos (sin tracking)
            var qPedBase = _context.PEDIDO
                .AsNoTracking()
                .Where(p => !p.ELIMINADO);

            if (f.BaseFecha.Equals("pedido", StringComparison.OrdinalIgnoreCase))
            {
                if (f.Desde.HasValue) qPedBase = qPedBase.Where(p => p.FECHA_PEDIDO >= f.Desde.Value);
                if (f.Hasta.HasValue) qPedBase = qPedBase.Where(p => p.FECHA_PEDIDO < f.Hasta.Value.AddDays(1));
            }
            else
            {
                if (dDesde.HasValue) qPedBase = qPedBase.Where(p => p.FECHA_ENTREGA_ESTIMADA >= dDesde.Value);
                if (dHasta.HasValue) qPedBase = qPedBase.Where(p => p.FECHA_ENTREGA_ESTIMADA <= dHasta.Value);
            }

            // Estados cerrados por NOMBRE → IDs
            string[] estadosCerrado = new[] { "CERRADO", "FINALIZADO" };
            var estadosCerradosIds = await _context.ESTADO_PEDIDO
                .AsNoTracking()
                .Where(e => !e.ELIMINADO && e.ESTADO == "ACTIVO"
                    && estadosCerrado.Contains((e.ESTADO_PEDIDO_NOMBRE ?? "").ToUpper()))
                .Select(e => e.ESTADO_PEDIDO_ID)
                .ToListAsync(ct);

            // Solo pedidos cerrados
            var qPedCerrados = qPedBase
                .Where(p => estadosCerradosIds.Contains(p.ESTADO_PEDIDO_ID))
                .Select(p => new { p.PEDIDO_ID, p.PROVEEDOR_ID });

            // Unidades y monto por proveedor
            var qDet = from d in _context.DETALLE_PEDIDO.AsNoTracking().Where(x => !x.ELIMINADO)
                       join pc in qPedCerrados on d.PEDIDO_ID equals pc.PEDIDO_ID
                       select new
                       {
                           pc.PROVEEDOR_ID,
                           Cant = d.CANTIDAD,
                           Monto = d.CANTIDAD * (d.PRECIO_PEDIDO ?? 0m)
                       };

            var resumen = await qDet
                .GroupBy(x => x.PROVEEDOR_ID)
                .Select(g => new
                {
                    ProveedorId = g.Key,
                    Unidades = g.Sum(z => (int)z.Cant),
                    Monto = g.Sum(z => z.Monto),
                    CantPedidos = qPedCerrados.Count(p => p.PROVEEDOR_ID == g.Key)
                })
                .ToListAsync(ct);

            decimal montoTotalPeriodo = resumen.Sum(x => x.Monto);

            var provIds = resumen.Select(x => x.ProveedorId).Distinct().ToList();
            var nombres = await (
                from pr in _context.PROVEEDOR.AsNoTracking()
                    .Where(x => !x.ELIMINADO && provIds.Contains(x.PROVEEDOR_ID))
                join pe in _context.PERSONA.AsNoTracking()
                    on pr.PROVEEDOR_ID equals pe.PERSONA_ID
                select new { pr.PROVEEDOR_ID, Nombre = (pe.PERSONA_PRIMERNOMBRE + " " + pe.PERSONA_PRIMERAPELLIDO).Trim() }
            ).ToDictionaryAsync(x => x.PROVEEDOR_ID, x => x.Nombre, ct);

            var vm = new ComprasProveedorPageVM
            {
                Filtros = f,
                Items = resumen.Select(x => new ComprasProveedorResumenVM
                {
                    ProveedorId = x.ProveedorId,
                    ProveedorNombre = nombres.TryGetValue(x.ProveedorId, out var n) ? n : x.ProveedorId,
                    CantPedidos = x.CantPedidos,
                    Unidades = x.Unidades,
                    Monto = x.Monto,
                    TicketMedio = x.CantPedidos > 0 ? (x.Monto / x.CantPedidos) : 0m,
                    ParticipacionPorc = (montoTotalPeriodo > 0m) ? Math.Round(100m * x.Monto / montoTotalPeriodo, 2) : 0m
                })
                .OrderByDescending(x => x.Monto)
                .ToList(),
                MontoTotalPeriodo = montoTotalPeriodo
            };

            ViewBag.Usuario = User?.Identity?.Name ?? "Admin";
            return View(vm);
        }


        // =====================================================
        // GET: /PedidosReportes/ComprasProveedorExport?formato=xlsx|pdf
        // - Usa tu servicio IReporteExportService para Excel.
        // - Para PDF, devolvemos la vista "PDF-like" (o conecta tu motor).
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ComprasProveedorExport(string formato, [FromQuery] ReporteHubFiltroVM f, CancellationToken ct)
        {
            // Reutilizamos la acción normal para armar el VM (sin duplicar lógica):
            var actionVM = await ComprasProveedor(f, ct) as ViewResult;
            var page = actionVM?.Model as ComprasProveedorPageVM;
            if (page == null || page.Items.Count == 0) return NotFound("Sin datos para exportar.");

            formato = (formato ?? "xlsx").Trim().ToLowerInvariant();

            if (formato == "xlsx" || formato == "excel")
            {
                // Excel agrupado (resumen)
                var bytes = _export.GenerarExcelComprasProveedorResumen(page.Items);
                var nombre = $"ComprasProveedor_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
            }
            else if (formato == "pdf")
            {
                // TODO: Si tienes generador PDF (wkhtmltopdf/Rotativa), úsalo aquí.
                // Mientras, devolvemos una vista de "PDF" con _PdfLayout.
                ViewData["Title"] = "Compras por Proveedor";
                return View("ComprasProveedorPdf", page);
            }
            else
            {
                return BadRequest("Formato no soportado.");
            }
        }

        // =====================================================
        // GET: /PedidosReportes/Cerrado/{id}?formato=pdf|xlsx
        // - Orden cerrada (liquidación). Solo si estado es CERRADO/FINALIZADO.
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Cerrado(string id, string formato = "pdf", CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta id.");
            id = id.Trim().ToUpper();

            // Cabecera + estado
            var ped = await (
                from p in _context.PEDIDO.Where(x => !x.ELIMINADO && x.PEDIDO_ID == id)
                join e in _context.ESTADO_PEDIDO on p.ESTADO_PEDIDO_ID equals e.ESTADO_PEDIDO_ID
                join pr in _context.PROVEEDOR on p.PROVEEDOR_ID equals pr.PROVEEDOR_ID
                join pe in _context.PERSONA on pr.PROVEEDOR_ID equals pe.PERSONA_ID
                select new { P = p, EstadoNombre = e.ESTADO_PEDIDO_NOMBRE, ProvNombre = pe.PERSONA_PRIMERNOMBRE + " " + pe.PERSONA_PRIMERAPELLIDO }
            ).FirstOrDefaultAsync(ct);

            if (ped == null) return NotFound("Pedido no encontrado.");

            // Solo CERRADO/FINALIZADO
            var estadoUp = (ped.EstadoNombre ?? "").Trim().ToUpperInvariant();
            if (estadoUp != "CERRADO" && estadoUp != "FINALIZADO")
                return BadRequest("El pedido no está cerrado.");

            // Detalle
            var lineas = await (
                from d in _context.DETALLE_PEDIDO.AsNoTracking().Where(x => !x.ELIMINADO && x.PEDIDO_ID == id)
                join pr in _context.PRODUCTO.AsNoTracking() on d.PRODUCTO_ID equals pr.PRODUCTO_ID
                orderby d.DETALLE_PEDIDO_ID
                select new PedidoCerradoLineaVM
                {
                    ProductoId = d.PRODUCTO_ID,
                    Codigo = pr.PRODUCTO_CODIGO,
                    Nombre = pr.PRODUCTO_NOMBRE,
                    Cantidad = d.CANTIDAD,
                    PrecioCompra = d.PRECIO_PEDIDO ?? 0m,
                    Subtotal = (d.CANTIDAD * (d.PRECIO_PEDIDO ?? 0m)),
                    FechaVencimiento = d.FECHA_VENCIMIENTO,
                    PrecioVenta = d.PRECIO_VENTA
                }
            ).ToListAsync(ct);

            var vm = new PedidoCerradoVM
            {
                PedidoId = ped.P.PEDIDO_ID,
                ProveedorNombre = ped.ProvNombre.Trim(),
                FechaPedido = ped.P.FECHA_PEDIDO,
                FechaRecibido = ped.P.FECHA_ENTREGA_ESTIMADA.HasValue
                    ? new DateTime(ped.P.FECHA_ENTREGA_ESTIMADA.Value.Year, ped.P.FECHA_ENTREGA_ESTIMADA.Value.Month, ped.P.FECHA_ENTREGA_ESTIMADA.Value.Day)
                    : (DateTime?)null,
                Observacion = ped.P.OBSERVACIONES,
                EstadoNombre = ped.EstadoNombre,
                Lineas = lineas,
                Total = lineas.Sum(z => z.Subtotal),
                CantLineas = lineas.Count
            };

            formato = (formato ?? "pdf").Trim().ToLowerInvariant();
            if (formato == "xlsx")
            {
                var bytes = _export.GenerarExcelPedidoCerrado(vm);
                var nombre = $"PedidoCerrado_{vm.PedidoId}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
            }
            else
            {
                ViewData["Title"] = $"Pedido {vm.PedidoId} - Orden cerrada";
                ViewBag.Usuario = User?.Identity?.Name ?? "Admin";
                return View("CerradoPdf", vm); // Views/PedidosReportes/CerradoPdf.cshtml (con _PdfLayout)
            }
        }

        // =====================================================
        // GET: /PedidosReportes/Cotizacion/{id}
        // - Si existe "cotización original" guardada en disco → servirla.
        // - Si no, reconstruir layout de cotización con datos actuales.
        //   (Útil cuando el pedido ya está cerrado.)
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Cotizacion(string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest("Falta id.");
            id = id.Trim().ToUpper();

            // 1) Buscar archivo original (si decidiste guardar copias)
            //    Ruta sugerida: wwwroot/reportes/pedidos/{id}/cotizacion-*.pdf
            var basePath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "reportes", "pedidos", id);
            if (Directory.Exists(basePath))
            {
                var file = Directory.EnumerateFiles(basePath, "cotizacion-*.pdf").OrderByDescending(x => x).FirstOrDefault();
                if (!string.IsNullOrEmpty(file))
                {
                    var bytes = await System.IO.File.ReadAllBytesAsync(file, ct);
                    var fname = System.IO.Path.GetFileName(file);
                    return File(bytes, "application/pdf", fname);
                }
            }

            // 2) Reconstruir con datos actuales
            var ped = await (
                from p in _context.PEDIDO.Where(x => !x.ELIMINADO && x.PEDIDO_ID == id)
                join pr in _context.PROVEEDOR on p.PROVEEDOR_ID equals pr.PROVEEDOR_ID
                join pe in _context.PERSONA on pr.PROVEEDOR_ID equals pe.PERSONA_ID
                select new { P = p, ProvNombre = pe.PERSONA_PRIMERNOMBRE + " " + pe.PERSONA_PRIMERAPELLIDO }
            ).FirstOrDefaultAsync(ct);
            if (ped == null) return NotFound("Pedido no encontrado.");

            var lineas = await (
                from d in _context.DETALLE_PEDIDO.AsNoTracking().Where(x => !x.ELIMINADO && x.PEDIDO_ID == id)
                join pr in _context.PRODUCTO.AsNoTracking() on d.PRODUCTO_ID equals pr.PRODUCTO_ID
                orderby d.DETALLE_PEDIDO_ID
                select new PedidoCerradoLineaVM    // reutilizamos VM de línea (sin problema)
                {
                    ProductoId = d.PRODUCTO_ID,
                    Codigo = pr.PRODUCTO_CODIGO,
                    Nombre = pr.PRODUCTO_NOMBRE,
                    Cantidad = d.CANTIDAD,
                    PrecioCompra = d.PRECIO_PEDIDO ?? 0m,
                    Subtotal = d.CANTIDAD * (d.PRECIO_PEDIDO ?? 0m),
                    FechaVencimiento = d.FECHA_VENCIMIENTO,
                    PrecioVenta = d.PRECIO_VENTA
                }
            ).ToListAsync(ct);

            var vm = new CotizacionVM
            {
                PedidoId = ped.P.PEDIDO_ID,
                ProveedorNombre = ped.ProvNombre.Trim(),
                FechaPedido = ped.P.FECHA_PEDIDO,
                Observacion = ped.P.OBSERVACIONES,
                Lineas = lineas
            };

            ViewData["Title"] = $"Pedido {vm.PedidoId} - Cotización";
            ViewBag.Usuario = User?.Identity?.Name ?? "Admin";
            return View("CotizacionPdf", vm); // Views/PedidosReportes/CotizacionPdf.cshtml
        }
    }
}
