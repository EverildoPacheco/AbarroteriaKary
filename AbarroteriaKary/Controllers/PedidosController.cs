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
    public class PedidosController : Controller
    {
        private readonly KaryDbContext _context;

        public PedidosController(KaryDbContext context)
        {
            _context = context;
        }

        // GET: Pedidos
        public async Task<IActionResult> Index()
        {
            var karyDbContext = _context.PEDIDO.Include(p => p.ESTADO_PEDIDO).Include(p => p.PROVEEDOR);
            return View(await karyDbContext.ToListAsync());
        }

        // GET: Pedidos/Details/5
        public async Task<IActionResult> Details(string id)
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

        // GET: Pedidos/Create
        public IActionResult Create()
        {
            ViewData["ESTADO_PEDIDO_ID"] = new SelectList(_context.ESTADO_PEDIDO, "ESTADO_PEDIDO_ID", "ESTADO_PEDIDO_ID");
            ViewData["PROVEEDOR_ID"] = new SelectList(_context.PROVEEDOR, "PROVEEDOR_ID", "PROVEEDOR_ID");
            return View();
        }

        // POST: Pedidos/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PEDIDO_ID,FECHA_PEDIDO,ESTADO_PEDIDO_ID,FECHA_ENTREGA_ESTIMADA,OBSERVACIONES,PROVEEDOR_ID,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO")] PEDIDO pEDIDO)
        {
            if (ModelState.IsValid)
            {
                _context.Add(pEDIDO);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ESTADO_PEDIDO_ID"] = new SelectList(_context.ESTADO_PEDIDO, "ESTADO_PEDIDO_ID", "ESTADO_PEDIDO_ID", pEDIDO.ESTADO_PEDIDO_ID);
            ViewData["PROVEEDOR_ID"] = new SelectList(_context.PROVEEDOR, "PROVEEDOR_ID", "PROVEEDOR_ID", pEDIDO.PROVEEDOR_ID);
            return View(pEDIDO);
        }

        // GET: Pedidos/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pEDIDO = await _context.PEDIDO.FindAsync(id);
            if (pEDIDO == null)
            {
                return NotFound();
            }
            ViewData["ESTADO_PEDIDO_ID"] = new SelectList(_context.ESTADO_PEDIDO, "ESTADO_PEDIDO_ID", "ESTADO_PEDIDO_ID", pEDIDO.ESTADO_PEDIDO_ID);
            ViewData["PROVEEDOR_ID"] = new SelectList(_context.PROVEEDOR, "PROVEEDOR_ID", "PROVEEDOR_ID", pEDIDO.PROVEEDOR_ID);
            return View(pEDIDO);
        }

        // POST: Pedidos/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("PEDIDO_ID,FECHA_PEDIDO,ESTADO_PEDIDO_ID,FECHA_ENTREGA_ESTIMADA,OBSERVACIONES,PROVEEDOR_ID,CREADO_POR,FECHA_CREACION,MODIFICADO_POR,FECHA_MODIFICACION,ELIMINADO,ELIMINADO_POR,FECHA_ELIMINACION,ESTADO")] PEDIDO pEDIDO)
        {
            if (id != pEDIDO.PEDIDO_ID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(pEDIDO);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PEDIDOExists(pEDIDO.PEDIDO_ID))
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
            ViewData["ESTADO_PEDIDO_ID"] = new SelectList(_context.ESTADO_PEDIDO, "ESTADO_PEDIDO_ID", "ESTADO_PEDIDO_ID", pEDIDO.ESTADO_PEDIDO_ID);
            ViewData["PROVEEDOR_ID"] = new SelectList(_context.PROVEEDOR, "PROVEEDOR_ID", "PROVEEDOR_ID", pEDIDO.PROVEEDOR_ID);
            return View(pEDIDO);
        }

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
