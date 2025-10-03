// Services/Ventas/VentaTxService.cs
using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using AbarroteriaKary.ModelsPartial;
using AbarroteriaKary.Services.Correlativos;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AbarroteriaKary.Services.Ventas
{
    public class VentaTxService : IVentaTxService
    {
        private readonly KaryDbContext _db;
        private readonly ICorrelativoService _corr;

        public VentaTxService(KaryDbContext db, ICorrelativoService corr)
        {
            _db = db;
            _corr = corr;
        }

        public async Task<VentaTxResult> ConfirmarVentaAsync(
            VentaViewModel vm, VentaPagoViewModel pago, string usuarioNombre,
            CancellationToken ct = default)
        {
            // 0) Validar sesión de caja
            var sesion = await _db.CAJA_SESION
                .FirstOrDefaultAsync(s => !s.ELIMINADO &&
                                          s.SESION_ID == vm.SesionId &&
                                          s.ESTADO_SESION == "ABIERTA", ct);
            if (sesion is null)
                throw new InvalidOperationException("No hay sesión de caja abierta.");

            var ahora = DateTime.Now;

            await using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            try
            {
                // 1) ID definitivo para VENTA
                var ventaId = await _corr.NextVentaIdAsync(ct);

                // 2) Recalcular PVP por si viniera manipulado desde el cliente
                //    (PRECIO_HISTORICO con HASTA = NULL; si no existe, puedes fallback a PRODUCTO)
                foreach (var ln in vm.Lineas)
                {
                    var pvp = await _db.PRECIO_HISTORICO
                        .Where(p => !p.ELIMINADO && p.PRODUCTO_ID == ln.ProductoId && p.HASTA == null)
                        .OrderByDescending(p => p.DESDE)
                        .Select(p => p.PRECIO)   // <-- AJUSTA nombre de columna si es PRECIO_VENTA
                        .FirstOrDefaultAsync(ct);

                    if (pvp <= 0)
                        pvp = ln.PrecioUnitario; // fallback: lo que venía del VM

                    ln.PrecioUnitario = pvp;
                }

                var totalVenta = Math.Round(vm.Lineas.Sum(l => l.Subtotal), 2);

                // 3) Crear VENTA
                var v = new VENTA
                {
                    VENTA_ID = ventaId,
                    FECHA = vm.FechaVenta,
                    CLIENTE_ID = vm.ClienteId,
                    USUARIO_ID = vm.UsuarioId,
                    TOTAL = totalVenta,

                    CREADO_POR = usuarioNombre,
                    FECHA_CREACION = ahora,
                    ESTADO = "ACTIVO",
                    ELIMINADO = false
                };
                _db.VENTA.Add(v);
                await _db.SaveChangesAsync(ct);





                //// 4) FEFO/FIFO → expandir en lotes y crear DETALLE_VENTA por lote
                ////    - FEFO: primero menor FECHA_VENCIMIENTO (NULL al final)
                ////    - FIFO: si no tiene vencimiento, por FECHA_CREACION o INVENTARIO_ID
                //foreach (var grupo in vm.Lineas.GroupBy(l => l.ProductoId))
                //{
                //    var productoId = grupo.Key;
                //    var cantTotal = grupo.Sum(g => g.Cantidad);
                //    var pvp = grupo.First().PrecioUnitario;

                //    var lotes = await _db.INVENTARIO
                //        .Where(i => !i.ELIMINADO && i.PRODUCTO_ID == productoId && i.STOCK_ACTUAL > 0)
                //        .OrderBy(i => i.FECHA_VENCIMIENTO == null) // FEFO: vencimiento primero (false < true)
                //        .ThenBy(i => i.FECHA_VENCIMIENTO)
                //        .ThenBy(i => i.FECHA_CREACION)             // tie-break FIFO
                //        .ThenBy(i => i.INVENTARIO_ID)
                //        .Select(i => new {
                //            i.INVENTARIO_ID,
                //            i.PRODUCTO_ID,
                //            i.STOCK_ACTUAL,
                //            i.COSTO_UNITARIO,  // asumiendo existe
                //            i.FECHA_VENCIMIENTO
                //        })
                //        .ToListAsync(ct);

                //    if (!lotes.Any())
                //        throw new InvalidOperationException($"No hay stock disponible para el producto {productoId}.");

                //    decimal restante = cantTotal;
                //    foreach (var lote in lotes)
                //    {
                //        if (restante <= 0) break;

                //        var tomar = Math.Min(restante, lote.STOCK_ACTUAL);
                //        if (tomar <= 0) continue;

                //        // 4.1) Insertar DETALLE_VENTA por lote
                //        var detId = await _corr.NextDetalleVentaIdAsync(ct);
                //        var det = new DETALLE_VENTA
                //        {
                //            DETALLE_VENTA_ID = detId,
                //            VENTA_ID = ventaId,
                //            INVENTARIO_ID = lote.INVENTARIO_ID,
                //            PRODUCTO_ID = lote.PRODUCTO_ID,
                //            CANTIDAD = (int)tomar,         // tu columna es INT
                //            PRECIO_UNITARIO = pvp,

                //            CREADO_POR = usuarioNombre,
                //            FECHA_CREACION = ahora,
                //            ESTADO = "ACTIVO",
                //            ELIMINADO = false
                //        };
                //        _db.DETALLE_VENTA.Add(det);

                //        // 4.2) Descontar INVENTARIO
                //        var inv = await _db.INVENTARIO.FirstAsync(i => i.INVENTARIO_ID == lote.INVENTARIO_ID
                //                                                       && i.PRODUCTO_ID == lote.PRODUCTO_ID, ct);
                //        inv.STOCK_ACTUAL -= tomar;
                //        inv.MODIFICADO_POR = usuarioNombre;
                //        inv.FECHA_MODIFICACION = ahora;

                //        //4.3) KARDEX SALIDA(ajusta nombres de columnas según tu tabla KARDEX)
                //         _db.KARDEX.Add(new KARDEX
                //         {
                //             KARDEX_ID = await _corr.NextKardexIdAsync(ct), // solo si lo manejas por correlativo
                //             PRODUCTO_ID = lote.PRODUCTO_ID,
                //             //INVENTARIO_ID = lote.INVENTARIO_ID,
                //             FECHA = ahora,
                //             TIPO_MOVIMIENTO = "SALIDA",
                //             CANTIDAD = tomar,
                //             COSTO_UNITARIO = lote.COSTO_UNITARIO,
                //             REFERENCIA = ventaId,
                //             MOTIVO = "VENTA",
                //             CREADO_POR = usuarioNombre,
                //             FECHA_CREACION = ahora,
                //             ESTADO = "ACTIVO",
                //             ELIMINADO = false
                //         });

                //        restante -= tomar;
                //    }

                //    if (restante > 0)
                //        throw new InvalidOperationException($"Stock insuficiente para el producto {productoId}. Faltan {restante}.");
                //}












                // 4) FEFO/FIFO → expandir en lotes y crear DETALLE_VENTA por lote
                //    - FEFO: primero menor FECHA_VENCIMIENTO (NULL al final)
                //    - FIFO: si no tiene vencimiento, por FECHA_CREACION o INVENTARIO_ID
                foreach (var grupo in vm.Lineas.GroupBy(l => l.ProductoId))
                {
                    var productoId = grupo.Key;

                    // 👇 Convertimos a ENTERO porque DETALLE_VENTA.CANTIDAD e INVENTARIO.STOCK_ACTUAL son INT
                    int cantTotal = grupo.Sum(g => (int)g.Cantidad);

                    // PVP (decimal) para esa familia de líneas
                    var pvp = grupo.First().PrecioUnitario;

                    var lotes = await _db.INVENTARIO
                        .Where(i => !i.ELIMINADO && i.PRODUCTO_ID == productoId && i.STOCK_ACTUAL > 0)
                        .OrderBy(i => i.FECHA_VENCIMIENTO == null) // FEFO: primero los que SÍ tienen vencimiento
                        .ThenBy(i => i.FECHA_VENCIMIENTO)
                        .ThenBy(i => i.FECHA_CREACION)             // tie-break FIFO
                        .ThenBy(i => i.INVENTARIO_ID)
                        .Select(i => new
                        {
                            i.INVENTARIO_ID,
                            i.PRODUCTO_ID,
                            StockActual = i.STOCK_ACTUAL,   // 👈 int
                            i.COSTO_UNITARIO,               // decimal (para COGS/Kardex)
                            i.FECHA_VENCIMIENTO
                        })
                        .ToListAsync(ct);

                    if (!lotes.Any())
                        throw new InvalidOperationException($"No hay stock disponible para el producto {productoId}.");

                    int restante = cantTotal;

                    foreach (var lote in lotes)
                    {
                        if (restante <= 0) break;

                        // 👇 todo en ENTERO
                        int tomar = Math.Min(restante, lote.StockActual);
                        if (tomar <= 0) continue;

                        // 4.1) Insertar DETALLE_VENTA por lote (INT en CANTIDAD)
                        var detId = await _corr.NextDetalleVentaIdAsync(ct);
                        var det = new DETALLE_VENTA
                        {
                            DETALLE_VENTA_ID = detId,
                            VENTA_ID = ventaId,
                            INVENTARIO_ID = lote.INVENTARIO_ID,
                            PRODUCTO_ID = lote.PRODUCTO_ID,
                            CANTIDAD = tomar,                // 👈 int
                            PRECIO_UNITARIO = pvp,                  // decimal

                            CREADO_POR = usuarioNombre,
                            FECHA_CREACION = ahora,
                            ESTADO = "ACTIVO",
                            ELIMINADO = false
                        };
                        _db.DETALLE_VENTA.Add(det);

                        // 4.2) Descontar INVENTARIO (INT -= INT)
                        var inv = await _db.INVENTARIO.FirstAsync(i =>
                            i.INVENTARIO_ID == lote.INVENTARIO_ID && i.PRODUCTO_ID == lote.PRODUCTO_ID, ct);

                        inv.STOCK_ACTUAL -= tomar;             // 👈 ya no hay conflicto de tipos
                        inv.MODIFICADO_POR = usuarioNombre;
                        inv.FECHA_MODIFICACION = ahora;

                        //4.3) KARDEX SALIDA(si aplica — ajusta nombres de columnas a tu tabla)
                         _db.KARDEX.Add(new KARDEX
                         {
                             KARDEX_ID = await _corr.NextKardexIdAsync(ct),
                             PRODUCTO_ID = lote.PRODUCTO_ID,
                             //INVENTARIO_ID = lote.INVENTARIO_ID,
                             FECHA = ahora,
                             TIPO_MOVIMIENTO = "SALIDA",
                             CANTIDAD = tomar,                 // int
                             COSTO_UNITARIO = lote.COSTO_UNITARIO,   // decimal
                             REFERENCIA = ventaId,
                             MOTIVO = "VENTA",
                             CREADO_POR = usuarioNombre,
                             FECHA_CREACION = ahora,
                             ESTADO = "ACTIVO",
                             ELIMINADO = false
                         });

                        restante -= tomar;
                    }

                    if (restante > 0)
                        throw new InvalidOperationException($"Stock insuficiente para el producto {productoId}. Faltan {restante} unidades.");
                }





                await _db.SaveChangesAsync(ct);

                // 5) RECIBO (Pago)
                var reciboId = await _corr.NextReciboIdAsync(ct);
                var recibo = new RECIBO
                {
                    RECIBO_ID = reciboId,
                    VENTA_ID = ventaId,
                    METODO_PAGO_ID = pago.MetodoPagoId, // FK a METODO_PAGO
                    MONTO = totalVenta,
                    FECHA = ahora,

                    CREADO_POR = usuarioNombre,
                    FECHA_CREACION = ahora,
                    ESTADO = "ACTIVO",
                    ELIMINADO = false
                };
                _db.RECIBO.Add(recibo);

                // 6) MOVIMIENTO_CAJA (INGRESO) vinculado a la SESION
                var movId = await _corr.NextMovimientoCajaIdAsync(ct);
                var m = new MOVIMIENTO_CAJA
                {
                    MOVIMIENTO_ID = movId,
                    SESION_ID = vm.SesionId,
                    FECHA = ahora,
                    TIPO = "INGRESO",
                    MONTO = totalVenta,
                    REFERENCIA = ventaId,
                    DESCRIPCION = "VENTA CONTADO",

                    CREADO_POR = usuarioNombre,
                    FECHA_CREACION = ahora,
                    ESTADO = "ACTIVO",
                    ELIMINADO = false
                };
                _db.MOVIMIENTO_CAJA.Add(m);

                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return new VentaTxResult(ventaId, totalVenta);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
    }
}
