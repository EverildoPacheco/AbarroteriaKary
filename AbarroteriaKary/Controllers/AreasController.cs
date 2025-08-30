using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.Services.Correlativos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;



namespace AbarroteriaKary.Controllers
{
    public class AreasController : Controller
    {
        private readonly KaryDbContext _context;
        private readonly ICorrelativoService _correlativos;


        public AreasController(KaryDbContext context, ICorrelativoService correlativos)
        {
            _context = context;
            _correlativos = correlativos;

        }

        // GET: Areas
        public async Task<IActionResult> Index()
        {
            return View(await _context.AREA.ToListAsync());

        }

        // GET: Areas/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var aREA = await _context.AREA
                .FirstOrDefaultAsync(m => m.AREA_ID == id);
            if (aREA == null)
            {
                return NotFound();
            }

            return View(aREA);
        }





        // =======================
        // GET: /Areas/Create
        // =======================
        [HttpGet] // <-- importante para evitar ambigüedad
        public async Task<IActionResult> Create()
        {
            var vm = new AreasViewModel
            {
                areaId = await _correlativos.PeekNextAreaIdAsync(), // solo mostrar
                estadoArea = "ACTIVO",
                FechaCreacion = DateTime.Now
            };

            ViewBag.Creado = TempData["Creado"];
            return View(vm); // La vista Create está tipada a AreasViewModel
        }

        // =======================
        // POST: /Areas/Create
        // =======================
        [HttpPost] // <-- importante para evitar ambigüedad
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AreasViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                // Reponer el ID tentativo si hay errores
                if (string.IsNullOrWhiteSpace(vm.areaId))
                    vm.areaId = await _correlativos.PeekNextAreaIdAsync();

                return View(vm);
            }

            var userName = User?.Identity?.Name ?? "Sistema";

            // Genere el ID definitivo y guarde en la MISMA transacción:
            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var nuevoId = await _correlativos.NextAreaIdAsync();

                var entidad = new AREA
                {
                    AREA_ID = nuevoId,
                    AREA_NOMBRE = (vm.areaNombre ?? string.Empty).Trim(),
                    AREA_DESCRIPCION = vm.areaDescripcion?.Trim(),

                    ESTADO = "ACTIVO",
                    ELIMINADO = false,

                    // Auditoría
                    CREADO_POR = userName,
                    FECHA_CREACION = DateTime.Now,
                    MODIFICADO_POR = null,
                    FECHA_MODIFICACION = null,
                    ELIMINADO_POR = null,
                    FECHA_ELIMINACION = null
                };

                _context.Add(entidad);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Creado"] = true;
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Ocurrió un error al guardar el Área. Inténtelo nuevamente.");

                // Reponer ID tentativo para que la vista no quede vacía
                vm.areaId = await _correlativos.PeekNextAreaIdAsync();
                return View(vm);
            }
        }
    


 

        // GET: Areas/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var aREA = await _context.AREA.FindAsync(id);
            if (aREA == null)
            {
                return NotFound();
            }
            return View(aREA);
        }




        // POST: Areas/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("AREA_ID,AREA_NOMBRE,AREA_DESCRIPCION,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO")] AREA aREA)
        {
            if (id != aREA.AREA_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(aREA);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AREAExists(aREA.AREA_ID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(aREA);
        }

        // GET: Areas/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var aREA = await _context.AREA
                .FirstOrDefaultAsync(m => m.AREA_ID == id);
            if (aREA == null)
            {
                return NotFound();
            }

            return View(aREA);
        }

        // POST: Areas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var aREA = await _context.AREA.FindAsync(id);
            if (aREA != null)
            {
                _context.AREA.Remove(aREA);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AREAExists(string id)
        {
            return _context.AREA.Any(e => e.AREA_ID == id);
        }
    }
}
