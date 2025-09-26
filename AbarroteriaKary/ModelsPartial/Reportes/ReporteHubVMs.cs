// ModelsPartial/Reportes/ReporteHubVMs.cs
using System;
using System.Collections.Generic;

namespace AbarroteriaKary.ModelsPartial
{
    public class ReporteHubIndexVM
    {
        // "COTIZACION" | "GENERAL" | "COMPRAS" | "CERRADO"
        public string Tipo { get; set; } = "GENERAL";
        public ReporteHubFiltroVM Filtros { get; set; } = new();
    }

    //public class ReporteHubFiltroVM
    //{
    //    // Comunes
    //    public string BaseFecha { get; set; } = "pedido"; // pedido|recibido
    //    public DateTime? Desde { get; set; }
    //    public DateTime? Hasta { get; set; }
    //    public string? Q { get; set; }

    //    // Multiselección por Id
    //    public List<string> Proveedores { get; set; } = new();
    //    public List<OpcionItemVM> ProveedoresOpciones { get; set; } = new();

    //    // Para tipos que piden 1 pedido
    //    public string? PedidoId { get; set; }
    //}

    public class OpcionItemVM
    {
        public string Id { get; set; } = "";
        public string Nombre { get; set; } = "";
    }
}
