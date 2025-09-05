using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.ModelsPartial.Paginacion;           // PaginadoViewModel<T>
using AbarroteriaKary.Services.Auditoria;
using AbarroteriaKary.Services.Correlativos;
using AbarroteriaKary.Services.Extensions;                // ToPagedAsync extension
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;


namespace AbarroteriaKary.Controllers
{
    public class ClientesController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;
        private readonly IAuditoriaService _auditoria;
        public ClientesController(KaryDbContext context, ICorrelativoService correlativos, IAuditoriaService auditoria)
        {
            _context = context;
            _correlativos = correlativos;
            _auditoria = auditoria;
        }

        // GET: Clientes
        public async Task<IActionResult> Index(
     string? estado, string? q = null,
     string? fDesde = null, string? fHasta = null,
     int page = 1, int pageSize = 10,
     CancellationToken ct = default)
        {
            // 0) Normalización de estado
            var estadoNorm = (estado ?? "ACTIVO").Trim().ToUpperInvariant();
            if (estadoNorm is not ("ACTIVO" or "INACTIVO" or "TODOS"))
                estadoNorm = "ACTIVO";

            // 1) Fechas (acepta dd/MM/yyyy o yyyy-MM-dd). Inclusivo.
            DateTime? desde = ParseDate(fDesde);
            DateTime? hasta = ParseDate(fHasta);

            // 2) Base query con JOIN CLIENTE-PERSONA (ignorando eliminados)
            var qry = from cli in _context.CLIENTE.AsNoTracking()
                      join per in _context.PERSONA.AsNoTracking() on cli.CLIENTE_ID equals per.PERSONA_ID
                      where !cli.ELIMINADO && !per.ELIMINADO
                      select new { cli, per };

            // 3) Filtro por estado (usamos el de CLIENTE; PERSONA debe ir sincronizado)
            if (estadoNorm is "ACTIVO" or "INACTIVO")
                qry = qry.Where(x => x.cli.ESTADO == estadoNorm);

            // 4) Búsqueda por texto (ID CLIENTE, Nombre completo, CUI, NIT, Teléfono, Dirección, Nota)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";

                // Construir nombre completo en SQL (para EF.Functions.Like)
                var nombreCompleto = qry.Select(x => new
                {
                    x,
                    FullName =
                        (x.per.PERSONA_PRIMERNOMBRE ?? "") + " " +
                        (x.per.PERSONA_SEGUNDONOMBRE ?? "") + " " +
                        (x.per.PERSONA_TERCERNOMBRE ?? "") + " " +
                        (x.per.PERSONA_PRIMERAPELLIDO ?? "") + " " +
                        (x.per.PERSONA_SEGUNDOAPELLIDO ?? "")
                });

                qry = nombreCompleto
                    .Where(y =>
                        EF.Functions.Like(y.x.cli.CLIENTE_ID, term) ||
                        EF.Functions.Like(y.FullName, term) ||
                        (y.x.per.PERSONA_CUI != null && EF.Functions.Like(y.x.per.PERSONA_CUI, term)) ||
                        (y.x.per.PERSONA_NIT != null && EF.Functions.Like(y.x.per.PERSONA_NIT, term)) ||
                        (y.x.per.PERSONA_TELEFONOMOVIL != null && EF.Functions.Like(y.x.per.PERSONA_TELEFONOMOVIL, term)) ||
                        (y.x.per.PERSONA_DIRECCION != null && EF.Functions.Like(y.x.per.PERSONA_DIRECCION, term)) ||
                        (y.x.cli.CLIENTE_NOTA != null && EF.Functions.Like(y.x.cli.CLIENTE_NOTA, term))
                    )
                    .Select(y => y.x); // volver a { cli, per }
            }

            // 5) Rango de fechas por FECHA_CREACION de CLIENTE (inclusivo)
            if (desde.HasValue) qry = qry.Where(x => x.cli.FECHA_CREACION >= desde.Value.Date);
            if (hasta.HasValue) qry = qry.Where(x => x.cli.FECHA_CREACION < hasta.Value.Date.AddDays(1));

            // 6) Orden y proyección a ViewModel (ANTES de paginar)
            var proyectado = qry
                .OrderBy(x => x.cli.CLIENTE_ID)
                .Select(x => new ClienteViewModel
                {
                    // Identificador
                    Id = x.cli.CLIENTE_ID,

                    // Persona
                    PrimerNombre = x.per.PERSONA_PRIMERNOMBRE,
                    SegundoNombre = x.per.PERSONA_SEGUNDONOMBRE,
                    TercerNombre = x.per.PERSONA_TERCERNOMBRE,
                    PrimerApellido = x.per.PERSONA_PRIMERAPELLIDO,
                    SegundoApellido = x.per.PERSONA_SEGUNDOAPELLIDO,

                    // Identificación / contacto (PERSONA)
                    NIT = x.per.PERSONA_NIT,
                    CUI = x.per.PERSONA_CUI,
                    Direccion = x.per.PERSONA_DIRECCION,
                    TelefonoMovil = x.per.PERSONA_TELEFONOMOVIL,

                    // Cliente
                    clienteNota = x.cli.CLIENTE_NOTA,

                    // Estado
                    ESTADO = x.cli.ESTADO,
                    EstadoActivo = x.cli.ESTADO == "ACTIVO"
                });

            // 7) Paginación (normalizar pageSize)
            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 10;

            var resultado = await proyectado.ToPagedAsync(page, pageSize, ct);

            // 8) RouteValues para mantener filtros en el pager
            resultado.RouteValues["estado"] = estadoNorm;
            resultado.RouteValues["q"] = q;
            resultado.RouteValues["fDesde"] = desde?.ToString("yyyy-MM-dd");
            resultado.RouteValues["fHasta"] = hasta?.ToString("yyyy-MM-dd");

            // Toolbar (persistencia de filtros en la vista)
            ViewBag.Estado = estadoNorm;
            ViewBag.Q = q;
            ViewBag.FDesde = resultado.RouteValues["fDesde"];
            ViewBag.FHasta = resultado.RouteValues["fHasta"];

            return View(resultado);
        }

        // === Utilidad local para parsear fechas ===
        [NonAction]
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
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            var vm = new ClienteViewModel
            {
                // ID “preview” (no reserva todavía)
                Id = await _correlativos.PeekNextClienteIdIdAsync(ct),
                ESTADO = "ACTIVO",
                EstadoActivo = true
            };

            // (No hay combos por ahora)
            return View(vm);
        }

        // =========================================================
        // POST: Clientes/Create
        // - Valida VM
        // - Verifica unicidad de CUI y NIT (permite “C/F” o “CF”)
        // - Reserva ID definitivo con Next* dentro de transacción
        // - Inserta PERSONA + CLIENTE con auditoría
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClienteViewModel vm, CancellationToken ct)
        {
            // 1) Sincronizar estado UI -> BD
            vm.ESTADO = vm.EstadoActivo ? "ACTIVO" : "INACTIVO";

            // 2) Normalizaciones mínimas
            if (!string.IsNullOrWhiteSpace(vm.NIT))
                vm.NIT = vm.NIT.Trim().ToUpperInvariant();

            if (!string.IsNullOrWhiteSpace(vm.CUI))
                vm.CUI = vm.CUI.Trim();

            // 3) Validación extra de NIT (permite C/F)
            if (!string.IsNullOrWhiteSpace(vm.NIT))
            {
                bool esCF = vm.NIT is "C/F" or "CF";
                if (!esCF)
                {
                    var ok = System.Text.RegularExpressions.Regex.IsMatch(
                        vm.NIT, @"^[0-9]{1,8}-?[0-9Kk]$"
                    );
                    if (!ok)
                        ModelState.AddModelError(nameof(vm.NIT),
                            "NIT inválido (ej.: 1234567-8 o 1234567K). Use C/F para consumidor final.");
                }
            }

            // 4) Si ya hay fallos de DataAnnotations o NIT, re-mostrar
            if (!ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(vm.Id))
                    vm.Id = await _correlativos.PeekNextClienteIdIdAsync(ct);
                return View(vm);
            }

            var ahora = DateTime.Now;
            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync();

            // 5) Unicidad CUI si viene
            if (!string.IsNullOrWhiteSpace(vm.CUI))
            {
                bool existeCui = await _context.PERSONA
                    .AsNoTracking()
                    .AnyAsync(p => !p.ELIMINADO
                                   && p.PERSONA_CUI != null
                                   && p.PERSONA_CUI.Trim() == vm.CUI, ct);
                if (existeCui)
                    ModelState.AddModelError(nameof(vm.CUI), "Ya existe una persona con ese CUI.");
            }

            // 6) Unicidad NIT si viene y NO es C/F
            if (!string.IsNullOrWhiteSpace(vm.NIT) && vm.NIT is not ("C/F" or "CF"))
            {
                bool existeNit = await _context.PERSONA
                    .AsNoTracking()
                    .AnyAsync(p => !p.ELIMINADO
                                   && p.PERSONA_NIT != null
                                   && p.PERSONA_NIT.Trim().ToUpper() == vm.NIT, ct);
                if (existeNit)
                    ModelState.AddModelError(nameof(vm.NIT), "Ya existe una persona con ese NIT.");
            }

            if (!ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(vm.Id))
                    vm.Id = await _correlativos.PeekNextClienteIdIdAsync(ct);
                return View(vm);
            }

            // 7) Transacción y reserva de ID definitiva
            await using var tx = await _context.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, ct);

            try
            {
                // ⚠️ Aquí debe ser Next* (reserva definitiva del ID)
                var nuevoId = await _correlativos.NextClienteIdIdAsync(ct);

                // Mapear PERSONA desde VM
                var persona = new PERSONA
                {
                    PERSONA_ID = nuevoId,
                    PERSONA_PRIMERNOMBRE = (vm.PrimerNombre ?? string.Empty).Trim(),
                    PERSONA_SEGUNDONOMBRE = vm.SegundoNombre?.Trim(),
                    PERSONA_TERCERNOMBRE = vm.TercerNombre?.Trim(),
                    PERSONA_PRIMERAPELLIDO = (vm.PrimerApellido ?? string.Empty).Trim(),
                    PERSONA_SEGUNDOAPELLIDO = vm.SegundoApellido?.Trim(),
                    PERSONA_NIT = vm.NIT,        // puede ser “C/F”
                    PERSONA_CUI = vm.CUI,
                    PERSONA_DIRECCION = vm.Direccion?.Trim(),
                    PERSONA_TELEFONOMOVIL = vm.TelefonoMovil?.Trim(),
                    PERSONA_CORREO = null,          // <- el VM no trae Correo; agregue si lo incluye
                    PERSONA_TELEFONOCASA = null,          // <- opcional si desea luego
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false,
                    CREADO_POR = usuarioNombre,
                    FECHA_CREACION = ahora
                };

                // Mapear CLIENTE desde VM
                var cliente = new CLIENTE
                {
                    CLIENTE_ID = nuevoId,               // = PERSONA_ID
                    CLIENTE_NOTA = vm.clienteNota,
                    ESTADO = vm.ESTADO,
                    ELIMINADO = false,
                    CREADO_POR = usuarioNombre,
                    FECHA_CREACION = ahora
                };

                _context.Add(persona);
                _context.Add(cliente);
                await _context.SaveChangesAsync(ct);

                await tx.CommitAsync(ct);

                // Mensajes de confirmación para la vista
                TempData["SavedOk"] = true;
                TempData["SavedName"] =
                    $"{persona.PERSONA_PRIMERNOMBRE} {persona.PERSONA_PRIMERAPELLIDO}".Trim();

                // Redirigir a Create para ingresar otro (mismo patrón que Empleado)
                return RedirectToAction(nameof(Create));
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync(ct);

                ModelState.AddModelError(string.Empty, $"Error BD: {ex.GetBaseException().Message}");

                if (string.IsNullOrWhiteSpace(vm.Id))
                    vm.Id = await _correlativos.PeekNextClienteIdIdAsync(ct);

                return View(vm);
            }
        }





        // ================================
        // EDIT / GET
        // - Traer CLIENTE + PERSONA (sin tracking)
        // - Proyección al ClienteViewModel
        // - Auditoría en ViewBag para card (opcional)
        // - Flags de resultado de actualización (PRG)
        // ================================
        [HttpGet]
        public async Task<IActionResult> Edit(string id, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // Traer CLIENTE + PERSONA (sin tracking)
            var data = await (from cli in _context.CLIENTE.AsNoTracking()
                              join per in _context.PERSONA.AsNoTracking()
                                on cli.CLIENTE_ID equals per.PERSONA_ID
                              where cli.CLIENTE_ID == id
                              select new { cli, per })
                             .FirstOrDefaultAsync(ct);

            if (data is null) return NotFound();

            // Proyección a VM
            var vm = new ClienteViewModel
            {
                // Identificador compartido
                Id = data.cli.CLIENTE_ID,

                // PERSONA
                PrimerNombre = data.per.PERSONA_PRIMERNOMBRE,
                SegundoNombre = data.per.PERSONA_SEGUNDONOMBRE,
                TercerNombre = data.per.PERSONA_TERCERNOMBRE,
                PrimerApellido = data.per.PERSONA_PRIMERAPELLIDO,
                SegundoApellido = data.per.PERSONA_SEGUNDOAPELLIDO,
                NIT = data.per.PERSONA_NIT,
                CUI = data.per.PERSONA_CUI,
                Direccion = data.per.PERSONA_DIRECCION,
                TelefonoMovil = data.per.PERSONA_TELEFONOMOVIL,

                // CLIENTE
                clienteNota = data.cli.CLIENTE_NOTA,

                // Estado (checkbox + string)
                ESTADO = data.cli.ESTADO,
                EstadoActivo = string.Equals(data.cli.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),
            };

            // Auditoría (opcional para mostrar en card)
            ViewBag.Auditoria = new
            {
                CreadoPor = data.cli.CREADO_POR,
                FechaCreacion = data.cli.FECHA_CREACION,
                ModificadoPor = data.cli.MODIFICADO_POR,
                FechaModificacion = data.cli.FECHA_MODIFICACION,
                Eliminado = data.cli.ELIMINADO,
                EliminadoPor = data.cli.ELIMINADO_POR,
                FechaEliminacion = data.cli.FECHA_ELIMINACION
            };

            // Flags de resultado de actualización (PRG)
            ViewBag.UpdatedOk = TempData["UpdatedOk"];
            ViewBag.UpdatedName = TempData["UpdatedName"];
            ViewBag.NoChanges = TempData["NoChanges"];

            return View(vm);
        }

        // ================================
        // EDIT / POST
        // - Normaliza datos desde VM
        // - Deriva estado del checkbox
        // - Compara para detectar “sin cambios”
        // - Aplica cambios a PERSONA + CLIENTE
        // - Auditoría y PRG
        // ================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, ClienteViewModel vm, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(id) || !string.Equals(id, vm.Id, StringComparison.Ordinal))
                return NotFound();

            if (!ModelState.IsValid)
                return View(vm);

            // Entidades con tracking
            var cliente = await _context.CLIENTE.FirstOrDefaultAsync(c => c.CLIENTE_ID == id, ct);
            var persona = await _context.PERSONA.FirstOrDefaultAsync(p => p.PERSONA_ID == id, ct);
            if (cliente is null || persona is null) return NotFound();

            // ===== Normalización de datos nuevos desde el VM =====
            string nPrimerNombre = (vm.PrimerNombre ?? "").Trim();
            string nSegundoNombre = vm.SegundoNombre?.Trim();
            string nTercerNombre = vm.TercerNombre?.Trim();
            string nPrimerApellido = (vm.PrimerApellido ?? "").Trim();
            string nSegundoApellido = vm.SegundoApellido?.Trim();
            string nNit = vm.NIT?.Trim().ToUpperInvariant();
            string nCui = vm.CUI?.Trim();
            string nDireccion = vm.Direccion?.Trim();
            string nTelefono = vm.TelefonoMovil?.Trim();

            string nClienteNota = vm.clienteNota?.Trim();

            // Estado derivado del checkbox
            var nEstado = vm.EstadoActivo ? "ACTIVO" : "INACTIVO";

            // ===== ¿Hay cambios? (comparación estricta/ordinal) =====
            bool sinCambios =
                string.Equals(persona.PERSONA_PRIMERNOMBRE ?? "", nPrimerNombre, StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_SEGUNDONOMBRE ?? "", nSegundoNombre ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_TERCERNOMBRE ?? "", nTercerNombre ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_PRIMERAPELLIDO ?? "", nPrimerApellido, StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_SEGUNDOAPELLIDO ?? "", nSegundoApellido ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_NIT ?? "", nNit ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_CUI ?? "", nCui ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_DIRECCION ?? "", nDireccion ?? "", StringComparison.Ordinal) &&
                string.Equals(persona.PERSONA_TELEFONOMOVIL ?? "", nTelefono ?? "", StringComparison.Ordinal) &&

                string.Equals(cliente.CLIENTE_NOTA ?? "", nClienteNota ?? "", StringComparison.Ordinal) &&
                string.Equals(cliente.ESTADO ?? "", nEstado, StringComparison.Ordinal);

            if (sinCambios)
            {
                TempData["NoChanges"] = true;
                return RedirectToAction(nameof(Edit), new { id });
            }

            // ===== Auditoría =====
            var ahora = DateTime.Now;
            var usuarioNombre = await _auditoria.GetUsuarioNombreAsync();

            // ===== Aplicar cambios a PERSONA =====
            persona.PERSONA_PRIMERNOMBRE = nPrimerNombre;
            persona.PERSONA_SEGUNDONOMBRE = nSegundoNombre;
            persona.PERSONA_TERCERNOMBRE = nTercerNombre;
            persona.PERSONA_PRIMERAPELLIDO = nPrimerApellido;
            persona.PERSONA_SEGUNDOAPELLIDO = nSegundoApellido;
            persona.PERSONA_NIT = nNit;
            persona.PERSONA_CUI = nCui;
            persona.PERSONA_DIRECCION = nDireccion;
            persona.PERSONA_TELEFONOMOVIL = nTelefono;

            persona.MODIFICADO_POR = usuarioNombre;
            persona.FECHA_MODIFICACION = ahora;

            // Mantener ESTADO de PERSONA sincronizado con CLIENTE
            // (si PERSONA se usa para otros módulos y quiere independencia, puede omitir esta línea)
            persona.ESTADO = nEstado;

            // ===== Aplicar cambios a CLIENTE =====
            cliente.CLIENTE_NOTA = nClienteNota;

            var estadoOriginalActivo = string.Equals(cliente.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase);
            var estadoNuevoActivo = vm.EstadoActivo;

            if (estadoOriginalActivo != estadoNuevoActivo)
            {
                if (!estadoNuevoActivo)
                {
                    // → DESACTIVAR (NO tocar ELIMINADO: debe seguir listando)
                    cliente.ESTADO = "INACTIVO";
                    // Opcional: deje rastro de desactivación
                    cliente.ELIMINADO_POR = usuarioNombre;
                    cliente.FECHA_ELIMINACION = ahora;
                    cliente.ELIMINADO = false;
                }
                else
                {
                    // → REACTIVAR
                    cliente.ESTADO = "ACTIVO";
                    // Opcional: actualice rastro o límpielo según su política
                    cliente.ELIMINADO_POR = usuarioNombre;
                    cliente.FECHA_ELIMINACION = ahora;
                    cliente.ELIMINADO = false;
                }
            }
            else
            {
                // Sin cambio de estado, sincronizamos explícitamente
                cliente.ESTADO = nEstado;
            }

            cliente.MODIFICADO_POR = usuarioNombre;
            cliente.FECHA_MODIFICACION = ahora;

            try
            {
                await _context.SaveChangesAsync(ct);

                TempData["UpdatedOk"] = true;
                TempData["UpdatedName"] =
                    $"{persona.PERSONA_PRIMERNOMBRE} {persona.PERSONA_PRIMERAPELLIDO}".Trim();

                return RedirectToAction(nameof(Edit), new { id });
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.CLIENTE.AnyAsync(e => e.CLIENTE_ID == id, ct))
                    return NotFound();
                throw;
            }
        }






        public async Task<IActionResult> Details(string id, CancellationToken ct)
        {
            // 1) Validación de parámetro
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // 2) Carga CLIENTE + PERSONA (solo lectura)
            var data = await (from cli in _context.CLIENTE.AsNoTracking()
                              join per in _context.PERSONA.AsNoTracking()
                                   on cli.CLIENTE_ID equals per.PERSONA_ID
                              where cli.CLIENTE_ID == id
                              select new { cli, per })
                             .FirstOrDefaultAsync(ct);

            if (data is null) return NotFound();

            // 3) Proyección a ViewModel (lo que usaremos en la vista)
            var vm = new ClienteViewModel
            {
                // Identificador compartido
                Id = data.cli.CLIENTE_ID,

                // PERSONA
                PrimerNombre = data.per.PERSONA_PRIMERNOMBRE,
                SegundoNombre = data.per.PERSONA_SEGUNDONOMBRE,
                TercerNombre = data.per.PERSONA_TERCERNOMBRE,
                PrimerApellido = data.per.PERSONA_PRIMERAPELLIDO,
                SegundoApellido = data.per.PERSONA_SEGUNDOAPELLIDO,
                NIT = data.per.PERSONA_NIT,
                CUI = data.per.PERSONA_CUI,
                Direccion = data.per.PERSONA_DIRECCION,
                TelefonoMovil = data.per.PERSONA_TELEFONOMOVIL,

                // CLIENTE
                clienteNota = data.cli.CLIENTE_NOTA,

                // Estado (checkbox + string)
                ESTADO = data.cli.ESTADO,
                EstadoActivo = string.Equals(data.cli.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase)
            };

            // 4) Auditoría (opcional): disponible en la vista si la quieres mostrar
            ViewBag.Auditoria = new
            {
                CreadoPor = data.cli.CREADO_POR,
                FechaCreacion = data.cli.FECHA_CREACION,
                ModificadoPor = data.cli.MODIFICADO_POR,
                FechaModificacion = data.cli.FECHA_MODIFICACION,
                EliminadoPor = data.cli.ELIMINADO_POR,
                FechaEliminacion = data.cli.FECHA_ELIMINACION
            };

            // 5) Devolver la vista tipada al ViewModel
            return View(vm);
        }




















        // GET: Clientes/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cLIENTE = await _context.CLIENTE
                .Include(c => c.CLIENTENavigation)
                .FirstOrDefaultAsync(m => m.CLIENTE_ID == id);
            if (cLIENTE == null)
            {
                return NotFound();
            }

            return View(cLIENTE);
        }

        // POST: Clientes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var cLIENTE = await _context.CLIENTE.FindAsync(id);
            if (cLIENTE != null)
            {
                _context.CLIENTE.Remove(cLIENTE);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CLIENTEExists(string id)
        {
            return _context.CLIENTE.Any(e => e.CLIENTE_ID == id);
        }
    }
}
