using System.Threading;
using System.Threading.Tasks;

namespace AbarroteriaKary.Services.Correlativos
{
    /// <summary>
    /// Servicio para generar correlativos/códigos por entidad.
    /// Nota: expongo métodos "peek" (solo para mostrar en GET)
    /// y "next" (para usar dentro de la transacción en el POST).
    /// </summary>
    //public interface ICorrelativoService
    //{
    //    /// <summary>
    //    /// Calcula el próximo ID de Área, solo para mostrar (no bloquea).
    //    /// </summary>
    //    Task<string> PeekNextAreaIdAsync(CancellationToken ct = default);

    //    /// <summary>
    //    /// Calcula el próximo ID de Área. Úselo dentro de la MISMA transacción
    //    /// donde hará el INSERT definitivo.
    //    /// </summary>
    //    Task<string> NextAreaIdAsync(CancellationToken ct = default);
    //}


    /// <summary>
    /// Servicio para generar correlativos por entidad.
    /// Peek = solo mostrar en GET (no bloquea);
    /// Next = usar dentro de la transacción en POST (insert real).
    /// </summary>
    public interface ICorrelativoService
    {
        // AREA (AREA000001)
        Task<string> PeekNextAreaIdAsync(CancellationToken ct = default);
        Task<string> NextAreaIdAsync(CancellationToken ct = default);

        // PUESTO (PUE0000001)
        Task<string> PeekNextPuestoIdAsync(CancellationToken ct = default);
        Task<string> NextPuestoIdAsync(CancellationToken ct = default);

        // EMPLEADO (EMP0000001)
        Task<string> PeekNextPersonaIdAsync(CancellationToken ct = default);
        Task<string> NextPersonaIdAsync(CancellationToken ct = default);

        // EMPLEADO (ROL0000007)
        Task<string> PeekNextRolIdAsync(CancellationToken ct = default);
        Task<string> NextRolIdAsync(CancellationToken ct = default);


        // EMPLEADO (USU0000007)
        Task<string> PeekNextUsuarioIdAsync(CancellationToken ct = default);
        Task<string> NextUsuarioIdAsync(CancellationToken ct = default);


        // EMPLEADO (PRO0000007)
        Task<string> PeekNextProveedorIdAsync(CancellationToken ct = default);
        Task<string> NextProveedorIdAsync(CancellationToken ct = default);


        // EMPLEADO (PRO0000007)
        Task<string> PeekNextClienteIdIdAsync(CancellationToken ct = default);
        Task<string> NextClienteIdIdAsync(CancellationToken ct = default);

        // EMPLEADO (PRO0000007)
        Task<string> PeekNextCategoriaIdAsync(CancellationToken ct = default);
        Task<string> NextCategoriaIdAsync(CancellationToken ct = default);

        Task<string> PeekNextSubCategoriaIdAsync(CancellationToken ct = default);
        Task<string> NextSubCategoriaIdAsync(CancellationToken ct = default);

        Task<string> PeekNextTipoProductoIdAsync(CancellationToken ct = default);
        Task<string> NextSubTipoProductoIdAsync(CancellationToken ct = default);

        Task<string> PeekNextMateriaEnvaseIdAsync(CancellationToken ct = default);
        Task<string> NextMateriaEnvaseIdAsync(CancellationToken ct = default);

        Task<string> PeekNextTipoEmpaqueIdAsync(CancellationToken ct = default);
        Task<string> NextTipoEmpaqueIdAsync(CancellationToken ct = default);

        Task<string> PeekNextUnidadDeMedidaIdAsync(CancellationToken ct = default);
        Task<string> NextUnidadDeMedidaIdAsync(CancellationToken ct = default);


        Task<string> PeekNextMarcaIdAsync(CancellationToken ct = default);
        Task<string> NextMarcaIdAsync(CancellationToken ct = default);

        Task<string> PeekNextProductoIdAsync(CancellationToken ct = default);
        Task<string> NextProductoIdAsync(CancellationToken ct = default);

        Task<string> PeekNextPedidosIdAsync(CancellationToken ct = default);
        Task<string> NextPedidosIdAsync(CancellationToken ct = default);

        Task<string> PeekNextDetallePedidosIdAsync(CancellationToken ct = default);


        // obsoleto
        Task<string> NextDetallePedidosIdAsync(CancellationToken ct = default);



        //--------------------------------------------

        // Rango de correlativos para DETALLE_PEDIDO 
        Task<IReadOnlyList<string>> NextDetallePedidosRangeAsync(int count, CancellationToken ct = default);

        // ID único para un solo detalle desde SEQUENCE (si lo quisieras usar suelto)
        Task<string> NextDetallePedidoFromSequenceAsync(CancellationToken ct = default);

        // --- NUEVOS: INVENTARIO ---

        Task<string> PeekNextInventarioIdAsync(CancellationToken ct = default);
        Task<string> NextInventarioIdAsync(CancellationToken ct = default);

        // --- NUEVOS: KARDEX ---
        Task<string> PeekNextKardexIdAsync(CancellationToken ct = default);
        Task<string> NextKardexIdAsync(CancellationToken ct = default);

        // --- NUEVOS: PRECIO_HISTORICO (columna PK = PRECIO_ID) ---
        Task<string> PeekNextPrecioHistoricoIdAsync(CancellationToken ct = default);
        Task<string> NextPrecioHistoricoIdAsync(CancellationToken ct = default);




        // CAJA (PRO0000007)
        Task<string> PeekNextCajaIdAsync(CancellationToken ct = default);
        Task<string> NextCajaIdAsync(CancellationToken ct = default);

        // CAJA SESCION (PRO0000007)



        //Task<string> PeekNextCajaSesionIdAsync(CancellationToken ct = default);
        //Task<string> NextCajaSesionIdAsync(CancellationToken ct = default);

        //// CAJA MOVIMIENTO CAJA (PRO0000007)

        //Task<string> PeekNextMovimientoCajaIdAsync(CancellationToken ct = default);
        //Task<string> NextMovimientoCajaIdAsync(CancellationToken ct = default);




        //Task<string> PeekNextVentaIdAsync(CancellationToken ct = default);
        //Task<string> NextVentaIdAsync(CancellationToken ct = default);



        //Task<string> PeekNextDetalleVentaIdAsync(CancellationToken ct = default);
        //Task<string> NextDetalleVentaIdAsync(CancellationToken ct = default);



        //Task<string> PeekNextReciboIdAsync(CancellationToken ct = default);
        //Task<string> NextReciboIdAsync(CancellationToken ct = default);






        // ===== CAJA_SESION (si aún NO tienes sequence para esto) =====
        Task<string> PeekNextCajaSesionIdAsync(CancellationToken ct = default);
        Task<string> NextCajaSesionIdAsync(CancellationToken ct = default);

        // ===== MOVIMIENTO_CAJA (MCA + 7, desde SEQUENCE) =====
        Task<string> PeekNextMovimientoCajaIdAsync(CancellationToken ct = default);
        Task<string> NextMovimientoCajaIdAsync(CancellationToken ct = default);
        Task<IReadOnlyList<string>> NextMovimientoCajaRangeAsync(int count, CancellationToken ct = default);

        // ===== VENTA (V + 9, desde SEQUENCE) =====
        Task<string> PeekNextVentaIdAsync(CancellationToken ct = default);
        Task<string> NextVentaIdAsync(CancellationToken ct = default);
        Task<IReadOnlyList<string>> NextVentaRangeAsync(int count, CancellationToken ct = default);

        // ===== DETALLE_VENTA (D + 9, desde SEQUENCE) =====
        Task<string> PeekNextDetalleVentaIdAsync(CancellationToken ct = default);
        Task<string> NextDetalleVentaIdAsync(CancellationToken ct = default);
        Task<IReadOnlyList<string>> NextDetalleVentaRangeAsync(int count, CancellationToken ct = default);

        // ===== RECIBO (R + 9, desde SEQUENCE) =====
        Task<string> PeekNextReciboIdAsync(CancellationToken ct = default);
        Task<string> NextReciboIdAsync(CancellationToken ct = default);
        Task<IReadOnlyList<string>> NextReciboRangeAsync(int count, CancellationToken ct = default);























    }
}
