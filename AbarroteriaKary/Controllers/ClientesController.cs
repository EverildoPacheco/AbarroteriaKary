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
    public class ClientesController : Controller
    {
        private readonly KaryDbContext _context;

        public ClientesController(KaryDbContext context)
        {
            _context = context;
        }

        // GET: Clientes
        public async Task<IActionResult> Index()
        {
            var karyDbContext = _context.CLIENTE.Include(c => c.CLIENTENavigation);
            return View(await karyDbContext.ToListAsync());
        }

        // GET: Clientes/Details/5
        public async Task<IActionResult> Details(string id)
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

        // GET: Clientes/Create
        public IActionResult Create()
        {
            ViewData["CLIENTE_ID"] = new SelectList(_context.PERSONA, "PERSONA_ID", "PERSONA_ID");
            return View();
        }

        // POST: Clientes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("CLIENTE_ID,CLIENTE_NOTA,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO")] CLIENTE cLIENTE)
        {
            if (ModelState.IsValid)
            {
                _context.Add(cLIENTE);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["CLIENTE_ID"] = new SelectList(_context.PERSONA, "PERSONA_ID", "PERSONA_ID", cLIENTE.CLIENTE_ID);
            return View(cLIENTE);
        }

        // GET: Clientes/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var cLIENTE = await _context.CLIENTE.FindAsync(id);
            if (cLIENTE == null)
            {
                return NotFound();
            }
            ViewData["CLIENTE_ID"] = new SelectList(_context.PERSONA, "PERSONA_ID", "PERSONA_ID", cLIENTE.CLIENTE_ID);
            return View(cLIENTE);
        }

        // POST: Clientes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("CLIENTE_ID,CLIENTE_NOTA,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO")] CLIENTE cLIENTE)
        {
            if (id != cLIENTE.CLIENTE_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(cLIENTE);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CLIENTEExists(cLIENTE.CLIENTE_ID))
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
            ViewData["CLIENTE_ID"] = new SelectList(_context.PERSONA, "PERSONA_ID", "PERSONA_ID", cLIENTE.CLIENTE_ID);
            return View(cLIENTE);
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
