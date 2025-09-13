using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.ModelsPartial.Paginacion;           // PaginadoViewModel<T>
using AbarroteriaKary.Services.Auditoria;
using AbarroteriaKary.Services.Correlativos;
using AbarroteriaKary.Services.Extensions;                // ToPagedAsync extension
using AbarroteriaKary.Services.Reportes;
using AbarroteriaKary.Services.Security;
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
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace AbarroteriaKary.Controllers
{
    public class ProductosController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;
        private readonly IAuditoriaService _auditoria;
        private readonly IReporteExportService _exportSvc;
        private readonly IWebHostEnvironment _env;


        // === Config de imágenes ===
        private static readonly string[] ImgExtPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly string[] ImgContentTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        private const long ImgMaxBytes = 2 * 1024 * 1024; // 2 MB
        private const string UploadRelPath = "/uploads/productos"; // Ruta relativa para servir en web



        public ProductosController(KaryDbContext context, ICorrelativoService correlativos, IAuditoriaService auditoria, IReporteExportService exportSvc, IWebHostEnvironment env)
        {
            _context = context;
            _correlativos = correlativos;
            _auditoria = auditoria;
            _exportSvc = exportSvc;
            _env = env;
        }




        // GET: Productos
        // ==========================
        // GET: Producto/Index
        // ==========================
        public async Task<IActionResult> Index(
            string? estado,
            string? q = null,
            string? fDesde = null,
            string? fHasta = null,
            int page = 1,
            int pageSize = 10)
        {
            // 0) Normalización de estado
            var estadoNorm = (estado ?? "ACTIVO").Trim().ToUpperInvariant();
            if (estadoNorm is not ("ACTIVO" or "INACTIVO" or "TODOS"))
                estadoNorm = "ACTIVO";

            // 1) Fechas (acepta dd/MM/yyyy o yyyy-MM-dd)
            DateTime? desde = ParseDate(fDesde);
            DateTime? hasta = ParseDate(fHasta);

            // 2) Base query (ignora eliminados) con joins necesarios para mostrar Subcategoría y Marca
            var baseQry =
                from p in _context.PRODUCTO.AsNoTracking()
                where !p.ELIMINADO
                join sc in _context.SUBCATEGORIA.AsNoTracking() on p.SUBCATEGORIA_ID equals sc.SUBCATEGORIA_ID
                // LEFT JOIN Marca (es opcional en la tabla)
                join m0 in _context.MARCA.AsNoTracking() on p.MARCA_ID equals m0.MARCA_ID into jm
                from m in jm.DefaultIfEmpty()
                select new { P = p, SC = sc, M = m };

            // 3) Filtro por estado
            if (estadoNorm is "ACTIVO" or "INACTIVO")
                baseQry = baseQry.Where(x => x.P.ESTADO == estadoNorm);

            // 4) Búsqueda por texto (ID, Nombre, Código, Subcategoría, Marca)
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = $"%{q.Trim()}%";
                baseQry = baseQry.Where(x =>
                    EF.Functions.Like(x.P.PRODUCTO_ID, term) ||
                    EF.Functions.Like(x.P.PRODUCTO_NOMBRE, term) ||
                    EF.Functions.Like(x.P.PRODUCTO_CODIGO ?? "", term) ||
                    EF.Functions.Like(x.SC.SUBCATEGORIA_NOMBRE, term) ||
                    EF.Functions.Like(x.M != null ? x.M.MARCA_NOMBRE : "", term)
                );
            }

            // 5) Rango de fechas (inclusivo)
            if (desde.HasValue) baseQry = baseQry.Where(x => x.P.FECHA_CREACION >= desde.Value.Date);
            if (hasta.HasValue) baseQry = baseQry.Where(x => x.P.FECHA_CREACION < hasta.Value.Date.AddDays(1));

            // 6) Orden + Proyección a VM (ANTES de paginar)
            var proyectado = baseQry
                .OrderBy(x => x.P.PRODUCTO_ID)
                .Select(x => new ProductoListItemViewModel
                {
                    ProductoId = x.P.PRODUCTO_ID,
                    ProductoNombre = x.P.PRODUCTO_NOMBRE,
                    CodigoProducto = x.P.PRODUCTO_CODIGO,
                    SubCategoriaNombre = x.SC.SUBCATEGORIA_NOMBRE,
                    MarcaNombre = x.M != null ? x.M.MARCA_NOMBRE : null,
                    ImagenUrl = x.P.PRODUCTO_IMG,          // ← se mostrará en miniatura uniforme
                    FechaCreacion = x.P.FECHA_CREACION,
                    ESTADO = x.P.ESTADO
                });

            // 7) Paginación (normaliza pageSize)
            var permitidos = new[] { 10, 25, 50, 100 };
            pageSize = permitidos.Contains(pageSize) ? pageSize : 10;

            var resultado = await proyectado.ToPagedAsync(page, pageSize); // tu extensión de paginado

            // 8) RouteValues para el pager (persistir filtros)
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
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // 1) Proyección directa al VM (eficiente)
            var vm = await _context.PRODUCTO
                .AsNoTracking()
                .Where(p => p.PRODUCTO_ID == id)
                .Select(p => new ProductoDetailsViewModel
                {
                    // Datos principales
                    ProductoId = p.PRODUCTO_ID,
                    ProductoNombre = p.PRODUCTO_NOMBRE,
                    CodigoProducto = p.PRODUCTO_CODIGO,
                    ProductoDescripcion = p.PRODUCTO_DESCRIPCION,

                    // Catálogos (navegaciones)
                    SubCategoriaId = p.SUBCATEGORIA_ID,
                    SubCategoriaNombre = p.SUBCATEGORIA.SUBCATEGORIA_NOMBRE,
                    TipoProductoId = p.TIPO_PRODUCTO_ID,
                    TipoProductoNombre = p.TIPO_PRODUCTO.TIPO_PRODUCTO_NOMBRE,
                    UnidadMedidaId = p.UNIDAD_MEDIDA_ID,
                    UnidadMedidaNombre = p.UNIDAD_MEDIDA.UNIDAD_MEDIDA_NOMBRE,
                    MaterialEnvaseId = p.MATERIAL_ENVASE_ID,
                    MaterialEnvaseNombre = p.MATERIAL_ENVASE != null ? p.MATERIAL_ENVASE.MATERIAL_ENVASE_NOMBRE : null,
                    TipoEmpaqueId = p.TIPO_EMPAQUE_ID,
                    TipoEmpaqueNombre = p.TIPO_EMPAQUE != null ? p.TIPO_EMPAQUE.TIPO_EMPAQUE_NOMBRE : null,
                    MarcaId = p.MARCA_ID,
                    MarcaNombre = p.MARCA != null ? p.MARCA.MARCA_NOMBRE : null,

                    // Imagen y estado
                    ImagenUrl = p.PRODUCTO_IMG,
                    ESTADO = p.ESTADO,
                    EstadoActivo = p.ESTADO == "ACTIVO",

                    // Auditoría
                    CreadoPor = p.CREADO_POR,
                    FechaCreacion = p.FECHA_CREACION,
                    ModificadoPor = p.MODIFICADO_POR,
                    FechaModificacion = p.FECHA_MODIFICACION,
                    EliminadoPor = p.ELIMINADO_POR,
                    FechaEliminacion = p.FECHA_ELIMINACION,
                    Eliminado = p.ELIMINADO
                })
                .FirstOrDefaultAsync();

            if (vm is null) return NotFound();

            // 2) ★ Muy importante: Poner la auditoría en ViewBag para que la vista renderice el toggle
            ViewBag.Auditoria = new
            {
                CreadoPor = vm.CreadoPor,
                FechaCreacion = vm.FechaCreacion,
                ModificadoPor = vm.ModificadoPor,
                FechaModificacion = vm.FechaModificacion,
                EliminadoPor = vm.EliminadoPor,
                FechaEliminacion = vm.FechaEliminacion
            };

            return View(vm);
        }














        // GET: Producto/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new ProductoViewModel
            {
                // Solo informativo; el definitivo se asigna en POST con Next...
                ProductoId = await _correlativos.PeekNextProductoIdAsync(),
                ESTADO = "ACTIVO"
            };

            await CargarCombosAsync(vm);

            // Flags para modal de éxito (opcional)
            ViewBag.SavedOk = TempData["SavedOk"];
            ViewBag.SavedName = TempData["SavedName"];

            return View(vm);
        }

        // POST: Producto/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductoViewModel vm)
        {
            // DataAnnotations
            if (!ModelState.IsValid)
            {
                if (string.IsNullOrWhiteSpace(vm.ProductoId))
                    vm.ProductoId = await _correlativos.PeekNextProductoIdAsync();

                await CargarCombosAsync(vm);
                return View(vm);
            }

            // === Validaciones de negocio opcionales ===
            // Ejemplo: si tiene código, evitar duplicados
            if (!string.IsNullOrWhiteSpace(vm.CodigoProducto))
            {
                var codExiste = await _context.PRODUCTO
                    .AnyAsync(p => !p.ELIMINADO && p.PRODUCTO_CODIGO == vm.CodigoProducto);
                if (codExiste)
                {
                    ModelState.AddModelError(nameof(vm.CodigoProducto), "El código de producto ya existe.");
                    if (string.IsNullOrWhiteSpace(vm.ProductoId))
                        vm.ProductoId = await _correlativos.PeekNextProductoIdAsync();
                    await CargarCombosAsync(vm);
                    return View(vm);
                }
            }

            // ====== Auditoría ======
            var ahora = DateTime.Now;
            var creadoPor = (await (_auditoria?.GetUsuarioNombreAsync() ?? Task.FromResult(User?.Identity?.Name)))
                            ?? "Sistema";

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            // ← OJO: el sistema de archivos no participa en la transacción de BD.
            // Si algo falla después de guardar la imagen, intentamos eliminar el archivo.
            string? archivoFisicoGuardado = null;

            try
            {
                // 1) ID definitivo y único (atómico)
                var nuevoId = await _correlativos.NextProductoIdAsync();

                // 2) Procesar imagen (si viene)
                string? rutaRelImagen = null;
                if (vm.ImagenArchivo is { Length: > 0 })
                {
                    // 2.1) Validar extensión
                    var ext = Path.GetExtension(vm.ImagenArchivo.FileName)?.ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(ext) || !ImgExtPermitidas.Contains(ext))
                    {
                        ModelState.AddModelError(nameof(vm.ImagenArchivo),
                            $"Extensión no permitida. Use: {string.Join(", ", ImgExtPermitidas)}");
                        if (string.IsNullOrWhiteSpace(vm.ProductoId))
                            vm.ProductoId = await _correlativos.PeekNextProductoIdAsync();
                        await CargarCombosAsync(vm);
                        return View(vm);
                    }

                    // 2.2) Validar tamaño
                    if (vm.ImagenArchivo.Length > ImgMaxBytes)
                    {
                        ModelState.AddModelError(nameof(vm.ImagenArchivo),
                            $"La imagen supera el máximo de {ImgMaxBytes / (1024 * 1024)} MB.");
                        if (string.IsNullOrWhiteSpace(vm.ProductoId))
                            vm.ProductoId = await _correlativos.PeekNextProductoIdAsync();
                        await CargarCombosAsync(vm);
                        return View(vm);
                    }

                    // 2.3) (Opcional) Validar ContentType
                    if (!string.IsNullOrWhiteSpace(vm.ImagenArchivo.ContentType) &&
                        !ImgContentTypes.Contains(vm.ImagenArchivo.ContentType.ToLowerInvariant()))
                    {
                        ModelState.AddModelError(nameof(vm.ImagenArchivo),
                            "Tipo de contenido no permitido. Use JPG, PNG o WEBP.");
                        if (string.IsNullOrWhiteSpace(vm.ProductoId))
                            vm.ProductoId = await _correlativos.PeekNextProductoIdAsync();
                        await CargarCombosAsync(vm);
                        return View(vm);
                    }

                    // 2.4) Guardado físico con nombre único: {ID}_{UTCtimestamp}{ext}
                    var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "productos");
                    Directory.CreateDirectory(uploadsDir);

                    var fileName = $"{nuevoId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
                    var physicalPath = Path.Combine(uploadsDir, fileName);

                    // Guardar
                    await using (var fs = System.IO.File.Create(physicalPath))
                        await vm.ImagenArchivo.CopyToAsync(fs);

                    archivoFisicoGuardado = physicalPath; // Para rollback si falla BD
                    rutaRelImagen = $"{UploadRelPath}/{fileName}".Replace('\\', '/'); // Para mostrar en web
                }

                // 3) Mapear VM -> Entidad EF
                var entidad = new PRODUCTO
                {
                    PRODUCTO_ID = nuevoId,
                    PRODUCTO_CODIGO = vm.CodigoProducto,
                    PRODUCTO_NOMBRE = vm.ProductoNombre,
                    PRODUCTO_DESCRIPCION = vm.ProductoDescripcion,
                    SUBCATEGORIA_ID = vm.SubCategoriaId,
                    TIPO_PRODUCTO_ID = vm.TipoProductoId,
                    UNIDAD_MEDIDA_ID = vm.UnidadMedidaId,
                    MATERIAL_ENVASE_ID = vm.MaterialEnvaseId,
                    TIPO_EMPAQUE_ID = vm.TipoEmpaqueId,
                    MARCA_ID = vm.MarcaId,
                    // Si se subió imagen nueva → priorizar; si no, use lo que venga en vm.ProductoImagen (p. ej. URL ya existente)
                    PRODUCTO_IMG = rutaRelImagen ?? vm.ProductoImagen,
                    //IVA_PORCENTAJE = vm.IvaPorcentaje,
                    ESTADO = vm.ESTADO,        // "ACTIVO"/"INACTIVO"
                    ELIMINADO = false,

                    // Auditoría
                    CREADO_POR = creadoPor,
                    FECHA_CREACION = ahora
                };

                _context.PRODUCTO.Add(entidad);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["SavedOk"] = true;
                TempData["SavedName"] = entidad.PRODUCTO_NOMBRE;
                return RedirectToAction(nameof(Create));
            }
            catch (DbUpdateException ex)
            {
                await tx.RollbackAsync();

                // Si ya guardamos la imagen pero falló BD → intentar eliminar el archivo
                if (!string.IsNullOrWhiteSpace(archivoFisicoGuardado) && System.IO.File.Exists(archivoFisicoGuardado))
                {
                    try { System.IO.File.Delete(archivoFisicoGuardado); }
                    catch { /* no interrumpir el flujo por esto */ }
                }

                ModelState.AddModelError(string.Empty, $"Error BD: {ex.GetBaseException().Message}");

                if (string.IsNullOrWhiteSpace(vm.ProductoId))
                    vm.ProductoId = await _correlativos.PeekNextProductoIdAsync();

                await CargarCombosAsync(vm);
                return View(vm);
            }
        }

        // Helpers de combos
        private async Task CargarCombosAsync(ProductoViewModel vm)
        {
            vm.Subcategorias = await _context.SUBCATEGORIA
                .AsNoTracking()
                .Where(x => !x.ELIMINADO && x.ESTADO == "ACTIVO")
                .OrderBy(x => x.SUBCATEGORIA_NOMBRE)
                .Select(x => new SelectListItem
                {
                    Value = x.SUBCATEGORIA_ID.Trim().ToUpper(),
                    Text = x.SUBCATEGORIA_NOMBRE
                })
                .ToListAsync();

            vm.TiposProducto = await _context.TIPO_PRODUCTO
                .AsNoTracking()
                .Where(x => !x.ELIMINADO && x.ESTADO == "ACTIVO")
                .OrderBy(x => x.TIPO_PRODUCTO_NOMBRE)
                .Select(x => new SelectListItem
                {
                    Value = x.TIPO_PRODUCTO_ID.Trim().ToUpper(),
                    Text = x.TIPO_PRODUCTO_NOMBRE
                })
                .ToListAsync();

            vm.UnidadesMedida = await _context.UNIDAD_MEDIDA
                .AsNoTracking()
                .Where(x => !x.ELIMINADO && x.ESTADO == "ACTIVO")
                .OrderBy(x => x.UNIDAD_MEDIDA_NOMBRE)
                .Select(x => new SelectListItem
                {
                    Value = x.UNIDAD_MEDIDA_ID.Trim().ToUpper(),
                    Text = x.UNIDAD_MEDIDA_NOMBRE
                })
                .ToListAsync();

            vm.MaterialesEnvase = await _context.MATERIAL_ENVASE
                .AsNoTracking()
                .Where(x => !x.ELIMINADO && x.ESTADO == "ACTIVO")
                .OrderBy(x => x.MATERIAL_ENVASE_NOMBRE)
                .Select(x => new SelectListItem
                {
                    Value = x.MATERIAL_ENVASE_ID.Trim().ToUpper(),
                    Text = x.MATERIAL_ENVASE_NOMBRE
                })
                .ToListAsync();

            vm.TiposEmpaque = await _context.TIPO_EMPAQUE
                .AsNoTracking()
                .Where(x => !x.ELIMINADO && x.ESTADO == "ACTIVO")
                .OrderBy(x => x.TIPO_EMPAQUE_NOMBRE)
                .Select(x => new SelectListItem
                {
                    Value = x.TIPO_EMPAQUE_ID.Trim().ToUpper(),
                    Text = x.TIPO_EMPAQUE_NOMBRE
                })
                .ToListAsync();

            vm.Marcas = await _context.MARCA
                .AsNoTracking()
                .Where(x => !x.ELIMINADO && x.ESTADO == "ACTIVO")
                .OrderBy(x => x.MARCA_NOMBRE)
                .Select(x => new SelectListItem
                {
                    Value = x.MARCA_ID.Trim().ToUpper(),
                    Text = x.MARCA_NOMBRE
                })
                .ToListAsync();
        }














        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            // 1) Cargar entidad (sin tracking); ignorar eliminados
            var p = await _context.PRODUCTO
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PRODUCTO_ID == id && !x.ELIMINADO);

            if (p == null) return NotFound();

            // 2) Mapear a VM
            var vm = new ProductoViewModel
            {
                ProductoId = p.PRODUCTO_ID,
                CodigoProducto = p.PRODUCTO_CODIGO,
                ProductoNombre = p.PRODUCTO_NOMBRE,
                ProductoDescripcion = p.PRODUCTO_DESCRIPCION,

                SubCategoriaId = p.SUBCATEGORIA_ID,
                TipoProductoId = p.TIPO_PRODUCTO_ID,
                UnidadMedidaId = p.UNIDAD_MEDIDA_ID,
                MaterialEnvaseId = p.MATERIAL_ENVASE_ID,
                TipoEmpaqueId = p.TIPO_EMPAQUE_ID,
                MarcaId = p.MARCA_ID,

                ProductoImagen = p.PRODUCTO_IMG,   // ruta relativa web (/uploads/productos/xxxx.jpg)
                //IvaPorcentaje = p.IVA_PORCENTAJE, // si decides no usarlo, no se renderiza en la vista
                ESTADO = p.ESTADO,
                EstadoActivo = string.Equals(p.ESTADO, "ACTIVO", StringComparison.OrdinalIgnoreCase),

                CreadoPor = p.CREADO_POR,
                FechaCreacion = p.FECHA_CREACION,
                ModificadoPor = p.MODIFICADO_POR,
                FechaModificacion = p.FECHA_MODIFICACION,
                Eliminado = p.ELIMINADO,
                EliminadoPor = p.ELIMINADO_POR,
                FechaEliminacion = p.FECHA_ELIMINACION
            };

            // 3) Cargar combos (activos + incluir valor actual aunque esté inactivo)
            await CargarCombosEditAsync(vm);

            // Flags para modal de éxito/“sin cambios”
            ViewBag.UpdatedOk = TempData["UpdatedOk"];
            ViewBag.UpdatedName = TempData["UpdatedName"];
            ViewBag.NoChanges = TempData["NoChanges"];

            return View(vm); // Views/Producto/Edit.cshtml
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, ProductoViewModel vm)
        {
            // Validación ruta vs VM
            if (string.IsNullOrWhiteSpace(id) || !string.Equals(id, vm.ProductoId, StringComparison.Ordinal))
                return NotFound();

            // Sincronizar checkbox -> cadena ("ACTIVO"/"INACTIVO")
            var nuevoEstado = vm.EstadoActivo ? "ACTIVO" : "INACTIVO";
            vm.ESTADO = nuevoEstado;

            // Validaciones por DataAnnotations
            if (!ModelState.IsValid)
            {
                await CargarCombosEditAsync(vm);
                return View(vm);
            }

            // Cargar entidad (tracking)
            var entidad = await _context.PRODUCTO.FirstOrDefaultAsync(x => x.PRODUCTO_ID == id && !x.ELIMINADO);
            if (entidad == null) return NotFound();

            // ====== Normalización ======
            static string NN(string? s) => (s ?? string.Empty).Trim();
            static string NU(string? s) => (s ?? string.Empty).Trim().ToUpperInvariant();

            var nuevoNombre = NN(vm.ProductoNombre);
            var nuevoCodigo = NN(vm.CodigoProducto);
            var nuevaDesc = NN(vm.ProductoDescripcion);
            var subCatId = NU(vm.SubCategoriaId);
            var tipoProdId = NU(vm.TipoProductoId);
            var umId = NU(vm.UnidadMedidaId);
            var matEnvId = string.IsNullOrWhiteSpace(vm.MaterialEnvaseId) ? null : NU(vm.MaterialEnvaseId);
            var tipEmpId = string.IsNullOrWhiteSpace(vm.TipoEmpaqueId) ? null : NU(vm.TipoEmpaqueId);
            var marcaId = string.IsNullOrWhiteSpace(vm.MarcaId) ? null : NU(vm.MarcaId);
            //var nuevoIva = vm.IvaPorcentaje;

            // ====== Reglas de negocio ======
            // Unicidad de Código (si lo envían): excluye el propio producto
            if (!string.IsNullOrWhiteSpace(nuevoCodigo))
            {
                var codigoYaExiste = await _context.PRODUCTO
                    .AnyAsync(x => !x.ELIMINADO && x.PRODUCTO_ID != id && x.PRODUCTO_CODIGO == nuevoCodigo);
                if (codigoYaExiste)
                {
                    ModelState.AddModelError(nameof(vm.CodigoProducto), "Ya existe otro producto con ese código.");
                    await CargarCombosEditAsync(vm);
                    return View(vm);
                }
            }

            // ====== Detección de “sin cambios” (si no hay imagen nueva) ======
            bool sinCambios =
                string.Equals(entidad.PRODUCTO_NOMBRE ?? "", nuevoNombre, StringComparison.Ordinal) &&
                string.Equals(entidad.PRODUCTO_CODIGO ?? "", nuevoCodigo, StringComparison.Ordinal) &&
                string.Equals(entidad.PRODUCTO_DESCRIPCION ?? "", nuevaDesc, StringComparison.Ordinal) &&
                string.Equals(NU(entidad.SUBCATEGORIA_ID), subCatId, StringComparison.Ordinal) &&
                string.Equals(NU(entidad.TIPO_PRODUCTO_ID), tipoProdId, StringComparison.Ordinal) &&
                string.Equals(NU(entidad.UNIDAD_MEDIDA_ID), umId, StringComparison.Ordinal) &&
                string.Equals(entidad.MATERIAL_ENVASE_ID ?? "", matEnvId ?? "", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entidad.TIPO_EMPAQUE_ID ?? "", tipEmpId ?? "", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entidad.MARCA_ID ?? "", marcaId ?? "", StringComparison.OrdinalIgnoreCase) &&
                //entidad.IVA_PORCENTAJE == nuevoIva &&
                string.Equals(entidad.ESTADO ?? "", nuevoEstado, StringComparison.Ordinal) &&
                vm.ImagenArchivo == null; // si hay imagen nueva, sí hay cambios

            if (sinCambios)
            {
                TempData["NoChanges"] = true;
                return RedirectToAction(nameof(Edit), new { id });
            }

            // ====== Imagen: validar/guardar nuevo archivo (si viene) ======
            string? nuevoRel = null;
            string? nuevoFis = null;
            string? anteriorFis = null;

            if (vm.ImagenArchivo != null && vm.ImagenArchivo.Length > 0)
            {
                var contentType = vm.ImagenArchivo.ContentType?.ToLowerInvariant() ?? "";
                var permitido = contentType is "image/jpeg" or "image/jpg" or "image/png" or "image/webp";

                var ext = Path.GetExtension(vm.ImagenArchivo.FileName).ToLowerInvariant();
                var extOk = ext is ".jpg" or ".jpeg" or ".png" or ".webp";

                if (!permitido || !extOk)
                    ModelState.AddModelError(nameof(vm.ImagenArchivo), "Formato no permitido. Use JPG, PNG o WEBP.");

                const long maxBytes = 2 * 1024 * 1024; // 2MB
                if (vm.ImagenArchivo.Length > maxBytes)
                    ModelState.AddModelError(nameof(vm.ImagenArchivo), "La imagen supera el tamaño máximo (2 MB).");

                if (!ModelState.IsValid)
                {
                    await CargarCombosEditAsync(vm);
                    return View(vm);
                }

                // Folder físico: wwwroot/uploads/productos
                const string UploadRelPath = "/uploads/productos";
                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "productos");
                Directory.CreateDirectory(uploadsDir);

                // Nombre único: {ID}_{UTCtimestamp}{ext}
                var fileName = $"{id}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}";
                nuevoFis = Path.Combine(uploadsDir, fileName);

                await using (var fs = System.IO.File.Create(nuevoFis))
                    await vm.ImagenArchivo.CopyToAsync(fs);

                // Ruta web
                nuevoRel = $"{UploadRelPath}/{fileName}".Replace('\\', '/');

                // Ruta física de la anterior (para borrarla al final si todo sale bien)
                if (!string.IsNullOrWhiteSpace(entidad.PRODUCTO_IMG))
                {
                    var prev = entidad.PRODUCTO_IMG.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    anteriorFis = Path.Combine(_env.WebRootPath, prev);
                }
            }

            // ====== Auditoría ======
            var ahora = DateTime.Now;
            var usuarioNombre = (await (_auditoria?.GetUsuarioNombreAsync() ?? Task.FromResult(User?.Identity?.Name)))
                                ?? "Sistema";

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                // Aplicar cambios a la entidad
                entidad.PRODUCTO_NOMBRE = nuevoNombre;
                entidad.PRODUCTO_CODIGO = string.IsNullOrWhiteSpace(nuevoCodigo) ? null : nuevoCodigo;
                entidad.PRODUCTO_DESCRIPCION = string.IsNullOrWhiteSpace(nuevaDesc) ? null : nuevaDesc;

                entidad.SUBCATEGORIA_ID = subCatId;
                entidad.TIPO_PRODUCTO_ID = tipoProdId;
                entidad.UNIDAD_MEDIDA_ID = umId;
                entidad.MATERIAL_ENVASE_ID = matEnvId;
                entidad.TIPO_EMPAQUE_ID = tipEmpId;
                entidad.MARCA_ID = marcaId;

                //entidad.IVA_PORCENTAJE = nuevoIva;
                entidad.ESTADO = nuevoEstado;





                if (nuevoRel != null) // hubo imagen nueva
                    entidad.PRODUCTO_IMG = nuevoRel;

                // Auditoría de modificación
                entidad.MODIFICADO_POR = usuarioNombre;
                entidad.FECHA_MODIFICACION = ahora;

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                // Borrar físicamente la imagen anterior solo si se guardó todo OK
                if (!string.IsNullOrWhiteSpace(anteriorFis) && System.IO.File.Exists(anteriorFis))
                {
                    try { System.IO.File.Delete(anteriorFis); } catch { /* no interrumpir UX */ }
                }

                TempData["UpdatedOk"] = true;
                TempData["UpdatedName"] = entidad.PRODUCTO_NOMBRE;
                return RedirectToAction(nameof(Edit), new { id });
            }
            catch
            {
                await tx.RollbackAsync();

                // Rollback del archivo nuevo si se alcanzó a escribir
                if (!string.IsNullOrWhiteSpace(nuevoFis) && System.IO.File.Exists(nuevoFis))
                {
                    try { System.IO.File.Delete(nuevoFis); } catch { /* swallow */ }
                }

                // Mensaje genérico
                ModelState.AddModelError(string.Empty, "Ocurrió un error al actualizar el producto.");
                await CargarCombosEditAsync(vm);
                return View(vm);
            }
        }



        // Cargar combos de Edit (activos + incluir el actual aunque esté inactivo)
        // En los opcionales, si quieres permitir "sin seleccionar", déjalo a nivel de View con <option value="">Seleccione</option>
        private async Task CargarCombosEditAsync(ProductoViewModel vm)
        {
            vm.Subcategorias = await GetSubcategoriasActivasMasActualAsync(vm.SubCategoriaId);
            vm.TiposProducto = await GetTiposProductoActivosMasActualAsync(vm.TipoProductoId);
            vm.UnidadesMedida = await GetUnidadesMedidaActivasMasActualAsync(vm.UnidadMedidaId);
            vm.MaterialesEnvase = await GetMaterialesEnvaseActivosMasActualAsync(vm.MaterialEnvaseId);
            vm.TiposEmpaque = await GetTiposEmpaqueActivosMasActualAsync(vm.TipoEmpaqueId);
            vm.Marcas = await GetMarcasActivasMasActualAsync(vm.MarcaId);
        }

        // === Catálogos ===
        // Patrón: activos (no eliminados + ACTIVO) ORDENADOS + "el actual" si no está en la lista (inactivo)

        private async Task<IEnumerable<SelectListItem>> GetSubcategoriasActivasMasActualAsync(string actualId)
        {
            actualId = (actualId ?? "").Trim().ToUpperInvariant();
            var items = await _context.SUBCATEGORIA.AsNoTracking()
                .Where(x => !x.ELIMINADO && x.ESTADO == "ACTIVO")
                .OrderBy(x => x.SUBCATEGORIA_NOMBRE)
                .Select(x => new SelectListItem { Value = x.SUBCATEGORIA_ID.Trim().ToUpper(), Text = x.SUBCATEGORIA_NOMBRE })
                .ToListAsync();

            if (!string.IsNullOrEmpty(actualId) && !items.Any(i => i.Value == actualId))
            {
                var cur = await _context.SUBCATEGORIA.AsNoTracking().FirstOrDefaultAsync(x => x.SUBCATEGORIA_ID == actualId);
                if (cur != null) items.Insert(0, new SelectListItem { Value = cur.SUBCATEGORIA_ID.Trim().ToUpper(), Text = $"{cur.SUBCATEGORIA_NOMBRE} (inactiva)" });
            }
            return items;
        }

        private async Task<IEnumerable<SelectListItem>> GetTiposProductoActivosMasActualAsync(string actualId)
        {
            actualId = (actualId ?? "").Trim().ToUpperInvariant();
            var items = await _context.TIPO_PRODUCTO.AsNoTracking()
                .Where(x => !x.ELIMINADO && x.ESTADO == "ACTIVO")
                .OrderBy(x => x.TIPO_PRODUCTO_NOMBRE)
                .Select(x => new SelectListItem { Value = x.TIPO_PRODUCTO_ID.Trim().ToUpper(), Text = x.TIPO_PRODUCTO_NOMBRE })
                .ToListAsync();

            if (!string.IsNullOrEmpty(actualId) && !items.Any(i => i.Value == actualId))
            {
                var cur = await _context.TIPO_PRODUCTO.AsNoTracking().FirstOrDefaultAsync(x => x.TIPO_PRODUCTO_ID == actualId);
                if (cur != null) items.Insert(0, new SelectListItem { Value = cur.TIPO_PRODUCTO_ID.Trim().ToUpper(), Text = $"{cur.TIPO_PRODUCTO_NOMBRE} (inactivo)" });
            }
            return items;
        }

        private async Task<IEnumerable<SelectListItem>> GetUnidadesMedidaActivasMasActualAsync(string actualId)
        {
            actualId = (actualId ?? "").Trim().ToUpperInvariant();
            var items = await _context.UNIDAD_MEDIDA.AsNoTracking()
                .Where(x => !x.ELIMINADO && x.ESTADO == "ACTIVO")
                .OrderBy(x => x.UNIDAD_MEDIDA_NOMBRE)
                .Select(x => new SelectListItem { Value = x.UNIDAD_MEDIDA_ID.Trim().ToUpper(), Text = x.UNIDAD_MEDIDA_NOMBRE })
                .ToListAsync();

            if (!string.IsNullOrEmpty(actualId) && !items.Any(i => i.Value == actualId))
            {
                var cur = await _context.UNIDAD_MEDIDA.AsNoTracking().FirstOrDefaultAsync(x => x.UNIDAD_MEDIDA_ID == actualId);
                if (cur != null) items.Insert(0, new SelectListItem { Value = cur.UNIDAD_MEDIDA_ID.Trim().ToUpper(), Text = $"{cur.UNIDAD_MEDIDA_NOMBRE} (inactiva)" });
            }
            return items;
        }

        private async Task<IEnumerable<SelectListItem>> GetMaterialesEnvaseActivosMasActualAsync(string? actualId)
        {
            actualId = string.IsNullOrWhiteSpace(actualId) ? null : actualId.Trim().ToUpperInvariant();
            var items = await _context.MATERIAL_ENVASE.AsNoTracking()
                .Where(x => !x.ELIMINADO && x.ESTADO == "ACTIVO")
                .OrderBy(x => x.MATERIAL_ENVASE_NOMBRE)
                .Select(x => new SelectListItem { Value = x.MATERIAL_ENVASE_ID.Trim().ToUpper(), Text = x.MATERIAL_ENVASE_NOMBRE })
                .ToListAsync();

            if (!string.IsNullOrEmpty(actualId) && !items.Any(i => i.Value == actualId))
            {
                var cur = await _context.MATERIAL_ENVASE.AsNoTracking().FirstOrDefaultAsync(x => x.MATERIAL_ENVASE_ID == actualId);
                if (cur != null) items.Insert(0, new SelectListItem { Value = cur.MATERIAL_ENVASE_ID.Trim().ToUpper(), Text = $"{cur.MATERIAL_ENVASE_NOMBRE} (inactivo)" });
            }
            return items;
        }

        private async Task<IEnumerable<SelectListItem>> GetTiposEmpaqueActivosMasActualAsync(string? actualId)
        {
            actualId = string.IsNullOrWhiteSpace(actualId) ? null : actualId.Trim().ToUpperInvariant();
            var items = await _context.TIPO_EMPAQUE.AsNoTracking()
                .Where(x => !x.ELIMINADO && x.ESTADO == "ACTIVO")
                .OrderBy(x => x.TIPO_EMPAQUE_NOMBRE)
                .Select(x => new SelectListItem { Value = x.TIPO_EMPAQUE_ID.Trim().ToUpper(), Text = x.TIPO_EMPAQUE_NOMBRE })
                .ToListAsync();

            if (!string.IsNullOrEmpty(actualId) && !items.Any(i => i.Value == actualId))
            {
                var cur = await _context.TIPO_EMPAQUE.AsNoTracking().FirstOrDefaultAsync(x => x.TIPO_EMPAQUE_ID == actualId);
                if (cur != null) items.Insert(0, new SelectListItem { Value = cur.TIPO_EMPAQUE_ID.Trim().ToUpper(), Text = $"{cur.TIPO_EMPAQUE_NOMBRE} (inactivo)" });
            }
            return items;
        }

        private async Task<IEnumerable<SelectListItem>> GetMarcasActivasMasActualAsync(string? actualId)
        {
            actualId = string.IsNullOrWhiteSpace(actualId) ? null : actualId.Trim().ToUpperInvariant();
            var items = await _context.MARCA.AsNoTracking()
                .Where(x => !x.ELIMINADO && x.ESTADO == "ACTIVO")
                .OrderBy(x => x.MARCA_NOMBRE)
                .Select(x => new SelectListItem { Value = x.MARCA_ID.Trim().ToUpper(), Text = x.MARCA_NOMBRE })
                .ToListAsync();

            if (!string.IsNullOrEmpty(actualId) && !items.Any(i => i.Value == actualId))
            {
                var cur = await _context.MARCA.AsNoTracking().FirstOrDefaultAsync(x => x.MARCA_ID == actualId);
                if (cur != null) items.Insert(0, new SelectListItem { Value = cur.MARCA_ID.Trim().ToUpper(), Text = $"{cur.MARCA_NOMBRE} (inactiva)" });
            }
            return items;
        }



























        // GET: Productos/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pRODUCTO = await _context.PRODUCTO
                .Include(p => p.MARCA)
                .Include(p => p.MATERIAL_ENVASE)
                .Include(p => p.SUBCATEGORIA)
                .Include(p => p.TIPO_EMPAQUE)
                .Include(p => p.TIPO_PRODUCTO)
                .Include(p => p.UNIDAD_MEDIDA)
                .FirstOrDefaultAsync(m => m.PRODUCTO_ID == id);
            if (pRODUCTO == null)
            {
                return NotFound();
            }

            return View(pRODUCTO);
        }

        // POST: Productos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var pRODUCTO = await _context.PRODUCTO.FindAsync(id);
            if (pRODUCTO != null)
            {
                _context.PRODUCTO.Remove(pRODUCTO);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PRODUCTOExists(string id)
        {
            return _context.PRODUCTO.Any(e => e.PRODUCTO_ID == id);
        }
    }
}
