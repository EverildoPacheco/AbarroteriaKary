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
    public class SubCategoriasController : Controller
    {
        private readonly KaryDbContext _context;

        public SubCategoriasController(KaryDbContext context)
        {
            _context = context;
        }

        // GET: SubCategorias
        public async Task<IActionResult> Index()
        {
            var karyDbContext = _context.SUBCATEGORIA.Include(s => s.CATEGORIA);
            return View(await karyDbContext.ToListAsync());
        }

        // GET: SubCategorias/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sUBCATEGORIA = await _context.SUBCATEGORIA
                .Include(s => s.CATEGORIA)
                .FirstOrDefaultAsync(m => m.SUBCATEGORIA_ID == id);
            if (sUBCATEGORIA == null)
            {
                return NotFound();
            }

            return View(sUBCATEGORIA);
        }

        // GET: SubCategorias/Create
        public IActionResult Create()
        {
            ViewData["CATEGORIA_ID"] = new SelectList(_context.CATEGORIA, "CATEGORIA_ID", "CATEGORIA_ID");
            return View();
        }

        // POST: SubCategorias/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SUBCATEGORIA_ID,SUBCATEGORIA_NOMBRE,SUBCATEGORIA_DESCRIPCION,CATEGORIA_ID,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO")] SUBCATEGORIA sUBCATEGORIA)
        {
            if (ModelState.IsValid)
            {
                _context.Add(sUBCATEGORIA);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["CATEGORIA_ID"] = new SelectList(_context.CATEGORIA, "CATEGORIA_ID", "CATEGORIA_ID", sUBCATEGORIA.CATEGORIA_ID);
            return View(sUBCATEGORIA);
        }

        // GET: SubCategorias/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sUBCATEGORIA = await _context.SUBCATEGORIA.FindAsync(id);
            if (sUBCATEGORIA == null)
            {
                return NotFound();
            }
            ViewData["CATEGORIA_ID"] = new SelectList(_context.CATEGORIA, "CATEGORIA_ID", "CATEGORIA_ID", sUBCATEGORIA.CATEGORIA_ID);
            return View(sUBCATEGORIA);
        }

        // POST: SubCategorias/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("SUBCATEGORIA_ID,SUBCATEGORIA_NOMBRE,SUBCATEGORIA_DESCRIPCION,CATEGORIA_ID,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO")] SUBCATEGORIA sUBCATEGORIA)
        {
            if (id != sUBCATEGORIA.SUBCATEGORIA_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(sUBCATEGORIA);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SUBCATEGORIAExists(sUBCATEGORIA.SUBCATEGORIA_ID))
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
            ViewData["CATEGORIA_ID"] = new SelectList(_context.CATEGORIA, "CATEGORIA_ID", "CATEGORIA_ID", sUBCATEGORIA.CATEGORIA_ID);
            return View(sUBCATEGORIA);
        }

        // GET: SubCategorias/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sUBCATEGORIA = await _context.SUBCATEGORIA
                .Include(s => s.CATEGORIA)
                .FirstOrDefaultAsync(m => m.SUBCATEGORIA_ID == id);
            if (sUBCATEGORIA == null)
            {
                return NotFound();
            }

            return View(sUBCATEGORIA);
        }

        // POST: SubCategorias/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var sUBCATEGORIA = await _context.SUBCATEGORIA.FindAsync(id);
            if (sUBCATEGORIA != null)
            {
                _context.SUBCATEGORIA.Remove(sUBCATEGORIA);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SUBCATEGORIAExists(string id)
        {
            return _context.SUBCATEGORIA.Any(e => e.SUBCATEGORIA_ID == id);
        }
    }
}
