using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AbarroteriaKary.Data;
using AbarroteriaKary.Models;

namespace AbarroteriaKary.Controllers
{
    public class ProveedoresController : Controller
    {
        private readonly KaryDbContext _context;

        public ProveedoresController(KaryDbContext context)
        {
            _context = context;
        }

        // GET: Proveedores
        public async Task<IActionResult> Index()
        {
            var karyDbContext = _context.PROVEEDOR.Include(p => p.PROVEEDORNavigation);
            return View(await karyDbContext.ToListAsync());
        }

        // GET: Proveedores/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pROVEEDOR = await _context.PROVEEDOR
                .Include(p => p.PROVEEDORNavigation)
                .FirstOrDefaultAsync(m => m.PROVEEDOR_ID == id);
            if (pROVEEDOR == null)
            {
                return NotFound();
            }

            return View(pROVEEDOR);
        }

        // GET: Proveedores/Create
        public IActionResult Create()
        {
            ViewData["PROVEEDOR_ID"] = new SelectList(_context.PERSONA, "PERSONA_ID", "PERSONA_ID");
            return View();
        }

        // POST: Proveedores/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PROVEEDOR_ID,PROVEEDOR_OBSERVACION,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO,EMPRESA")] PROVEEDOR pROVEEDOR)
        {
            if (ModelState.IsValid)
            {
                _context.Add(pROVEEDOR);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["PROVEEDOR_ID"] = new SelectList(_context.PERSONA, "PERSONA_ID", "PERSONA_ID", pROVEEDOR.PROVEEDOR_ID);
            return View(pROVEEDOR);
        }

        // GET: Proveedores/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pROVEEDOR = await _context.PROVEEDOR.FindAsync(id);
            if (pROVEEDOR == null)
            {
                return NotFound();
            }
            ViewData["PROVEEDOR_ID"] = new SelectList(_context.PERSONA, "PERSONA_ID", "PERSONA_ID", pROVEEDOR.PROVEEDOR_ID);
            return View(pROVEEDOR);
        }

        // POST: Proveedores/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("PROVEEDOR_ID,PROVEEDOR_OBSERVACION,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO,EMPRESA")] PROVEEDOR pROVEEDOR)
        {
            if (id != pROVEEDOR.PROVEEDOR_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(pROVEEDOR);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PROVEEDORExists(pROVEEDOR.PROVEEDOR_ID))
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
            ViewData["PROVEEDOR_ID"] = new SelectList(_context.PERSONA, "PERSONA_ID", "PERSONA_ID", pROVEEDOR.PROVEEDOR_ID);
            return View(pROVEEDOR);
        }

        // GET: Proveedores/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pROVEEDOR = await _context.PROVEEDOR
                .Include(p => p.PROVEEDORNavigation)
                .FirstOrDefaultAsync(m => m.PROVEEDOR_ID == id);
            if (pROVEEDOR == null)
            {
                return NotFound();
            }

            return View(pROVEEDOR);
        }

        // POST: Proveedores/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var pROVEEDOR = await _context.PROVEEDOR.FindAsync(id);
            if (pROVEEDOR != null)
            {
                _context.PROVEEDOR.Remove(pROVEEDOR);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PROVEEDORExists(string id)
        {
            return _context.PROVEEDOR.Any(e => e.PROVEEDOR_ID == id);
        }
    }
}
