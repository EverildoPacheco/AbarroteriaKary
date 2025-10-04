using AbarroteriaKary.Data;                    // KaryDbContext
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;           // VMs de Caja
using AbarroteriaKary.Services.Auditoria;      // IAuditoriaService
using AbarroteriaKary.Services.Correlativos;   // ICorrelativoService
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; // ← para User.FindFirstValue(...)
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;

namespace AbarroteriaKary.Controllers
{
    public class CajaSesionController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;
        private readonly IAuditoriaService _auditoria;

        public CajaSesionController(
            KaryDbContext context,
            ICorrelativoService correlativos,
            IAuditoriaService auditoria)
        {
            _context = context;
            _correlativos = correlativos;
            _auditoria = auditoria;
        }

        // ============================================================
        // GET: /CajaSesion/Estado?cajaId=CAJ000001
        // Devuelve JSON para pintar el botón "Caja" y habilitar "Nueva venta".
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Estado(string cajaId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cajaId))
                return BadRequest(new { ok = false, message = "Debe indicar la caja." });

            var sesion = await _context.CAJA_SESION
                .AsNoTracking()
                .Where(s => !s.ELIMINADO && s.CAJA_ID == cajaId && s.ESTADO_SESION == "ABIERTA")
                .OrderByDescending(s => s.FECHA_APERTURA)
                .FirstOrDefaultAsync(ct);

            var vm = new CajaEstadoVM
            {
                CajaId = cajaId,
                CajaNombre = await _context.CAJA.Where(c => c.CAJA_ID == cajaId).Select(c => c.CAJA_NOMBRE).FirstOrDefaultAsync(ct),
                SesionId = sesion?.SESION_ID,
                SesionAbierta = sesion != null,
                FechaApertura = sesion?.FECHA_APERTURA,
                UsuarioAperturaId = sesion?.USUARIO_APERTURA_ID,
                UsuarioAperturaNombre = null, // si quieres, resuelve a nombre con join a USUARIO
                MontoInicial = sesion?.MONTO_INICIAL,
                NotaApertura = sesion?.NOTAAPERTURA
            };

            if (sesion != null)
            {
                var tot = await _context.MOVIMIENTO_CAJA
                    .AsNoTracking()
                    .Where(m => !m.ELIMINADO && m.SESION_ID == sesion.SESION_ID)
                    .GroupBy(m => 1)
                    .Select(g => new
                    {
                        Ingresos = g.Where(m => m.TIPO == "INGRESO").Sum(m => (decimal?)m.MONTO) ?? 0m,
                        Egresos = g.Where(m => m.TIPO == "EGRESO").Sum(m => (decimal?)m.MONTO) ?? 0m
                    })
                    .FirstOrDefaultAsync(ct) ?? new { Ingresos = 0m, Egresos = 0m };

                vm.TotalIngresos = tot.Ingresos;
                vm.TotalEgresos = tot.Egresos;
            }

            return Json(new
            {
                ok = true,
                estado = vm.SesionAbierta ? "ABIERTA" : "CERRADA",
                sesionId = vm.SesionId,
                fechaApertura = vm.FechaApertura?.ToString("yyyy-MM-dd HH:mm"),
                montoInicial = vm.MontoInicial,
                ingresos = vm.TotalIngresos,
                egresos = vm.TotalEgresos,
                saldoEsperado = vm.SaldoEsperado
            });
        }

        // ============================================================
        // GET: /CajaSesion/Abrir?cajaId=CAJ000001
        // Render del modal de apertura (parcial Razor).
        // Si ya hay ABIERTA -> devolvemos 409 + JSON para SweetAlert2.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Abrir(string cajaId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cajaId))
                return BadRequest(new { ok = false, message = "Debe indicar la caja." });

            var yaAbierta = await _context.CAJA_SESION
                .AnyAsync(s => !s.ELIMINADO && s.CAJA_ID == cajaId && s.ESTADO_SESION == "ABIERTA", ct);

            if (yaAbierta)
                return StatusCode(409, new { ok = false, message = "La caja ya está aperturada." });

            var previewSesionId = await _correlativos.PeekNextCajaSesionIdAsync(ct);
            //var usuarioId = await _auditoria.GetUsuarioIdAsync();
            var usuarioId = RequireUserId();
            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync();

            var vm = new CajaAperturaViewModel
            {
                SesionId = previewSesionId,              // sólo vista
                CajaId = cajaId,
                CajaNombre = await _context.CAJA.Where(c => c.CAJA_ID == cajaId).Select(c => c.CAJA_NOMBRE).FirstOrDefaultAsync(ct),
                FechaApertura = DateTime.Now,
                UsuarioAperturaId = usuarioId,
                UsuarioAperturaNombre = usuarioNombre,
                MontoInicial = 0m
            };

            return PartialView("~/Views/CajaSesion/_AperturaModal.cshtml", vm);
        }






        private string RequireUserId()
        {
            // Prioridades comunes: NameIdentifier, "sub", claim custom "usuario_id", fallback Identity.Name
            var id =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirst("usuario_id")?.Value
                ?? User.Identity?.Name;

            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException("No se pudo determinar el USUARIO_ID del usuario autenticado.");

            return id.Trim().ToUpperInvariant();
        }










        // ============================================================
        // POST: /CajaSesion/Abrir
        // Crea la sesión (CAJA_SESION). Maneja índice único de ABIERTA por caja.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Abrir(CajaAperturaViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { ok = false, message = "Datos inválidos.", errors = ModelState });

            // Re-validar no haya otra abierta (capa extra de seguridad)
            var yaAbierta = await _context.CAJA_SESION
                .AnyAsync(s => !s.ELIMINADO && s.CAJA_ID == vm.CajaId && s.ESTADO_SESION == "ABIERTA", ct);

            if (yaAbierta)
                return StatusCode(409, new { ok = false, message = "La caja ya está aperturada." });

            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync();
            var ahora = DateTime.Now;
            var usuarioId = RequireUserId(); // ✅
            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            try
            {
                var sesionId = await _correlativos.NextCajaSesionIdAsync(ct);

                var entidad = new CAJA_SESION
                {
                    SESION_ID = sesionId,
                    CAJA_ID = vm.CajaId,
                    FECHA_APERTURA = ahora,
                    //USUARIO_APERTURA_ID = vm.UsuarioAperturaId,
                    USUARIO_APERTURA_ID = usuarioId, // ✅ no confiar en el form
                    MONTO_INICIAL = vm.MontoInicial,
                    NOTAAPERTURA = vm.NotaApertura,
                    ESTADO_SESION = "ABIERTA",

                    // Auditoría
                    CREADO_POR = usuarioNombre,
                    FECHA_CREACION = ahora,
                    ELIMINADO = false,
                    ESTADO = "ACTIVO"
                };

                _context.CAJA_SESION.Add(entidad);
                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return Ok(new
                {
                    ok = true,
                    message = $"Caja aperturada correctamente. Sesión: {sesionId}.",
                    sesionId
                });
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync(ct);
                // Puede ser violación del índice único UX_CAJA_SESION_CAJA_ABIERTA
                return StatusCode(409, new { ok = false, message = $"No se pudo abrir la caja: {ex.GetBaseException().Message}" });
            }
        }

        // ============================================================
        // GET: /CajaSesion/Cerrar?cajaId=CAJ000001
        // Render del modal de cierre (parcial Razor).
        // Si NO hay ABIERTA -> 404 + JSON para SweetAlert2.
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> Cerrar(string cajaId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cajaId))
                return BadRequest(new { ok = false, message = "Debe indicar la caja." });

            var sesion = await _context.CAJA_SESION
                .AsNoTracking()
                .Where(s => !s.ELIMINADO && s.CAJA_ID == cajaId && s.ESTADO_SESION == "ABIERTA")
                .OrderByDescending(s => s.FECHA_APERTURA)
                .FirstOrDefaultAsync(ct);

            if (sesion == null)
                return NotFound(new { ok = false, message = "No se encontró ninguna caja aperturada." });

            var tot = await _context.MOVIMIENTO_CAJA
                .AsNoTracking()
                .Where(m => !m.ELIMINADO && m.SESION_ID == sesion.SESION_ID)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Ingresos = g.Where(m => m.TIPO == "INGRESO").Sum(m => (decimal?)m.MONTO) ?? 0m,
                    Egresos = g.Where(m => m.TIPO == "EGRESO").Sum(m => (decimal?)m.MONTO) ?? 0m
                })
                .FirstOrDefaultAsync(ct) ?? new { Ingresos = 0m, Egresos = 0m };

            var usuarioId = RequireUserId();
            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync();

            var esperado = Math.Round((((decimal?)sesion.MONTO_INICIAL) ?? 0m) + tot.Ingresos - tot.Egresos, 2);

            var vm = new CajaCierreViewModel
            {
                SesionId = sesion.SESION_ID,
                CajaId = cajaId,
                CajaNombre = await _context.CAJA
                    .Where(c => c.CAJA_ID == cajaId)
                    .Select(c => c.CAJA_NOMBRE)
                    .FirstOrDefaultAsync(ct),

                FechaApertura = sesion.FECHA_APERTURA,
                MontoInicial = ((decimal?)sesion.MONTO_INICIAL) ?? 0m,

                // 👇 Estos dos SON CLAVE para que el POST sea válido
                UsuarioCierreId = usuarioId,
                UsuarioCierreNombre = usuarioNombre,

                FechaCierre = DateTime.Now,

                TotalIngresos = tot.Ingresos,
                TotalEgresos = tot.Egresos,
                SaldoEsperado = esperado
            };

            return PartialView("~/Views/CajaSesion/_CierreModal.cshtml", vm);
        }









        // ============================================================
        // POST: /CajaSesion/Cerrar
        // Cierra la sesión (ESTADO_SESION=CERRADA) y guarda totales de cierre.
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cerrar(CajaCierreViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { ok = false, message = "Datos inválidos.", errors = ModelState });

            var sesion = await _context.CAJA_SESION
                .FirstOrDefaultAsync(s => s.SESION_ID == vm.SesionId && !s.ELIMINADO, ct);

            if (sesion == null || !string.Equals(sesion.ESTADO_SESION, "ABIERTA", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { ok = false, message = "No se encontró ninguna caja aperturada." });

            // Recalcular totales/esperado en servidor
            var tot = await _context.MOVIMIENTO_CAJA
                .AsNoTracking()
                .Where(m => !m.ELIMINADO && m.SESION_ID == sesion.SESION_ID)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Ingresos = g.Where(m => m.TIPO == "INGRESO").Sum(m => (decimal?)m.MONTO) ?? 0m,
                    Egresos = g.Where(m => m.TIPO == "EGRESO").Sum(m => (decimal?)m.MONTO) ?? 0m
                })
                .FirstOrDefaultAsync(ct) ?? new { Ingresos = 0m, Egresos = 0m };

            var esperado = Math.Round((((decimal?)sesion.MONTO_INICIAL) ?? 0m) + tot.Ingresos - tot.Egresos, 2);
            var diferencia = Math.Round(vm.MontoFinal - esperado, 2);

            // Nota obligatoria si no cuadra
            if (diferencia != 0m && string.IsNullOrWhiteSpace(vm.NotaCierre))
                return BadRequest(new { ok = false, message = "Si el efectivo no cuadra, la nota de cierre es obligatoria." });

            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync();
            var usuarioId = RequireUserId();
            var ahora = DateTime.Now;

            sesion.FECHA_CIERRE = ahora;
            sesion.USUARIO_CIERRE_ID = usuarioId;
            sesion.MONTO_FINAL = vm.MontoFinal;
            sesion.NOTACIERRE = string.IsNullOrWhiteSpace(vm.NotaCierre) ? null : vm.NotaCierre.Trim();
            sesion.ESTADO_SESION = "CERRADA";
            sesion.MODIFICADO_POR = usuarioNombre;
            sesion.FECHA_MODIFICACION = ahora;

            try
            {
                await _context.SaveChangesAsync(ct);
                return Ok(new
                {
                    ok = true,
                    message = "Caja cerrada correctamente.",
                    sesionId = sesion.SESION_ID,
                    diferencia
                });
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(409, new { ok = false, message = $"No se pudo cerrar la caja: {ex.GetBaseException().Message}" });
            }
        }










        [HttpGet]
        public async Task<IActionResult> VentaDiaria(string sesionId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(sesionId)) return NotFound();

            var ses = await _context.CAJA_SESION.AsNoTracking()
                .FirstOrDefaultAsync(s => s.SESION_ID == sesionId && !s.ELIMINADO, ct);
            if (ses == null) return NotFound();

            var cajaNombre = await _context.CAJA.AsNoTracking()
                .Where(c => c.CAJA_ID == ses.CAJA_ID)
                .Select(c => c.CAJA_NOMBRE)
                .FirstOrDefaultAsync(ct) ?? ses.CAJA_ID;

            // Ventas ligadas a MOVIMIENTO_CAJA (INGRESO) de la sesión
            var ventas = await _context.MOVIMIENTO_CAJA.AsNoTracking()
                .Where(m => !m.ELIMINADO && m.SESION_ID == sesionId && m.TIPO == "INGRESO" && m.REFERENCIA.StartsWith("V"))
                .Join(_context.VENTA.AsNoTracking(),
                      m => m.REFERENCIA, v => v.VENTA_ID,
                      (m, v) => new { v.VENTA_ID, v.FECHA, v.TOTAL, v.CLIENTE_ID, v.USUARIO_ID })
                .OrderBy(x => x.FECHA)
                .ToListAsync(ct);

            var items = ventas.Select(x => new AbarroteriaKary.ModelsPartial.CajaVentaDiariaItemVM
            {
                VentaId = x.VENTA_ID,
                Fecha = x.FECHA,
                ClienteId = x.CLIENTE_ID,
                ClienteNombre = x.CLIENTE_ID, // si quieres, resuelve PERSONA aquí
                Total = x.TOTAL
            }).ToList();

            var vm = new AbarroteriaKary.ModelsPartial.CajaVentaDiariaPdfVM
            {
                SesionId = sesionId,
                CajaId = ses.CAJA_ID,
                CajaNombre = cajaNombre,
                FechaApertura = ses.FECHA_APERTURA,
                FechaCorte = DateTime.Now,
                UsuarioNombre = await _auditoria.GetUsuarioNombreAsync(),
                Total = items.Sum(i => i.Total),
                Ventas = items
            };

            return new ViewAsPdf("_VentaDiariaPdf", vm)
            {
                FileName = $"VentaDiaria_{sesionId}.pdf",
                PageSize = Size.A4,
                PageOrientation = Orientation.Portrait,
                PageMargins = new Margins { Top = 10, Right = 10, Bottom = 10, Left = 10 },
                ContentDisposition = ContentDisposition.Inline
            };
        }














    }
}
