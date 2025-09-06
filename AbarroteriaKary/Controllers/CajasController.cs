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
    public class CajasController : Controller
    {
        private readonly KaryDbContext _context;

        public CajasController(KaryDbContext context)
        {
            _context = context;
        }

        // GET: Cajas
        public async Task<IActionResult> Index()
        {
            return View(await _context.CAJA.ToListAsync());
        }

        // GET: Cajas/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cAJA = await _context.CAJA
                .FirstOrDefaultAsync(m => m.CAJA_ID == id);
            if (cAJA == null)
            {
                return NotFound();
            }

            return View(cAJA);
        }

        // GET: Cajas/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Cajas/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CAJA_ID,CAJA_NOMBRE,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO")] CAJA cAJA)
        {
            if (ModelState.IsValid)
            {
                _context.Add(cAJA);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(cAJA);
        }

        // GET: Cajas/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cAJA = await _context.CAJA.FindAsync(id);
            if (cAJA == null)
            {
                return NotFound();
            }
            return View(cAJA);
        }

        // POST: Cajas/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("CAJA_ID,CAJA_NOMBRE,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO")] CAJA cAJA)
        {
            if (id != cAJA.CAJA_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(cAJA);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CAJAExists(cAJA.CAJA_ID))
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
            return View(cAJA);
        }

        // GET: Cajas/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cAJA = await _context.CAJA
                .FirstOrDefaultAsync(m => m.CAJA_ID == id);
            if (cAJA == null)
            {
                return NotFound();
            }

            return View(cAJA);
        }

        // POST: Cajas/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var cAJA = await _context.CAJA.FindAsync(id);
            if (cAJA != null)
            {
                _context.CAJA.Remove(cAJA);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CAJAExists(string id)
        {
            return _context.CAJA.Any(e => e.CAJA_ID == id);
        }
    }
}
