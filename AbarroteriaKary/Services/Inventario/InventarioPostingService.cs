using AbarroteriaKary.Data;
using AbarroteriaKary.Models;
using AbarroteriaKary.Services.Correlativos;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AbarroteriaKary.Services.Inventario
{
    public class InventarioPostingService : IInventarioPostingService
    {
        private readonly KaryDbContext _db;
        private readonly ICorrelativoService _correlativos;

        public InventarioPostingService(KaryDbContext db, ICorrelativoService correlativos)
        {
            _db = db;
            _correlativos = correlativos;
        }

        public async Task<PosteoPedidoResultado> PostearPedidoAsync(string pedidoId, string usuario, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(pedidoId))
                throw new ArgumentException("pedidoId requerido.");

            pedidoId = pedidoId.Trim().ToUpper();
            var ahora = DateTime.Now;

            // 1) Cargar pedido y validar estado = RECIBIDO
            var ped = await _db.PEDIDO
                .FirstOrDefaultAsync(p => !p.ELIMINADO && p.PEDIDO_ID == pedidoId, ct)
                ?? throw new InvalidOperationException("Pedido no encontrado.");

            var estadoNombre = await _db.ESTADO_PEDIDO
                .Where(e => e.ESTADO_PEDIDO_ID == ped.ESTADO_PEDIDO_ID && !e.ELIMINADO)
                .Select(e => e.ESTADO_PEDIDO_NOMBRE)
                .FirstOrDefaultAsync(ct);

            if (!string.Equals((estadoNombre ?? "").Trim(), "RECIBIDO", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Solo se puede actualizar inventario cuando el pedido está en RECIBIDO.");

            // 2) Traer líneas NO posteadas (POSTEADO=0) con datos necesarios
            var lineas = await _db.DETALLE_PEDIDO
                .Where(d => !d.ELIMINADO && d.PEDIDO_ID == pedidoId && d.POSTEADO == false)
                .OrderBy(d => d.DETALLE_PEDIDO_ID)
                .ToListAsync(ct);

            if (lineas.Count == 0)
                return new PosteoPedidoResultado(0, 0, false);

            // 3) Validaciones de negocio para cierre: precios > 0, vencimiento presente, cantidad > 0
            var errores = lineas
                .Select(d =>
                {
                    var faltas = new System.Collections.Generic.List<string>();
                    if (d.CANTIDAD <= 0) faltas.Add("Cantidad (> 0)");
                    if (!d.PRECIO_PEDIDO.HasValue || d.PRECIO_PEDIDO <= 0) faltas.Add("Precio compra (> 0)");
                    if (!d.PRECIO_VENTA.HasValue || d.PRECIO_VENTA <= 0) faltas.Add("Precio venta (> 0)");
                    if (!d.FECHA_VENCIMIENTO.HasValue) faltas.Add("Fecha vencimiento");
                    return (det: d, faltas);
                })
                .Where(x => x.faltas.Count > 0)
                .ToList();

            if (errores.Count > 0)
            {
                // Puedes mapear a nombres de producto si quieres mensajes más descriptivos desde el controlador.
                var mensaje = string.Join("\n", errores.Select(x => $"• {x.det.PRODUCTO_ID}: {string.Join(", ", x.faltas)}"));
                throw new InvalidOperationException(mensaje);
            }

            // 4) Transacción serializable para evitar carreras (IDs + unique lote/fecha)
            using var tx = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

            try
            {
                int posteadas = 0, omitidas = 0;

                foreach (var d in lineas)
                {
                    var qty = d.CANTIDAD;                 // INT (DETALLE_PEDIDO.CANTIDAD)
                    var prod = d.PRODUCTO_ID;
                    var fv = d.FECHA_VENCIMIENTO;        // DATE (INVENTARIO.FECHA_VENCIMIENTO)
                    var lote = d.LOTE_CODIGO;              // NVARCHAR(50) NULL (ALTER aplicado en chat)
                    var pCompra = d.PRECIO_PEDIDO ?? 0m;    // DECIMAL(12,2) NOT NULL en INVENTARIO
                    var pVenta = d.PRECIO_VENTA ?? 0m;

                    if (qty <= 0) { omitidas++; continue; }

                    // 4.1 Buscar renglón INVENTARIO por (PRODUCTO_ID, FECHA_VENCIMIENTO, LOTE_CODIGO)
                    //var inv = await _db.INVENTARIO.FirstOrDefaultAsync(x =>
                    //       x.PRODUCTO_ID == prod
                    //    && x.FECHA_VENCIMIENTO == fv
                    //    && ((x.LOTE_CODIGO == null && lote == null) || x.LOTE_CODIGO == lote), ct);

                    //if (inv == null)
                    //{
                    //    // 4.2 Crear renglón de inventario (usa correlativo INV******)
                    //    var invId = await _correlativos.NextInventarioIdAsync(ct);

                    //    inv = new INVENTARIO
                    //    {
                    //        INVENTARIO_ID = invId,
                    //        PRODUCTO_ID = prod,
                    //        STOCK_ACTUAL = qty,             // INT
                    //        STOCK_MINIMO = 0,
                    //        COSTO_UNITARIO = pCompra,
                    //        FECHA_VENCIMIENTO = fv,
                    //        LOTE_CODIGO = lote,            // viene del ALTER
                    //        ESTADO = "ACTIVO",
                    //        ELIMINADO = false,
                    //        CREADO_POR = usuario,
                    //        FECHA_CREACION = ahora,
                    //        MODIFICADO_POR = usuario,
                    //        FECHA_MODIFICACION = ahora
                    //    };
                    //    _db.INVENTARIO.Add(inv);
                    //}
                    //else
                    //{
                    //    // 4.3 Sumar stock y actualizar costo (política: "último costo")
                    //    inv.STOCK_ACTUAL += qty;
                    //    inv.COSTO_UNITARIO = pCompra;
                    //    inv.MODIFICADO_POR = usuario;
                    //    inv.FECHA_MODIFICACION = ahora;
                    //}






                    // Normalizar lote: "" => null
                    string? loteNorm = string.IsNullOrWhiteSpace(lote) ? null : lote.Trim().ToUpper();

                    // 4.1 Buscar renglón INVENTARIO por (PRODUCTO_ID, FECHA_VENCIMIENTO, LOTE_CODIGO)
                    // ⚠ AsTracking para garantizar que EF vea los cambios
                    var inv = await _db.INVENTARIO
                        .AsTracking()
                        .FirstOrDefaultAsync(x =>
                               x.PRODUCTO_ID == prod
                            && x.FECHA_VENCIMIENTO == fv
                            && (
                                   (x.LOTE_CODIGO == null && loteNorm == null)
                                || (x.LOTE_CODIGO != null && loteNorm != null && x.LOTE_CODIGO == loteNorm)
                               ),
                            ct);

                    if (inv == null)
                    {
                        var invId = await _correlativos.NextInventarioIdAsync(ct);
                        inv = new INVENTARIO
                        {
                            INVENTARIO_ID = invId,
                            PRODUCTO_ID = prod,
                            STOCK_ACTUAL = qty,
                            STOCK_MINIMO = 0,
                            COSTO_UNITARIO = pVenta,
                            FECHA_VENCIMIENTO = fv,
                            LOTE_CODIGO = loteNorm,
                            ESTADO = "ACTIVO",
                            ELIMINADO = false,
                            CREADO_POR = usuario,
                            FECHA_CREACION = ahora,
                            MODIFICADO_POR = usuario,
                            FECHA_MODIFICACION = ahora
                        };
                        _db.INVENTARIO.Add(inv);
                    }
                    else
                    {
                        inv.STOCK_ACTUAL += qty;

                        // Política: último costo (si prefieres promedio, te paso fórmula)
                        inv.COSTO_UNITARIO = pVenta;

                        inv.MODIFICADO_POR = usuario;
                        inv.FECHA_MODIFICACION = ahora;

                        // Fuerza la marca como modificado por si el modelo tuviera SaveBehavior raro
                        _db.Entry(inv).Property(x => x.COSTO_UNITARIO).IsModified = true;
                        _db.Entry(inv).Property(x => x.STOCK_ACTUAL).IsModified = true;
                    }










                    // 4.4 KARDEX (ENTRADA)
                    //    Ojo: en tu BD la columna se llama FECHA (no FECHA_MOVIMIENTO).
                    //    TIPO_MOVIMIENTO debe ser 'ENTRADA' | 'SALIDA' | 'AJUSTE'.
                    var kdxId = await _correlativos.NextKardexIdAsync(ct);
                    _db.KARDEX.Add(new KARDEX
                    {
                        KARDEX_ID = kdxId,
                        PRODUCTO_ID = prod,
                        FECHA = ahora,          // KARDEX.FECHA
                        TIPO_MOVIMIENTO = "ENTRADA",
                        CANTIDAD = qty,
                        COSTO_UNITARIO = pCompra,
                        REFERENCIA = ped.PEDIDO_ID,  // opcional: referencia al pedido
                        LOTE_CODIGO = lote,           // viene del ALTER
                        ESTADO = "ACTIVO",
                        ELIMINADO = false,
                        CREADO_POR = usuario,
                        FECHA_CREACION = ahora,
                        MODIFICADO_POR = usuario,
                        FECHA_MODIFICACION = ahora
                    });

                    // 4.5 PRECIO_HISTORICO: cerrar vigente y crear nuevo si cambió el precio de venta
                    if (pVenta > 0)
                    {
                        var vigente = await _db.PRECIO_HISTORICO
                            .Where(ph => ph.PRODUCTO_ID == prod && ph.HASTA == null && !ph.ELIMINADO)
                            .FirstOrDefaultAsync(ct);

                        if (vigente == null || vigente.PRECIO != pVenta)
                        {
                            if (vigente != null)
                            {
                                vigente.HASTA = ahora; // cerrar vigente
                                vigente.MODIFICADO_POR = usuario;
                                vigente.FECHA_MODIFICACION = ahora;
                            }

                            var pchId = await _correlativos.NextPrecioHistoricoIdAsync(ct);
                            _db.PRECIO_HISTORICO.Add(new PRECIO_HISTORICO
                            {
                                PRECIO_ID = pchId,
                                PRODUCTO_ID = prod,
                                PRECIO = pVenta,
                                DESDE = ahora,
                                HASTA = null,
                                ESTADO = "ACTIVO",
                                ELIMINADO = false,
                                CREADO_POR = usuario,
                                FECHA_CREACION = ahora
                            });
                        }
                    }

                    // 4.6 Marcar la línea como posteada (idempotencia)
                    d.POSTEADO = true;   // ALTER aplicado
                    d.POSTEADO_EN = ahora;  // ALTER aplicado
                    d.MODIFICADO_POR = usuario;
                    d.FECHA_MODIFICACION = ahora;

                    posteadas++;
                }

                await _db.SaveChangesAsync(ct);

                // 4.7 Si ya NO quedan líneas sin postear -> poner CERRADO
                bool quedanSinPostear = await _db.DETALLE_PEDIDO
                    .AnyAsync(x => x.PEDIDO_ID == pedidoId && !x.ELIMINADO && x.POSTEADO == false, ct);

                bool cerrado = false;
                if (!quedanSinPostear)
                {
                    var idCerrado = await _db.ESTADO_PEDIDO
                        .Where(e => !e.ELIMINADO && e.ESTADO_PEDIDO_NOMBRE == "CERRADO")
                        .Select(e => e.ESTADO_PEDIDO_ID)
                        .FirstOrDefaultAsync(ct);

                    if (!string.IsNullOrEmpty(idCerrado))
                    {
                        ped.ESTADO_PEDIDO_ID = idCerrado!;
                        ped.MODIFICADO_POR = usuario;
                        ped.FECHA_MODIFICACION = ahora;
                        await _db.SaveChangesAsync(ct);
                        cerrado = true;
                    }
                }

                await tx.CommitAsync(ct);
                return new PosteoPedidoResultado(posteadas, 0 /*omitidas*/, cerrado);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
    }
}
