using System;

namespace AbarroteriaKary.ModelsPartial
{
    /// <summary>
    /// Ítem para grilla de Producto/Index.
    /// </summary>
    public class ProductoListItemViewModel
    {
        public string ProductoId { get; set; } = string.Empty;
        public string ProductoNombre { get; set; } = string.Empty;
        public string? CodigoProducto { get; set; }
        public string SubCategoriaNombre { get; set; } = string.Empty;
        public string? MarcaNombre { get; set; }
        public string? ImagenUrl { get; set; }      // ← miniatura en la vista
        public DateTime FechaCreacion { get; set; }
        public string ESTADO { get; set; } = "ACTIVO";
    }
}
